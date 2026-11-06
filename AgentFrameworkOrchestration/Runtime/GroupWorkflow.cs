using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace AgentFrameworkOrchestration;

public sealed class GroupWorkflow
{
    public static Workflow Build(IReadOnlyList<AIAgent> agents, GroupTransition transition, int turns,
        string stopText, IChatClient? selector = null)
    {
        if (agents.Count == 0) throw new ArgumentException("Add at least one participant.");
        if (!Enum.IsDefined(transition)) throw new ArgumentException("Unknown workflow transition.");
        if (turns is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(turns), "Turn limit must be between 1 and 100.");
        if (agents.Select(a => a.Id).Distinct().Count() != agents.Count) throw new ArgumentException("Participant IDs must be unique.");
        return AgentWorkflowBuilder.CreateGroupChatBuilderWith(participants =>
            new Manager(participants, transition, turns, stopText, selector)).AddParticipants(agents).Build();
    }

    public static async IAsyncEnumerable<AgentResponseUpdate> RunAsync(Workflow workflow, string input,
        Func<ToolApprovalRequestContent, CancellationToken, Task<bool>> approve,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var run = await InProcessExecution.RunStreamingAsync(workflow,
            new ChatMessage(ChatRole.User, input), cancellationToken: cancellationToken);
        await run.TrySendMessageAsync(new TurnToken(emitEvents: true));
        await foreach (var evt in run.WatchStreamAsync(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (evt is AgentResponseUpdateEvent update) yield return update.Update;
            if (evt is RequestInfoEvent request)
            {
                if (!request.Request.TryGetDataAs<ToolApprovalRequestContent>(out var approval) || approval is null)
                    throw new InvalidOperationException("The workflow requested an unsupported input.");
                var accepted = await approve(approval, cancellationToken);
                await run.SendResponseAsync(request.Request.CreateResponse(approval.CreateResponse(accepted)));
            }
            if (evt is WorkflowErrorEvent error) throw new InvalidOperationException($"Workflow execution failed: {error.Exception?.Message ?? error.ToString()}", error.Exception);
        }
        cancellationToken.ThrowIfCancellationRequested();
    }

    private sealed class Manager(IReadOnlyList<AIAgent> agents, GroupTransition transition, int turns,
        string stop, IChatClient? selector) : GroupChatManager
    {
        private int _next;
        private int _previous = -1;

        protected override async ValueTask<AIAgent> SelectNextAgentAsync(IReadOnlyList<ChatMessage> history,
            CancellationToken cancellationToken = default)
        {
            if (transition == GroupTransition.PromptBased)
            {
                if (selector is null) throw new InvalidOperationException("Prompt-based transitions require a selector client.");
                var choices = agents.Select((a, i) => new { index = i, a.Name, a.Description })
                    .Where(a => agents.Count == 1 || a.index != _previous).ToArray();
                var response = await selector.GetResponseAsync([
                    new ChatMessage(ChatRole.System, $"Select the next participant from {JsonSerializer.Serialize(choices)}. Return a JSON object with its index."),
                    new ChatMessage(ChatRole.User, string.Join("\n", history.TakeLast(10).Select(m => $"{m.AuthorName}: {m.Text}")))
                ], new ChatOptions
                {
                    MaxOutputTokens = 50,
                    ResponseFormat = ChatResponseFormat.ForJsonSchema(JsonSerializer.SerializeToElement(new
                    {
                        type = "object", properties = new { index = new { type = "integer", @enum = choices.Select(c => c.index).ToArray() } },
                        required = new[] { "index" }, additionalProperties = false
                    }), "participant")
                }, cancellationToken);
                using var selection = JsonDocument.Parse(response.Text);
                if (!selection.RootElement.TryGetProperty("index", out var selected) || selected.ValueKind != JsonValueKind.Number ||
                    !selected.TryGetInt32(out var index) || !choices.Any(c => c.index == index))
                    throw new InvalidOperationException("The selector returned an invalid participant index.");
                _previous = index;
                _next++;
                return agents[index];
            }
            var next = transition == GroupTransition.HubAndSpoke && _next % 2 == 0
                ? 0 : transition == GroupTransition.HubAndSpoke && agents.Count > 1
                    ? 1 + (_next / 2) % (agents.Count - 1) : _next % agents.Count;
            _next++;
            return agents[next];
        }

        protected override ValueTask<bool> ShouldTerminateAsync(IReadOnlyList<ChatMessage> history,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(_next >= turns || IterationCount >= turns ||
                (!string.IsNullOrEmpty(stop) && history.LastOrDefault() is { Role: var role } message &&
                    role == ChatRole.Assistant && message.Text.Contains(stop, StringComparison.OrdinalIgnoreCase)));
    }
}
