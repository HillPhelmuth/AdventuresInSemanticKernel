using Microsoft.SemanticKernel;


namespace SkPluginLibrary.Abstractions;

public interface ICoreKernelExecution
{
    Task<ChatGptPluginManifest> GetManifest(ChatGptPlugin chatGptPlugin);
    Task<Dictionary<string, ISKFunction>> GetExternalPluginFunctions(ChatGptPluginManifest manifest);
    Task<string> ExecuteFunction(ISKFunction function, Dictionary<string, string>? variables = null);
    IAsyncEnumerable<string> ExecuteFunctionStream(ISKFunction function, Dictionary<string, string>? variables = null);
    IAsyncEnumerable<string> ChatWithActionPlanner(string query, ChatRequestModel chatRequestModel, bool runAsChat = true);
    IAsyncEnumerable<string> ChatWithSequentialPlanner(string query, ChatRequestModel chatRequestModel, bool runAsChat = true);
    Task<string> ExecuteFunctionChain(ChatRequestModel chatRequestModel);
    Task<List<PluginFunctions>> GetAllPlugins();
}