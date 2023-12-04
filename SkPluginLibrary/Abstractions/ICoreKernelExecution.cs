using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Orchestration;


namespace SkPluginLibrary.Abstractions;

public interface ICoreKernelExecution
{
    Task<Dictionary<string, ISKFunction>> GetExternalPluginFunctions(ChatGptPluginManifest manifest);

    IAsyncEnumerable<string> ChatWithActionPlanner(string query, ChatRequestModel chatRequestModel,
        bool runAsChat = true, string? askOverride = null, CancellationToken cancellationToken = default);
    IAsyncEnumerable<string> ChatWithSequentialPlanner(string query, ChatRequestModel chatRequestModel,
        bool runAsChat = true, string? askOverride = null, CancellationToken cancellationToken = default);
    Task<string> ExecuteFunctionChain(ChatRequestModel chatRequestModel, CancellationToken cancellationToken = default);
    Task<List<PluginFunctions>> GetAllPlugins();

    IAsyncEnumerable<string> ChatWithStepwisePlanner(string query, ChatRequestModel chatRequestModel,
        bool runAsChat = true, string? askOverride = null, CancellationToken cancellationToken = default);

    event Action<string>? YieldAdditionalText;
    Task<KernelResult> ExecuteKernelFunction(ISKFunction function, Dictionary<string, string>? variables = null);
    IAsyncEnumerable<string> ChatWithHandlebarsPlanner(string query, ChatRequestModel chatRequestModel, bool runAsChat = true, string? askOverride = null);
}