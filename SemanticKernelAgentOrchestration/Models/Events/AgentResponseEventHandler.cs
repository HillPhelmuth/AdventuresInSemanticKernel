namespace SemanticKernelAgentOrchestration.Models.Events;

public delegate void AgentResponseEventHandler(object? sender, AgentResponseArgs e);
public delegate void AgentStreamingResponseEventHandler(object? sender, AgentStreamingResponseArgs e);