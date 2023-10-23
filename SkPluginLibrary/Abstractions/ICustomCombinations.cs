namespace SkPluginLibrary.Abstractions;

public interface ICustomCombinations
{
    Task<string> SequentialDndApi(string characterDescription, (string Race, string Class, string Alignment) details);
   
}