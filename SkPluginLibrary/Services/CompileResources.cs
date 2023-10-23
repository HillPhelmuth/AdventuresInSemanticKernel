using Microsoft.CodeAnalysis;
using System.Reflection;

namespace SkPluginLibrary.Services;

public class CompileResources
{
    public CompileResources()
    {
        //Parallel.Invoke(
        //    () =>
        //    {
        //        Console.WriteLine("Invoke For Parallel 1");
        //    },
        //    () =>
        //    {
        //        Console.WriteLine("Invoke For Parallel 2");
        //    });
        //TrySomething();
        //var logger = LogBuilder();
        //Task task = new Task(() => Console.WriteLine(""));
    }

    static CompileResources()
    {
        //Parallel.Invoke(
        //    () =>
        //    {
        //        Console.WriteLine("Invoke For Parallel 1");
        //    },
        //    () =>
        //    {
        //        Console.WriteLine("Invoke For Parallel 2");
        //    });
        ////TrySomething();
        //var logger = LogBuilder();
        ////Task task = StartChatAsync();
        ////Task task2 = RunSettingsAsync();
        //CoreKernelService coreKernelService;

    }
    public static List<PortableExecutableReference> PortableExecutableReferences
    {
        get
        {
            _portableExecutableReferences ??= AppDomain.CurrentDomain.GetAssemblies().Where(assembly => !assembly.IsDynamic && !string.IsNullOrWhiteSpace(assembly.Location))
                .Select(assembly => MetadataReference.CreateFromFile(assembly.Location)).Concat(AdditionalAssemblies().Select(x => MetadataReference.CreateFromFile(x))).Distinct()
                .ToList();
            //.Where(x => x is { FullName: not null, IsDynamic: false } && !string.IsNullOrWhiteSpace(x.Location) &&
            //            (x.FullName.Contains("System") || x.FullName.Contains("Azure") || x.FullName.Contains("netstandard") || x.FullName.Contains("Microsoft.CodeAnalysis") || x.FullName.Contains("Microsoft.Extensions") || x.FullName.Contains("SkPluginLibrary") || x.FullName.Contains("SemanticKernel")))
            //.Select(assembly => MetadataReference.CreateFromFile(assembly.Location)).ToList();
            Console.WriteLine($"Executable Refs Total: {_portableExecutableReferences.Count}");
            return _portableExecutableReferences;
        }
    }

    private static List<string> AdditionalAssemblies()
    {
        var path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ??
                                Directory.GetCurrentDirectory());
        var files = Directory.GetFiles(path, "*.dll", SearchOption.TopDirectoryOnly);
        return files.ToList();
    }
    //netstandard
    private static List<PortableExecutableReference>? _portableExecutableReferences;


}