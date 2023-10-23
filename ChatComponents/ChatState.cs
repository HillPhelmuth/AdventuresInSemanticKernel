using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ChatComponents
{
    public class ChatState : INotifyPropertyChanged
    {

        public event PropertyChangedEventHandler? PropertyChanged;

        public List<Message> ChatMessages { get; } = new();

        public void Reset()
        {
            ChatMessages.Clear();
            OnPropertyChanged(nameof(ChatMessages));
        }
        public void AddUserMessage(string message, int order)
        {
            ChatMessages.Add(Message.UserMessage(message, order));
            OnPropertyChanged(nameof(ChatMessages));
        }

        public void AddAssistantMessage(string message, int order)
        {
            ChatMessages.Add(Message.AssistantMessage(message, order));
            OnPropertyChanged(nameof(ChatMessages));
        }

        public void UpdateAssistantMessage(string token)
        {
            var message = ChatMessages.Last(x => x.Role == Role.Assistant).Content += token;
            OnPropertyChanged(nameof(ChatMessages));
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
