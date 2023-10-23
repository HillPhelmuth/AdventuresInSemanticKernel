namespace SkPluginLibrary.Models
{
    public class ContextItem
    {
        public ContextItem()
        {
            Id = Guid.NewGuid().ToString();
        }

        public ContextItem(string id)
        {
            Id = id;
        }
        public string Id { get; set; }
        public string? MemoryId { get; set; }
        public string? Prompt { get; set; }
        public List<double> Vector { get; set; } = new();
    }
    public interface IContext
    {
        string Id { get; set; }
        string Title { get; set; }
        string Content { get; set; }
    }
}
