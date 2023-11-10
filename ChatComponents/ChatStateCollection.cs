using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatComponents
{
    public class ChatStateCollection
    {
        private readonly ILogger<ChatStateCollection> _logger;

        public ChatStateCollection(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<ChatStateCollection>();
        }
        public Dictionary<string, ChatState> ChatStates { get; } = new();

        public ChatState CreateChatState(string viewId)
        {
            ChatStates[viewId] = new ChatState();
            _logger.LogInformation("Created ChatState for viewId {viewId}", viewId);
            return ChatStates[viewId];
        }

        public ChatState GetChatState(string viewId)
        {
            if (!ChatStates.ContainsKey(viewId))
            {
                _logger.LogError("ChatState for viewId {viewId} not found", viewId);
                throw new ArgumentException($"ChatState for viewId {viewId} not found");
            }
            return ChatStates[viewId];
        }

        public bool TryGetChatState(string viewId, out ChatState? chatState)
        {
            var chatViewIds = ChatStates.Keys;
            var chatViewIdsString = string.Join(", ", chatViewIds);
            _logger.LogInformation("TryGet for ViewId: {viewId}\nChatViewIds available: {chatViewIdsString}",viewId, chatViewIdsString);
            var tryGetChatState = ChatStates.TryGetValue(viewId, out var chat);
            chatState = chat;
            return tryGetChatState;
        }

    }
}
