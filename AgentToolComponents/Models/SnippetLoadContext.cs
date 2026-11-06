using System.Reflection;
using System.Runtime.Loader;

namespace AgentToolComponents.Models;

public sealed class SnippetLoadContext : AssemblyLoadContext
{
    public SnippetLoadContext(bool isCollectible) : base(nameof(SnippetLoadContext), isCollectible) { }
    protected override Assembly? Load(AssemblyName assemblyName) => null; // fallback to default ALC
}