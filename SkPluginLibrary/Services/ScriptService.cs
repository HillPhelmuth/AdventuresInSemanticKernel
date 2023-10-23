using Microsoft.CodeAnalysis.CSharp.Scripting.Hosting;

namespace SkPluginLibrary.Services;

public class ScriptService
{
    private readonly ScriptSession _session;

    public ScriptService()
    {
        _session = new ScriptSession();
    }
    public async Task<string> EvaluateAsync(string code)
    {
        var evalOutput = await _session.EvaluateAsync(code);
        return evalOutput;
    }

    public async Task<string> ReEvaluateAsync(int commandIndex, string newCode)
    {
        var reEvalObject = await _session.ReEvaluateAsync(commandIndex, newCode);
        return CSharpObjectFormatter.Instance.FormatObject(reEvalObject);
    }
}