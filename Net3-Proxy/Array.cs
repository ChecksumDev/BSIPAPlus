using OgArray = System.Array;

namespace Net3_Proxy
{
    public static class Array
    {
        public static T[] Empty<T>()
        {
            return EmptyArray<T>.Value;
        }

        private static class EmptyArray<T>
        {
            public static readonly T[] Value = new T[0];
        }
    }
}