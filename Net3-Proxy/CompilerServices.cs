using System.Collections.Generic;

namespace System.Runtime.CompilerServices
{
    public sealed class ConditionalWeakTable<TKey, TValue> where TKey : class where TValue : class
    {
        public delegate TValue CreateValueCallback(TKey key);

        private readonly object _lock = new object();
        private readonly Dictionary<WeakReference<TKey>, TValue> items = new Dictionary<WeakReference<TKey>, TValue>();

        public ConditionalWeakTable()
        {
            GCTracker.OnGC += OnGC;
        }

        private static WeakReference<TKey> WeakRef(TKey key)
        {
            return new WeakReference<TKey>(key);
        }

        public void Add(TKey key, TValue value)
        {
            if (key == null)
            {
                throw new ArgumentException("Null key", nameof(key));
            }

            lock (_lock)
            {
                items.Add(WeakRef(key), value);
            }
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            if (key == null)
            {
                throw new ArgumentException("Null key", nameof(key));
            }

            value = null;
            lock (_lock)
            {
                return items.TryGetValue(WeakRef(key), out value);
            }
        }

        public TValue GetValue(TKey key, CreateValueCallback createValueCallback)
        {
            if (createValueCallback == null)
            {
                throw new ArgumentException("Null create delegate", nameof(createValueCallback));
            }

            lock (_lock)
            {
                if (TryGetValue(key, out TValue value))
                {
                    return value;
                }

                value = createValueCallback(key);
                Add(key, value);
                return value;
            }
        }

        public TValue GetOrCreateValue(TKey key)
        {
            return GetValue(key, k => Activator.CreateInstance<TValue>());
        }

        public bool Remove(TKey key)
        {
            if (key == null)
            {
                throw new ArgumentException("Null key", nameof(key));
            }

            return items.Remove(WeakRef(key));
        }

        ~ConditionalWeakTable()
        {
            GCTracker.OnGC -= OnGC;
        }

        private void OnGC()
        {
            // on each GC, we want to clear the entire set of empty keys
            WeakReference<TKey> nullWeakRef = WeakRef(null);
            while (items.Remove(nullWeakRef))
            {
                ; // just loop
            }
        }

        private sealed class KeyComparer : IEqualityComparer<WeakReference<TKey>>
        {
            public bool Equals(WeakReference<TKey> x, WeakReference<TKey> y)
            {
                return x.TryGetTarget(out TKey keyX) && y.TryGetTarget(out TKey keyY) && ReferenceEquals(keyX, keyY);
            }

            public int GetHashCode(WeakReference<TKey> obj)
            {
                return obj.TryGetTarget(out TKey key) ? key.GetHashCode() : 0;
            }
        }

        private sealed class GCTracker
        {
            private static readonly WeakReference<GCTracker> tracker = new WeakReference<GCTracker>(new GCTracker());
            public static event Action OnGC;

            ~GCTracker()
            {
                OnGC?.Invoke();
                if (!AppDomain.CurrentDomain.IsFinalizingForUnload() && !Environment.HasShutdownStarted)
                {
                    tracker.SetTarget(new GCTracker());
                }
            }
        }
    }
}