using System.Collections;
using System.Collections.Generic;

namespace Net3_Proxy
{
    public class IReadOnlyList<T> : IEnumerable<T>
    {
        private readonly IList<T> list;

        private IReadOnlyList(IList<T> lst)
        {
            list = lst;
        }

        public int Count => list.Count;

        public T this[int index] => list[index];

        public IEnumerator<T> GetEnumerator()
        {
            return list.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)list).GetEnumerator();
        }

        public static implicit operator IReadOnlyList<T>(List<T> list)
        {
            return new IReadOnlyList<T>(list);
        }
    }

    public class IReadOnlyDictionary<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>
    {
        private readonly IDictionary<TKey, TValue> dict;

        private IReadOnlyDictionary(IDictionary<TKey, TValue> d)
        {
            dict = d;
        }

        public int Count => dict.Count;

        public TValue this[TKey key] => dict[key];

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return dict.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return dict.GetEnumerator();
        }

        public bool ContainsKey(TKey key)
        {
            return dict.ContainsKey(key);
        }

        public bool TryGetValue(TKey key, out TValue val)
        {
            return dict.TryGetValue(key, out val);
        }

        public static implicit operator IReadOnlyDictionary<TKey, TValue>(Dictionary<TKey, TValue> dict)
        {
            return new IReadOnlyDictionary<TKey, TValue>(dict);
        }
    }
}