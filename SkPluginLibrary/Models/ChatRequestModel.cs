using Microsoft.SemanticKernel;

namespace SkPluginLibrary.Models;


public class ChatRequestModel
{
    public ExecutionType ExecutionType { get; set; }
    public List<KernelPlugin> SelectedPlugins { get; set; } = [];
    public List<string>? ExcludedFunctions { get; set; }
    public List<string>? RequredFunctions { get; set; }
    public Dictionary<string, string>? Variables { get; set; }
}