## Blazor Agent Tools
An RCL that exposes Blazor UI interactions as Agent Framework tools to allow LLMs to interact with a user interface.

### Getting Started

1. Add the following to your `Program.cs` file in your Blazor project:
```csharp
builder.Services.AddAskUserService();
```

2. Add the following to the bottom your App.razor (.net 7 or lower) or Routes.razor (.net 8):
```csharp
<SkPluginComponents.AskUser />
```

3. Inject `AskUserService` into any class or component in which you build agents. `AskUserPlugin.GetTools()` returns `AIFunction`s that any Agent Framework agent (or `IChatClient` with function invocation) can call.
```
@using SkPluginComponents.Models
@using SkPluginComponents
@inject AskUserService AskUserService
<button @onclick=Ask>Ask the agent</button>
<p>
    Answer: @_answer
</p>
@code {
    private string? _answer;
    private async Task Ask()
    {
        var tools = new AskUserPlugin(AskUserService).GetTools();
        AIAgent agent = new ChatClientAgent(chatClient, new ChatClientAgentOptions
        {
            ChatOptions = new ChatOptions { Instructions = "Ask the user for their name, then greet them.", Tools = tools }
        });
        _answer = (await agent.RunAsync("Start")).Text;
    }
}

```
When the agent calls the tool, a modal displays the question. The input type chosen by the agent determines the input component, and the user's response is returned to the agent as a string.
