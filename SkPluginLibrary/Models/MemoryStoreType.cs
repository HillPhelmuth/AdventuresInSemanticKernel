namespace SkPluginLibrary.Models
{
    public enum MemoryStoreType
    {
        None,
        [Description("In Memory Vector store")]
        InMemory,
        [Description("SqlLite as VectorDb")]
        SqlLite,
        [Description("Redis as VectorDb")]
        Redis,
        [Description("Qdrant VectorDb")]
        Qdrant,
        [Description("Weaviate VectorDb")]
        Weaviate

    }
}
