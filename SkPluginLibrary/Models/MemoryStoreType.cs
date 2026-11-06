namespace SkPluginLibrary.Models
{
    public enum MemoryStoreType
    {
        None,
        [Description("In Memory Vector store")]
        InMemory,
        [Description("SqlLite as VectorDb")]
        SqlLite
    }
}
