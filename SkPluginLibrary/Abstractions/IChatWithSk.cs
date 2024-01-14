namespace SkPluginLibrary.Abstractions;

public interface IChatWithSk
{
    IAsyncEnumerable<string> ExecuteChatWithSkStream(string query, string? history = null,
        CancellationToken cancellationToken = default);
}