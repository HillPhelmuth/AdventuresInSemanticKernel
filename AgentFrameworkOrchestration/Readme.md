## Microsoft Agent Framework orchestration

[Runtime](./Runtime) contains the application's provider settings, persisted agent definition, agent factory, session owner, and group-workflow adapter.

- `AgentConversation` owns a real `AgentSession`, rejects overlapping runs, and correlates single-use approvals before resuming the same session.
- `GroupWorkflow` uses `AgentWorkflowBuilder` and `InProcessExecution`, with sequential, hub-and-spoke, and validated schema-constrained selection.
- Turn caps and assistant stop text are deterministic; prompt selection cannot immediately repeat the current speaker.
- User clarification/human-participant callbacks are distinct from approval-required tools. The Blazor host supplies cancellable human input; waits are not durable checkpoints.
- Agent JSON preserves logical tool groups, exclusions, stable identity, primary/human flags, model overrides, and history modes. Legacy numeric history values remain supported.

See the [application setup and limitations](../README.md) and [deterministic regression tests](../AgentFramework.Tests).