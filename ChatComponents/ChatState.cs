using Microsoft.SemanticKernel.ChatCompletion;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ChatComponents
{
    public class ChatState : INotifyPropertyChanged
    {

        public event PropertyChangedEventHandler? PropertyChanged;

        public List<Message> ChatMessages { get; } = [];
        public ChatHistory ChatHistory { get; set; } = [];
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
            ChatHistory.AddUserMessage(message);
            MessagePropertyChanged();
        }

        public void AddAssistantMessage(string message, int? order = null)
        {
            order ??= MessageCount + 1;
            ChatMessages.Add(Message.AssistantMessage(message, order.Value));
            ChatHistory.AddAssistantMessage(message);
            MessagePropertyChanged();
        }

        public void UpdateAssistantMessage(string token)
        {
            var message = ChatMessages.Last(x => x.Role == Role.Assistant).Content += token;
            ChatHistory.Last(x => x.Role == AuthorRole.Assistant).Content += token;
            MessagePropertyChanged();
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

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
