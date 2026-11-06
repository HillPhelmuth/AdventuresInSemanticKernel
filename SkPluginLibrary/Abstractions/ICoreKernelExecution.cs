using Microsoft.Extensions.AI;
using SemanticKernelAgentOrchestration.Models;

namespace SkPluginLibrary.Abstractions;

public interface ICoreKernelExecution
{
    Task<Dictionary<string, AIFunction>> GetExternalPluginFunctions(ChatGptPluginManifest manifest);
    IAsyncEnumerable<string> ChatWithAutoFunctionCalling(string query, ChatRequestModel chatRequestModel,
        bool runAsChat = true, string? askOverride = null, CancellationToken cancellationToken = default,
        bool resetChat = false);
    Task<Dictionary<PluginType, List<ToolPlugin>>> GetAllPlugins();
    IAsyncEnumerable<string> ChatWithStepwiseAgent(string query, ChatRequestModel chatRequestModel,
        bool runAsChat = true, string? askOverride = null, CancellationToken cancellationToken = default,
        bool resetChat = false);
    event Action<string>? YieldAdditionalText;
    Task<string> ExecuteFunction(AIFunction function, Dictionary<string, object>? variables = null);
    IAsyncEnumerable<string> ChatWithPlanAndExecuteWorkflow(string query, ChatRequestModel chatRequestModel,
        bool runAsChat = true, string? askOverride = null, CancellationToken cancellationToken = default,
        bool resetChat = false);
}
