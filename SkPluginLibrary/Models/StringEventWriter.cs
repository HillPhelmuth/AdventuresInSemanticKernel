using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text;

namespace SkPluginLibrary.Models
{
    public class StringEventWriter : StringWriter
    {
        public event EventHandler<string?>? StringWritten;
        public override void WriteLine(string? value)
        {
            StringWritten?.Invoke(this, value);
            base.WriteLine(value);
        }

        public override void WriteLine(StringBuilder? stringBuilder)
        {
            StringWritten?.Invoke(this, stringBuilder?.ToString());
            base.WriteLine(stringBuilder);
        }
        public override void WriteLine(object? value)
        {
            StringWritten?.Invoke(this, value?.ToString());
            base.WriteLine(value);
        }

        public override void WriteLine(decimal value)
        {
            StringWritten?.Invoke(this, value.ToString(CultureInfo.CurrentCulture));
            base.WriteLine(value);
        }
    }
    public class StringEventWriterLogger : ILogger
    {
        private readonly StringEventWriter _stringEventWriter;

        public StringEventWriterLogger(StringEventWriter stringEventWriter)
        {
            _stringEventWriter = stringEventWriter;
        }

#pragma warning disable CS8633 // Nullability in constraints for type parameter doesn't match the constraints for type parameter in implicitly implemented interface method'.
        public IDisposable? BeginScope<TState>(TState state) => default;
#pragma warning restore CS8633 // Nullability in constraints for type parameter doesn't match the constraints for type parameter in implicitly implemented interface method'.

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception, string> formatter)
        {
            _stringEventWriter.WriteLine(formatter(state, exception!));
        }
    }

    public class StringEventWriterLoggerProvider : ILoggerProvider
    {
        private readonly StringEventWriter _stringEventWriter;

        public StringEventWriterLoggerProvider(StringEventWriter stringEventWriter)
        {
            _stringEventWriter = stringEventWriter;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new StringEventWriterLogger(_stringEventWriter);
        }

        public void Dispose() { }
    }

    //public static class LoggerExtensions
    //{
    //    public static void LogInformation(this ILoggerFactory loggerFactory, string message, params object[] args)
    //    {
    //        loggerFactory.CreateLogger("Information").LogInformation(message, args);
    //    }
    //}
}
