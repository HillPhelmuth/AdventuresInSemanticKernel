using AgentFrameworkOrchestration;
using Microsoft.Agents.AI;

namespace AgentFrameworkLibrary;

public static class SampleContext
{
    private static readonly AsyncLocal<ModelSettings?> Settings = new();
    public static ModelSettings Current => Settings.Value ?? throw new InvalidOperationException("Run the sample from the configured gallery.");
    public static IDisposable Enter(ModelSettings settings)
    {
        var previous = Settings.Value;
        Settings.Value = settings;
        return new Scope(() => Settings.Value = previous);
    }
    private sealed class Scope(Action exit) : IDisposable { public void Dispose() => exit(); }
}
