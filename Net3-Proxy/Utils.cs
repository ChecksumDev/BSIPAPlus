using System.Collections;
using System.Collections.Generic;

namespace Net3_Proxy
{
    internal static class Utils
    {
        public static bool IsNullOrWhiteSpace(string value)
        {
            if (value == null)
            {
                return true;
            }

            for (int i = 0; i < value.Length; i++)
            {
                if (!char.IsWhiteSpace(value[i]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        ///     Adds a value to the beginning of the sequence.
        /// </summary>
        /// <typeparam name="T">the type of the elements of <paramref name="seq" /></typeparam>
        /// <param name="seq">a sequence of values</param>
        /// <param name="prep">the value to prepend to <paramref name="seq" /></param>
        /// <returns>a new sequence beginning with <paramref name="prep" /></returns>
        public static IEnumerable<T> Prepend<T>(this IEnumerable<T> seq, T prep)
        {
            return new PrependEnumerable<T>(seq, prep);
        }

        private sealed class PrependEnumerable<T> : IEnumerable<T>
        {
            private readonly T first;
            private readonly IEnumerable<T> rest;

            public PrependEnumerable(IEnumerable<T> rest, T first)
            {
                this.rest = rest;
                this.first = first;
            }

            public IEnumerator<T> GetEnumerator()
            {
                return new PrependEnumerator(this);
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            private sealed class PrependEnumerator : IEnumerator<T>
            {
                private readonly PrependEnumerable<T> enumerable;
                private readonly IEnumerator<T> restEnum;
                private int state;

                public PrependEnumerator(PrependEnumerable<T> enumerable)
                {
                    this.enumerable = enumerable;
                    restEnum = enumerable.rest.GetEnumerator();
                }

                public T Current { get; private set; }

                object IEnumerator.Current => Current;

                public void Dispose()
                {
                    restEnum.Dispose();
                }

                public bool MoveNext()
                {
                    switch (state)
                    {
                        case 0:
                            Current = enumerable.first;
                            state++;
                            return true;
                        case 1:
                            if (!restEnum.MoveNext())
                            {
                                state = 2;
                                return false;
                            }

                            Current = restEnum.Current;

                            return true;
                        case 2:
                        default:
                            return false;
                    }
                }

                public void Reset()
                {
                    restEnum.Reset();
                    state = 0;
                }
            }
        }
    }
}