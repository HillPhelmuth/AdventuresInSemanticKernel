using Microsoft.AspNetCore.Components;

namespace AgentToolComponents;
public partial class SnippetRunner
{
    [Parameter] public Type? ComponentType { get; set; }
    [Parameter] public Dictionary<string, object?>? Parameters { get; set; }
}
