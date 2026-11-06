using System.ComponentModel;

namespace AgentToolComponents.Models;

public class RazorInput
{
    [Description("Top level file name from `RazorFiles`")]
    public required string TopLevelComponentName { get; set; }

    [Description("Razor files for the generated blazor application")]
    public List<RazorFile> RazorFiles { get; set; } = [];
}