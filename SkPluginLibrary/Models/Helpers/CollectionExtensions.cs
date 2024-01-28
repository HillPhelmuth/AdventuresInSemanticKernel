namespace SkPluginLibrary.Models.Helpers
{
    public static class CollectionExtensions
    {
        /// <summary>
        /// Inserts or updates a range of key-value pairs in a dictionary.
        /// </summary>
        /// <typeparam name="TKey">The type of the keys in the dictionary.</typeparam>
        /// <typeparam name="TValue">The type of the values in the dictionary.</typeparam>
        /// <param name="dictionary">The dictionary to insert or update the items in.</param>
        /// <param name="upsertItems">The key-value pairs to insert or update in the 
        public static void UpsertRange<TKey, TValue>(this Dictionary<TKey, TValue> dictionary,
            IDictionary<TKey, TValue> upsertItems) where TKey : notnull
        {
            foreach (var item in upsertItems)
            {
                dictionary[item.Key] = item.Value;

            }
        }
        /// <summary>
        /// Adds a range of key-value pairs to a dictionary, only if the key does not already exist.
        /// </summary>
        /// <typeparam name="TKey">The type of the keys in the dictionary.</typeparam>
        /// <typeparam name="TValue">The type of the values in the dictionary.</typeparam>
        /// <param name="dictionary">The dictionary to add the items to.</param>
        /// <param name="upsertItems">The key-value pairs to add to the dictionary.</param>
        public static void AddRange<TKey, TValue>(this Dictionary<TKey, TValue> dictionary,
            IDictionary<TKey, TValue> upsertItems) where TKey : notnull
        {
            foreach (var item in upsertItems.Where(item => !dictionary.ContainsKey(item.Key)))
            {
                dictionary.Add(item.Key, item.Value);
            }
        }
        public static Dictionary<TKey, TValue> UpsertConcat<TKey, TValue>(this Dictionary<TKey, TValue> dictionary,
            IDictionary<TKey, TValue> upsertItems) where TKey : notnull
        {
            foreach (var item in upsertItems)
            {
                dictionary[item.Key] = item.Value;

            }

            return dictionary;
        }

        public static Dictionary<TKey, TValue> AddConcat<TKey, TValue>(this Dictionary<TKey, TValue> dictionary,
            IDictionary<TKey, TValue> upsertItems) where TKey : notnull
        {
            foreach (var item in upsertItems.Where(item => !dictionary.ContainsKey(item.Key)))
            {
                dictionary.Add(item.Key, item.Value);
            }

            return dictionary;
        }
        public static IEnumerable<List<T>> SplitIntoChunks<T>(this List<T> source, int chunkSize)
        {
            for (int i = 0; i < source.Count; i += chunkSize)
            {
                yield return source.GetRange(i, Math.Min(chunkSize, source.Count - i));
            }
        }
        private static readonly Random Random = new();
        public static void Shuffle<T>(this IList<T> list)
        {
            var n = list.Count;
            while (n > 1)
            {
                n--;
                var k = Random.Next(n + 1);
                (list[k], list[n]) = (list[n], list[k]);
            }
        }
        public static void Move<T>(this List<T> list, int oldIndex, int newIndex)
        {
            if (oldIndex < 0 || oldIndex >= list.Count)
                throw new ArgumentOutOfRangeException(nameof(oldIndex), "Old index is out of range.");

            if (newIndex < 0 || newIndex >= list.Count)
                throw new ArgumentOutOfRangeException(nameof(newIndex), "New index is out of range.");

            T item = list[oldIndex];
            list.RemoveAt(oldIndex);
            list.Insert(newIndex, item);
        }
    }
}
