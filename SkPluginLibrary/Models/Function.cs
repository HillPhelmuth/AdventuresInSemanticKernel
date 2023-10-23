using Microsoft.SemanticKernel;

namespace SkPluginLibrary.Models;

public record Function(string Name, ISKFunction SkFunction)
{
    public int Order { get; set; }
}