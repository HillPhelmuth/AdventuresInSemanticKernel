namespace SkPluginLibrary.Agents.Models.Events;

/// <summary>
/// Represents the event arguments for an agent request.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="AgentInputRequestEventArgs"/> class.
/// </remarks>
/// <param name="agent">The interactive conversable agent associated with the request.</param>
public class AgentInputRequestEventArgs(IInteractiveAgent agent) : EventArgs
{
    /// <summary>
    /// Gets the interactive conversable agent associated with the request.
    /// </summary>
    public IInteractiveAgent Agent { get; } = agent;
}