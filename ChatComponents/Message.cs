namespace ChatComponents
{
    public class Message
    {
        public Message(Role role, string content, int order)
        {
            Role = role;
            Content = content;
            TimeStamp = DateTime.Now;
            Order = order;
        }
        public int Order { get; set; }
        public string? Content { get; set; }
        public Role Role { get; set; }
        public DateTime TimeStamp { get; set; }
        public bool IsActiveStreaming { get; set; }

        public static Message UserMessage(string content, int order)
        {
            return new Message(Role.User, content, order);
        }

        public static Message AssistantMessage(string content, int order)
        {
            return new Message(Role.Assistant, content, order);
        }

        public string CssClass => Role.ToString().ToLower();
    }

    public enum Role
    {
        User, Assistant
    }
}
