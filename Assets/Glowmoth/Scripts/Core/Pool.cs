using System.Collections.Generic;

namespace Glowmoth
{
    /// <summary>
    /// Minimal object pool. Level objects are created once (prewarmed) and recycled between levels,
    /// so loading a level never instantiates GameObjects after the first time.
    /// </summary>
    public sealed class Pool<T> where T : class
    {
        readonly Stack<T> free;
        readonly System.Func<T> create;
        readonly System.Action<T> onGet;
        readonly System.Action<T> onRelease;

        public Pool(System.Func<T> create, System.Action<T> onGet, System.Action<T> onRelease, int prewarm)
        {
            this.create = create;
            this.onGet = onGet;
            this.onRelease = onRelease;
            free = new Stack<T>(prewarm);
            for (int i = 0; i < prewarm; i++) free.Push(create());
        }

        public T Get()
        {
            T item = free.Count > 0 ? free.Pop() : create();
            if (onGet != null) onGet(item);
            return item;
        }

        public void Release(T item)
        {
            if (onRelease != null) onRelease(item);
            free.Push(item);
        }

        /// <summary>Releases every item in the list back to the pool and clears the list.</summary>
        public void ReleaseAll(List<T> items)
        {
            for (int i = 0; i < items.Count; i++) Release(items[i]);
            items.Clear();
        }
    }
}
