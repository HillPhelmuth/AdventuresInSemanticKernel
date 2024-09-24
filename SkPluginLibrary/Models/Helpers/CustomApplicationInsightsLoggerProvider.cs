using Microsoft.Extensions.Logging;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;

namespace SkPluginLibrary.Models.Helpers
{
    public class CustomApplicationInsightsLoggerProvider(TelemetryClient telemetryClient) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName)
        {
            return new CustomApplicationInsightsLogger(telemetryClient);
        }

        public void Dispose()
        {
            telemetryClient.Flush();
        }
    }
    public class CustomApplicationInsightsLogger(TelemetryClient telemetryClient) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            SeverityLevel severityLevel = logLevel.ToSeverityLevel();
            TraceTelemetry? traceTelemetry;
            if (severityLevel is SeverityLevel.Error or SeverityLevel.Critical)
            {
                traceTelemetry = new TraceTelemetry("App Exception", severityLevel);
                telemetryClient.Context.GlobalProperties["HResult"] = exception?.HResult.ToString();
                telemetryClient.Context.GlobalProperties["ErrorMessage"] = exception?.Message ?? "No message";
                telemetryClient.Context.GlobalProperties["StackTrace"] = exception?.StackTrace ?? string.Empty;
                telemetryClient.TrackException(exception);
                telemetryClient.TrackTrace(traceTelemetry);
            }
            else
            {
                traceTelemetry = new TraceTelemetry(formatter(state, exception), severityLevel);
                telemetryClient.TrackTrace(traceTelemetry);
            }
        }
    }

    public static class LogLevelExtensions
    {
        public static SeverityLevel ToSeverityLevel(this LogLevel logLevel)
        {
            return logLevel switch
            {
                LogLevel.Trace => SeverityLevel.Verbose,
                LogLevel.Debug => SeverityLevel.Verbose,
                LogLevel.Information => SeverityLevel.Information,
                LogLevel.Warning => SeverityLevel.Warning,
                LogLevel.Error => SeverityLevel.Error,
                LogLevel.Critical => SeverityLevel.Critical,
                _ => SeverityLevel.Verbose
            };
        }
    }
}
