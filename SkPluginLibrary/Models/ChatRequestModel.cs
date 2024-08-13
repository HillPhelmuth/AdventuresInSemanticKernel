using System.Text;

namespace SkPluginLibrary.Models;


public class ChatRequestModel
{
    public ExecutionType ExecutionType { get; set; }
    public List<KernelPlugin> SelectedPlugins { get; set; } = [];
    public AIModel SelectedModel { get; set; }
    public List<string>? ExcludedFunctions { get; set; }
    public List<string>? RequredFunctions { get; set; }
    public Dictionary<string, string>? Variables { get; set; }
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Execution Type: {ExecutionType}");
        sb.AppendLine($"Selected Model: {SelectedModel}");
        sb.AppendLine($"Selected Plugins: {string.Join(", ", SelectedPlugins.Select(x => x.Name))}");
        sb.AppendLine($"Excluded Functions: {string.Join(", ", ExcludedFunctions?? [])}");
        sb.AppendLine($"Required Functions: {string.Join(", ", RequredFunctions ?? [])}");
        sb.AppendLine($"Variables: {Variables?.Count}");
        return sb.ToString();
    }
}