using System.Text;

namespace AgentFrameworkLibrary.Services;

internal sealed class ConsoleCapture : IDisposable
{
    private static readonly AsyncLocal<TextWriter?> Current = new();
    private readonly TextWriter? _previous = Current.Value;
    private readonly StringWriter _buffer = new();

    static ConsoleCapture() => Console.SetOut(new RoutingWriter(Console.Out));
    public ConsoleCapture() => Current.Value = TextWriter.Synchronized(_buffer);
    public string Output => _buffer.ToString();
    public void Dispose() { Current.Value = _previous; _buffer.Dispose(); }

    private sealed class RoutingWriter(TextWriter fallback) : TextWriter
    {
        public override Encoding Encoding => fallback.Encoding;
        public override void Write(char value) => (Current.Value ?? fallback).Write(value);
        public override void Write(string? value) => (Current.Value ?? fallback).Write(value);
        public override void WriteLine(string? value) => (Current.Value ?? fallback).WriteLine(value);
    }
}
