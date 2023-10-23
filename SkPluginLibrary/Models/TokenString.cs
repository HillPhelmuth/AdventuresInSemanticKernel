using Microsoft.Extensions.Logging;

namespace SkPluginLibrary.Models;

public record TokenString(int Token, string StringValue);
public static class LoggerExtensions
{
    public static void LogInformation(this ILoggerFactory loggerFactory, string message, params object[] args)
    {
        loggerFactory.CreateLogger("Information").LogInformation(message, args);
    }
}