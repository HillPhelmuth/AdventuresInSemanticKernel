namespace SkPluginLibrary.Abstractions;

public interface ICustomCombinations
{
    Task<string> FunctionCallStepwiseDndApi(string characterDescription,
        (string? Race, string? Class, string? Alignment) details,
        CancellationToken cancellationToken = default);
   
}