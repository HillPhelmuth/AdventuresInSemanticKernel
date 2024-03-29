using DocumentFormat.OpenXml;
using Microsoft.Extensions.Logging;

namespace SkPluginLibrary.Models;

public record TokenString
{
    public TokenString(string StringValue, double LogProb, int Token = 0)
    {
        this.Token = Token;
        this.LogProb = LogProb;
        this.StringValue = StringValue;
        NormalizedLogProbability = LogProb;
    }

    public string StringValue { get; set; }
    public List<TokenString> TopLogProbs { get; set; } = [];
    public double NormalizedLogProbability { get; set; }
    public int Token { get; init; }
    public double LogProb { get; init; }
   
}
public static class LoggerExtensions
{
    public static void LogInformation(this ILoggerFactory loggerFactory, string message, params object[] args)
    {
        loggerFactory.CreateLogger("Information").LogInformation(message, args);
    }
}