# AdventuresInMSAgentFramework

An interactive .NET 10 Blazor Server playground built with [Microsoft Agent Framework](https://learn.microsoft.com/agent-framework/overview/).
Agents use `ChatClientAgent`, `AgentSession`, `IChatClient`, and `AIFunction`; group conversations run real Agent Framework workflows.
The application no longer references Semantic Kernel packages.

## Run locally

Install the .NET 10 SDK. Open [AdventuresInMSAgentFramework.sln](./AdventuresInMSAgentFramework.sln), or use PowerShell:

```powershell
dotnet build .\AdventuresInMSAgentFramework.sln
dotnet test .\AgentFramework.Tests\AgentFramework.Tests.csproj
dotnet run --project .\BlazorWithMSAgentFramework\BlazorWithMSAgentFramework.csproj
```

Hard-refresh existing browser tabs after upgrading so cached JavaScript matches the migrated component assemblies.

The UI uses the Radzen Standard theme and responsive sidebar. Application styles load after the theme and also style the migrated native HTML controls; Radzen CSS alone does not style those controls.
Chat transcripts and user-interaction dialogs have their own themed component styles, including Markdown tables/code and narrow-screen layouts. Raw HTML in displayed Markdown is escaped rather than applied to the host page.
The sidebar exposes one entry per workspace: dynamic teams are part of **Agent workflows and dynamic teams**, clustering is part of **Memory and clustering**, and log probabilities are part of **Tokens and probabilities**. Older URLs remain compatibility aliases, not separate menu features.
Navigating between agent routes starts the selected page and a fresh conversation rather than retaining the previous route's agent. Restart a running app/debug session before rebuilding on Windows so its loaded component assemblies can be replaced.

The existing Auth0 host integration requires `Auth0:Domain` and `Auth0:ClientId`.
Keep real configuration in user secrets, environment variables, or your deployment's configuration, not committed files.
For example:

```powershell
dotnet user-secrets set --project .\BlazorWithMSAgentFramework "Auth0:Domain" "YOUR_TENANT.auth0.com"
dotnet user-secrets set --project .\BlazorWithMSAgentFramework "Auth0:ClientId" "YOUR_CLIENT_ID"
dotnet user-secrets set --project .\BlazorWithMSAgentFramework "OpenAI:ApiKey" "YOUR_API_KEY"
```

Model keys are not needed for deterministic tests, tokenization, arithmetic tools, or explicit local code execution.
Model, embedding, image, and speech requests require your own credentials and may incur charges.

## Providers and configuration

Only **OpenAI** and **OpenRouter** are active model providers. Configuration is scoped to the current Blazor circuit; one visitor's override does not mutate another visitor's credentials.
Use **Configure services** to apply a profile or save/load an encrypted browser override.
The existing `OpenAIConfig` and `OpenRouterConfig` keys are retained, including import of their older name/value-array payloads.
Changing profiles or an agent definition starts a new conversation on the next send.

Supported server settings:

| Key | Purpose |
|---|---|
| `CoreSettings:Service` | `OpenAI` or `OpenRouter` |
| `OpenAI:ApiKey`, `OpenRouter:ApiKey` | Provider credentials |
| `<provider>:ModelId` | Default chat model; older `Gpt4MiniModelId` remains readable |
| `<provider>:Endpoint` | Optional HTTPS-compatible endpoint |
| `<provider>:EmbeddingModelId`, `<provider>:EmbeddingDimensions` | Explicit embedding model and vector size |
| `Google:ApiKey`, `Google:SearchEngineId` | Google Programmable Search, independent of the model provider |
| `OpenAI:ImageModelId`, `OpenAI:SpeechModelId` | Optional image/speech model overrides |
| `AgentFramework:MemoryDirectory` | Writable directory for the new SQLite database |

Defaults are `gpt-4.1-mini` / `openai/gpt-4.1-mini`, `text-embedding-3-small` with 1536 dimensions, `gpt-image-1`, and `tts-1`.
Choose models that actually support the configured tools/log-probability capabilities. Unsupported operations and upstream failures are reported, not silently stripped or redirected to another provider.

## Features

- **Samples:** six original, editable examples for streaming, approval-required tools, sessions/reset, sequential workflows, host-supplied human input, and retrieval ranking. The exact displayed sources are compiled in tests. Run uses the existing Ace editor and Roslyn compiler.
- **Execute a tool:** inspect generated JSON schemas and retained YAML prompt templates; run native functions, arithmetic, Wikipedia, web search/read, D&D, weather, user clarification, or C#/Blazor tools.
- **Build an agent:** editable instructions, per-agent model, tool groups/exclusions, JSON import/export, streaming replies, tool progress, cancellation, and session reset.
- **Group workflows:** sequential, hub-and-spoke, and schema-constrained model-selected routing; editable or generated teams, primary participant, history visibility, turn/stop limits, human input, and approvals. Human questions and approval requests are separate interactions.
- **Web, Wikipedia, documentation, and D&D agents:** retain their routes with typed HTTP tools or retrieval from freshly indexed Agent Framework documentation. Web search requires its own Google configuration; retired Bing Search is not called.
- **Novel writer:** outline and chapter generation, Markdown download, and explicitly requested OpenAI cover-image/speech utilities.
- **C#/Blazor REPL:** console programs, stateful scripts, and the existing multi-file Razor compiler/preview. Compiler output is isolated by asynchronous execution context.
- **Memory and clustering:** text/Markdown/PDF ingestion, chunked embeddings, in-memory or fresh SQLite collections, exact cosine search with filters/top-N/threshold, HDBSCAN metrics and generated summaries, and an embedding projection with cosine relationship edges. Scanned PDFs require OCR beforehand.
- **Tokens:** measured chunk budgets/overlap, token IDs, native OpenAI logit bias for compatible GPT-4o/GPT-4.1 models, and explicitly enabled log-probability results. Token counts use the GPT-4o tokenizer, not an arbitrary provider's tokenizer.

Retained prompt tools bind explicit `{{$variable}}` inputs and declared defaults. Clock values are supplied in code; previous implicit recall expressions now require an explicit `context` input.
Historical YAML `execution_settings` are not model-provider configuration; new calls use the selected provider/model defaults.

## Safety and persistence

**This is a trusted-user development playground, not a code sandbox.**
Model-initiated C#/script/Blazor execution requires a server-correlated, single-use approval on the same conversation.
Rejecting or cancelling a pending approval never starts that action. A user-clicked Run is itself explicit authorization.
Cancellation cannot forcibly terminate arbitrary host code that has already started. Do not expose execution tools to untrusted users.

Public-page tools accept public HTTPS hosts, reject private destinations, disallow redirects, bind connections to validated addresses, and limit pages to 2 MB.
API keys are not placed in displayed sample source or tool-progress messages.

New SQLite data uses **`agent-framework-memory.db`**, a separate `af_memory` table, and encrypted-browser owner IDs.
Existing databases are not opened, imported, rewritten, or deleted. Changing embedding models/dimensions requires reindexing.
Browser encryption depends on the ASP.NET Core Data Protection keys; preserve those keys if overrides must survive deployments.
Conversation sessions, human waits, and approval waits are process/circuit-local, not durable workflow checkpoints.

## Migration and validation

The legacy Stepwise/Handlebars planners, arbitrary marketplace-manifest imports, unsupported model providers, obsolete experiments, and the old 67-example collection are retired.
Legacy agent JSON keeps logical tool-group/exclusion fields and numeric history values; removed tools produce actionable errors.
`Gpt35`/`Gpt4` legacy aliases use the configured modern default model.

[Runtime and component tests](./AgentFramework.Tests) cover sessions, approvals, cancellation, provider requests/options, tools/prompts, workflows/human input, saved configuration, memory persistence/isolation, chunk budgets, gallery compilation, and the multi-file Blazor REPL, without paid model calls. bUnit regressions also exercise route-to-route navigation, route casing, consolidated menus, dialog completion, current-definition downloads, chat subscriptions, streaming display state, and editor prerender/disposal.
Both deployment workflows build/test the renamed solution before publishing.
Live provider and external-service checks require separately configured credentials and authorization; deterministic tests do not claim to validate model quality or tenant authentication.

The GitHub repository, workspace directory, Azure resource names, and deployment-secret identifiers remain unchanged.
Existing Azure targets must support .NET 10 before deployment. No deployment is required to use the local migration.
