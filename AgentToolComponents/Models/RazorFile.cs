using System.ComponentModel;

namespace AgentToolComponents.Models;

public class RazorFile
{
    [Description("Name of the razor file, e.g. Index.razor")]
    public required string Name { get; set; }

    [Description("Code of the razor file, e.g. @page \"/\" including @code block.")]
    public required string Code { get; set; }

}