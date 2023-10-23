namespace SkPluginLibrary.Models;

public class CodeOutputModel
{
    public string? Code { get; set; }
    public string? Output { get; set; }
    public override string ToString()
    {
        return $"Code:\n{Code}\n\nOutput:\n{Output}";
    }
}
