using Microsoft.Extensions.AI;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ChatComponents
{
    public class ChatState : INotifyPropertyChanged
    {

        public event PropertyChangedEventHandler? PropertyChanged;

        public List<Message> ChatMessages { get; } = [];
        public List<ChatMessage> ChatHistory { get; } = [];
        public int MessageCount => ChatMessages.Count;

        public void Reset()
        {
            ChatMessages.Clear();
            ChatHistory.Clear();
            MessagePropertyChanged();
        }
        public void AddUserMessage(string message, int? order = null)
        {
            order ??= MessageCount + 1;
            ChatMessages.Add(Message.UserMessage(message, order.Value));
            ChatHistory.Add(new ChatMessage(ChatRole.User, message));
            MessagePropertyChanged();
        }

        public void AddAssistantMessage(string message, int? order = null)
        {
            order ??= MessageCount + 1;
            ChatMessages.Add(Message.AssistantMessage(message, order.Value));
            ChatHistory.Add(new ChatMessage(ChatRole.Assistant, message));
            MessagePropertyChanged();
        }
        /// <summary>
        /// Updates the last assistant message with the token.
        /// </summary>
        /// <param name="token"></param>
        public void UpdateAssistantMessage(string token)
        {
            if (ChatMessages.All(x => x.Role != Role.Assistant)) throw new ArgumentOutOfRangeException(nameof(token),"No assistant message found.");
            ChatMessages.Last(x => x.Role == Role.Assistant).Content += token;
            var historyMessage = ChatHistory.Last(x => x.Role == ChatRole.Assistant);
            historyMessage.Contents.OfType<TextContent>().Last().Text += token;
            MessagePropertyChanged();
        }
        /// <summary>
        /// Checks if last message is an assistant message and if so, appends the token to the message. Otherwise, adds a new assistant message.
        /// </summary>
        /// <param name="message"></param>
        public void UpsertAssistantMessage(string message)
        {
            if (ChatMessages.LastOrDefault()?.Role == Role.Assistant)
            {
                UpdateAssistantMessage(message);
            }
            else
            {
                AddAssistantMessage(message);
            }
        }
        private void MessagePropertyChanged()
        {
            OnPropertyChanged(nameof(ChatMessages));
            OnPropertyChanged(nameof(ChatHistory));
        }
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
       
    }
}
