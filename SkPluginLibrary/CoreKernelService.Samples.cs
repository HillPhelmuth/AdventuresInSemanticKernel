using System.Reflection;

namespace SkPluginLibrary;

public partial class CoreKernelService
{
    #region Samples from SK Repo

    public void HandleStringWritten(object? sender, string e)
    {
        StringWritten?.Invoke(sender, e);
    }

    public async Task RunExample(string typename)
    {
        var sw = new StringEventWriter();
        sw.StringWritten += HandleStringWritten;
        var temp = Console.Out;
        Console.SetOut(sw);
        var assembly = Assembly.GetExecutingAssembly();
        var type = assembly.GetTypes().FirstOrDefault(x => x.Name.Contains(typename));
        var namespaces = assembly.GetTypes()
            .Where(t => t.Namespace?.Contains("SkPluginLibrary.Examples") == true)
            .Distinct();
        //var instance = Activator.CreateInstance(assembly.FullName, type.FullName);
        var methodInfo = type.GetMethod("RunAsync");

        if (methodInfo != null && methodInfo.ReturnType == typeof(Task))
        {
            // Invoke the method on the instance
            var methodTask = (Task)methodInfo.Invoke(null, null)!;

            // Wait for the method to complete
            await methodTask;
        }
        else
        {
            Console.WriteLine("Method 'RunAsync' not found or does not return Task.");
        }

        Console.SetOut(temp);
    }

    #endregion
}