namespace SkPluginLibrary.Models;

public record Function(string Name, KernelFunction SkFunction)
{
    public int Order { get; set; }
}