namespace SkPluginLibrary.Models
{
    public class ContextItem(string id)
    {
        public ContextItem() : this(Guid.NewGuid().ToString())
        {
        }

        public string Id { get; set; } = id;
        public string? MemoryId { get; set; }
        public string? Prompt { get; set; }
        public List<double> Vector { get; set; } = new();
    }
   
}
