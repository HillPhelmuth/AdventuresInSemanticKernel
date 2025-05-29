using Microsoft.CodeAnalysis;
using System.Reflection;

namespace SkPluginLibrary.Services;

public class CompileResources
{
    public static List<PortableExecutableReference> PortableExecutableReferences
    {
        get
        {
            if (_portableExecutableReferences is null)
                Console.WriteLine("Getting Metadata references from assemblies in current domain.");
            _portableExecutableReferences ??= AppDomain.CurrentDomain.GetAssemblies().Where(assembly => !assembly.IsDynamic && !string.IsNullOrWhiteSpace(assembly.Location) && IsValidAssembly(assembly.Location))
                .Select(assembly => MetadataReference.CreateFromFile(assembly.Location)).Concat(AdditionalAssemblies().Select(x => MetadataReference.CreateFromFile(x))).Distinct()
                .ToList();
            Console.WriteLine($"Executable Refs Total: {_portableExecutableReferences.Count}");
            return _portableExecutableReferences;
        }
    }

    private static IEnumerable<string> AdditionalAssemblies()
    {
        var path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ??
                                Directory.GetCurrentDirectory());
        return Directory.GetFiles(path, "*.dll", SearchOption.TopDirectoryOnly).Where(IsValidAssembly);
        
    }

    private static bool IsValidAssembly(string assembyName)
    {
        return !assembyName.Contains("Cosmos") && !Path.GetFileName(assembyName).StartsWith("vc") && !Path.GetFileName(assembyName).StartsWith("msvcp");
    }
    //netstandard
    private static List<PortableExecutableReference>? _portableExecutableReferences;


}