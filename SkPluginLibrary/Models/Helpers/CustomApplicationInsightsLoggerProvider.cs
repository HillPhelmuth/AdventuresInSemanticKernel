using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        public IDisposable BeginScope<TState>(TState state)
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            var traceTelemetry = new TraceTelemetry(formatter(state, exception), logLevel.ToSeverityLevel());
            telemetryClient.TrackTrace(traceTelemetry);
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
