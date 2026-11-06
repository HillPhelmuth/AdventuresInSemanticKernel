using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;

namespace SkPluginLibrary.Services;

/// <summary>
/// Renders simple <c>{{$variable}}</c> prompt templates and runs them against a chat client.
/// This is the Microsoft.Extensions.AI replacement for "inline prompt functions".
/// </summary>
public static partial class PromptRunner
{
    public static string Render(string template, IDictionary<string, object?>? variables)
    {
        if (variables is null || variables.Count == 0) return template;
        return PlaceholderRegex().Replace(template, m =>
            variables.TryGetValue(m.Groups[1].Value, out var v) ? v?.ToString() ?? "" : m.Value);
    }

    private static List<ChatMessage> Messages(string prompt, string? system)
    {
        var messages = new List<ChatMessage>();
        if (!string.IsNullOrEmpty(system)) messages.Add(new ChatMessage(ChatRole.System, system));
        messages.Add(new ChatMessage(ChatRole.User, prompt));
        return messages;
    }

    public static async Task<string> RunAsync(IChatClient client, string template, IDictionary<string, object?>? variables = null,
        ChatOptions? options = null, string? system = null, CancellationToken cancellationToken = default)
    {
        var response = await client.GetResponseAsync(Messages(Render(template, variables), system), options, cancellationToken);
        return response.Text;
    }

    /// <summary>Runs a prompt and deserializes the model's reply into <typeparamref name="T"/> (structured output).</summary>
    public static async Task<T?> RunAsync<T>(IChatClient client, string template, IDictionary<string, object?>? variables = null,
        ChatOptions? options = null, string? system = null, CancellationToken cancellationToken = default)
    {
        var response = await client.GetResponseAsync<T>(Messages(Render(template, variables), system), options, cancellationToken: cancellationToken);
        return response.Result;
    }

    public static async IAsyncEnumerable<string> StreamAsync(IChatClient client, string template, IDictionary<string, object?>? variables = null,
        ChatOptions? options = null, string? system = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var update in client.GetStreamingResponseAsync(Messages(Render(template, variables), system), options, cancellationToken))
        {
            if (!string.IsNullOrEmpty(update.Text)) yield return update.Text;
        }
    }

    [GeneratedRegex(@"\{\{\s*\$(\w+)\s*\}\}")]
    private static partial Regex PlaceholderRegex();
}
