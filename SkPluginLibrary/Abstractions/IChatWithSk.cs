using Microsoft.SemanticKernel.ChatCompletion;

namespace SkPluginLibrary.Abstractions;

public interface IChatWithSk
{
    IAsyncEnumerable<string> ExecuteChatWithSkStream(string query,
        ChatHistory? chatHistory = null,
        CancellationToken cancellationToken = default);
}