using Microsoft.SemanticKernel;


namespace SkPluginLibrary.Abstractions;

public interface ICoreKernelExecution
{
    Task<Dictionary<string, KernelFunction>> GetExternalPluginFunctions(ChatGptPluginManifest manifest);

    IAsyncEnumerable<string> ChatWithAutoFunctionCalling(string query, ChatRequestModel chatRequestModel,
        bool runAsChat = true, string? askOverride = null, CancellationToken cancellationToken = default);
    IAsyncEnumerable<string> ChatWithSequentialPlanner(string query, ChatRequestModel chatRequestModel,
        bool runAsChat = true, string? askOverride = null, CancellationToken cancellationToken = default);
    //Task<string> ExecuteFunctionChain(ChatRequestModel chatRequestModel, CancellationToken cancellationToken = default);
    Task<Dictionary<PluginType, List<KernelPlugin>>> GetAllPlugins();

    IAsyncEnumerable<string> ChatWithStepwisePlanner(string query, ChatRequestModel chatRequestModel,
        bool runAsChat = true, string? askOverride = null, CancellationToken cancellationToken = default);

    event Action<string>? YieldAdditionalText;
    Task<FunctionResult> ExecuteKernelFunction(KernelFunction function, Dictionary<string, string>? variables = null);
    IAsyncEnumerable<string> ChatWithHandlebarsPlanner(string query, ChatRequestModel chatRequestModel,
        bool runAsChat = true, string? askOverride = null, CancellationToken cancellationToken = default);

    event Action<SimpleChatMessage>? DndPlannerFunctionHook;
}