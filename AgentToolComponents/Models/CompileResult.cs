using System.Reflection;
using AgentToolComponents.Models.RazorGen;

namespace AgentToolComponents.Models;

public sealed class CompileResult(Assembly asm, SnippetLoadContext ctx, Dictionary<string, Type> map)
{
    public Assembly Assembly { get; } = asm;
    public SnippetLoadContext LoadContext { get; } = ctx;
    public IReadOnlyDictionary<string, Type> ComponentTypesByPath { get; } = map;

    public IEnumerable<CustomDiag> Diagnostics { get; set; } = [];
    public Type? TryGetComponentType(string pathOrRelative)
    {
        
        var item = ComponentTypesByPath.GetValueOrDefault(pathOrRelative.Replace('\\', '/').TrimStart('/'));
        if (item is not null)
            return item;
        if (pathOrRelative.Contains(".razor")) pathOrRelative = pathOrRelative.Replace(".razor", "");
        var match = ComponentTypesByPath.FirstOrDefault(kv => kv.Key.Contains(pathOrRelative, StringComparison.OrdinalIgnoreCase));
        return match.Value;
    }

    public void Unload()
    {
        LoadContext.Unload();
        GC.Collect(); GC.WaitForPendingFinalizers();
    }
}