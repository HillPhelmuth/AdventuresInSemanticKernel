# Adventures in Microsoft Agent Framework

An interactive Blazor demo of [Microsoft Agent Framework](https://learn.microsoft.com/en-us/agent-framework/) (MAF) for .NET. It started life as *Adventures in Semantic Kernel* and has been ported to MAF while keeping the same look and feel. Experiment with agents, tools, workflows, memory and tokenization, and see how the pieces fit together.

> The project and solution names still say "Semantic Kernel". Renaming is the last step of the migration.

## Model providers

Only **OpenAI** and **OpenRouter** are supported. Both are reached through the OpenAI client behind a Microsoft.Extensions.AI `IChatClient`.
Set `CoreSettings:Service` to `OpenAI` or `OpenRouter` and fill in the matching section (see `BlazorWithSematicKernel/appsettings.example.json`).

## What replaced what

| Semantic Kernel | Agent Framework version |
| --- | --- |
| Kernel + chat completion service | `IChatClient` (Microsoft.Extensions.AI) |
| Plugins and `[KernelFunction]` | Tool collections of `AIFunction` (`ToolPlugin` groups them for the UI) |
| Prompt (YAML) functions | The same YAML files, loaded as `AIFunction`s by `YamlPromptFunction` |
| OpenAPI plugins | `OpenApiToolFactory` builds `AIFunction`s from an OpenAPI document |
| `ChatCompletionAgent` / `AgentGroupChat` | `ChatClientAgent` and MAF group-chat workflows |
| Function invocation filters | Agent function-calling middleware (`AIAgentBuilder.Use`) |
| Stepwise / Handlebars planners | A step-by-step tool-calling agent and a plan-and-execute workflow |
| Memory connectors | A small vector store that reads and writes the existing SQLite memory databases (MAF has no equivalent) |

## Application features

### Samples
View, edit and run the Agent Framework samples in `SkPluginLibrary/Examples`: chat clients, agents, tools, streaming, structured output, middleware, agents as tools, sequential, concurrent, group chat and handoff workflows, human-in-the-loop tool approval, and OpenRouter models.

### Execute Function
Pick a tool group (native, prompt or OpenAPI) and run one of its functions.

### Build Agent
Build an agent from a persona and a set of tools.

### Build Agent Group Chat
Build a group chat from several agents (hub-and-spoke, sequential, or prompt-based speaker selection) running as an Agent Framework workflow.

### Build Planner
Select tools and run them with a step-by-step agent or a plan-and-execute workflow.

### Custom Examples
Web chat agent (Bing search plus scrape-and-summarize), Wikipedia chat agent, C# REPL agent, D&D story agent (tools plus the `AskUserPlugin` for human-in-the-loop), and a novel writer.

### Memory
Vector playground and HDBSCAN clustering over embeddings.

### Tokens
Text chunking, tokenization and logit-bias experiments.
