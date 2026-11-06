using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using AgentToolComponents.Models.RazorGen;

namespace AgentToolComponents.Models;



public sealed class RazorSnippetCompiler
{
    
    // === Multi-file (.razor) compilation with nested paths and _Imports.razor support ===
    public async Task<CompileResult> CompileComponentsAsync(
         IReadOnlyDictionary<string, string> files,       // e.g. { "Pages/Demo.razor": "...", "Shared/Counter.razor": "...", "_Imports.razor": "@using ..." }
         string topComponent = "",
         string rootNamespace = "Playground.Dynamic",
         IEnumerable<MetadataReference>? extraRefs = null)
    {
        var razorCompile = new RazorCompile();
        await razorCompile.InitAsync();
        var result = await
            razorCompile.CompileToAssemblyAsync(files.Select(x => new RazorFile { Name = x.Key, Code = x.Value })
                .ToList());
        return result.BlazorComponentResult ?? throw new InvalidOperationException("Blazor compilation failed:\n" +
            string.Join("\n", result.Diagnostics.Select(d => $"{d.Code}: {d.Description} ({d.File}:{d.Line})")));
        
    }


    // Minimal in-memory Razor FS; nested folders map into namespaces
}
public sealed class MetadataReferenceComparer : IEqualityComparer<MetadataReference>
{
    public static readonly MetadataReferenceComparer Instance = new();
    public bool Equals(MetadataReference? x, MetadataReference? y)
        => string.Equals((x as PortableExecutableReference)?.FilePath, (y as PortableExecutableReference)?.FilePath,
            StringComparison.OrdinalIgnoreCase);
    public int GetHashCode(MetadataReference obj)
        => ((obj as PortableExecutableReference)?.FilePath ?? "").ToLowerInvariant().GetHashCode();
}