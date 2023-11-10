using System.Text;

namespace SkPluginLibrary.Models;

public struct StepwiseExecutionResult
{
    public string? Model;
    public string? Question;
    public string? Answer;
    public string? StepsTaken;
    public string? Iterations;
    public string? Functions;
    public string? TimeTaken;
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"- **Question** {Question}");
        sb.AppendLine($"- **Steps** {StepsTaken}");
        sb.AppendLine($"- **Iterations** {Iterations}");
        sb.AppendLine($"- **Functions** {Functions}");
        sb.AppendLine();
        sb.AppendLine($"_**FINAL ANSWER**_\n\n{Answer}");
        return sb.ToString();
    }
}