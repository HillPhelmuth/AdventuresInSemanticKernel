## Custom Agent Orchestration using Microsoft Agent Framework

A custom group-chat style orchestration built on Agent Framework agents (`ChatClientAgent`) and Microsoft.Extensions.AI. It keeps its own agent classes (`ChatAgent`, `InteractiveAgent`, `UserProxyAgent`), transition graphs and a dynamic agent chat service, and uses MAF for the agents, tools and function-calling middleware.

For the framework's built-in orchestrations (sequential, concurrent, group chat, handoff) see the samples in `SkPluginLibrary/Examples` and the [Agent Framework docs](https://learn.microsoft.com/en-us/agent-framework/).
