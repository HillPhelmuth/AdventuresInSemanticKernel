using Microsoft.Extensions.AI;

namespace SkPluginLibrary.Models;

public record Function(string Name, AIFunction SkFunction)
{
    public int Order { get; set; }
}
