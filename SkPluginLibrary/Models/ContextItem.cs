using System.Text;
using Microsoft.SemanticKernel.ChatCompletion;

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
       
    }
    public class MyChatHistory : ChatHistory
    {
        public event Action<ChatMessageContent>? OnChatMessageContent;
        public new void AddMessage(
            AuthorRole authorRole,
            string content,
            Encoding? encoding = null,
            IReadOnlyDictionary<string, object?>? metadata = null)
        {
            var chatMessageContent = new ChatMessageContent(authorRole, content, encoding: encoding, metadata: metadata);
            OnChatMessageContent?.Invoke(chatMessageContent);
            Add(chatMessageContent);
        }
    }
   
}
