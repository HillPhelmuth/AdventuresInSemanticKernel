namespace SkPluginLibrary.Models.Helpers
{
    public static class CollectionExtensions
    {
        public static Dictionary<TKey, TValue> UpsertRange<TKey, TValue>(this Dictionary<TKey, TValue> dictionary,
            IDictionary<TKey, TValue> upsertItems) where TKey : notnull
        {
            foreach (var item in upsertItems)
            {
                dictionary[item.Key] = item.Value;

            }

            return dictionary;
        }

        public static Dictionary<TKey, TValue> AddRange<TKey, TValue>(this Dictionary<TKey, TValue> dictionary,
            IDictionary<TKey, TValue> upsertItems) where TKey : notnull
        {
            foreach (var item in upsertItems.Where(item => !dictionary.ContainsKey(item.Key)))
            {
                dictionary.Add(item.Key, item.Value);
            }

            return dictionary;
        }
    }
}
