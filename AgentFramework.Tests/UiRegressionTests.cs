using System.Security.Claims;
using System.Text.Json;
using AgentFrameworkLibrary;
using AgentToolComponents;
using AgentToolComponents.Components;
using AgentToolComponents.Models;
using BlazorAceEditor;
using BlazorAceEditor.Extensions;
using BlazorWithMSAgentFramework;
using BlazorWithMSAgentFramework.Workspace;
using Bunit;
using ChatComponents;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Radzen;
using Xunit;

namespace AgentFramework.Tests;

public sealed class UiRegressionTests : BunitContext
{
    public UiRegressionTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupModule("./_content/ChatComponents/appJsInterop.js").Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OpenAI:ApiKey"] = "ui-test-not-a-secret", ["OpenAI:Endpoint"] = "https://provider.invalid/v1"
        }).Build());
        Services.AddHttpClient("public-web");
        Services.AddAgentFrameworkServices();
        Services.AddChat();
        Services.AddRadzenComponents();
        Services.AddBlazorAceEditor();
        Services.AddAskUserService();
        Services.AddAuthorizationCore();
        Services.AddCascadingAuthenticationState();
        Services.AddSingleton<AuthenticationStateProvider, AnonymousAuthentication>();
        Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        Services.AddScoped<ProtectedLocalStorage>();
    }

    [Fact]
    public void NavigationBetweenSharedAgentRoutesChangesTheActualPage()
    {
        var navigation = Services.GetRequiredService<NavigationManager>();
        var cut = Render<Routes>();
        foreach (var (path, title) in new[]
        {
            ("/simpleAgent", "Build an agent"), ("/web", "Web agent"), ("/wikiChat", "Wikipedia agent"),
            ("/docs", "Agent Framework documentation"), ("/repl", "C# / Blazor REPL"),
            ("/novel", "Novel writer"), ("/dnd", "D&D story agent"), ("/simpleAgent", "Build an agent")
        })
        {
            navigation.NavigateTo(path);
            cut.WaitForAssertion(() => Assert.Equal(title, cut.Find("main h1").TextContent));
        }
    }

    [Fact]
    public void NavigationDoesNotExposeDuplicateCompatibilityPagesOrDuplicateRadzenHosts()
    {
        var cut = Render<Routes>();
        var paths = cut.FindAll("nav[aria-label='Main navigation'] a").Select(a => a.GetAttribute("href")).ToArray();
        Assert.DoesNotContain("/autoSequentialChatPage", paths);
        Assert.DoesNotContain("/cluster", paths);
        Assert.DoesNotContain("/logProbs", paths);
        Assert.DoesNotContain("/planBuilder", paths);
        Assert.Equal(paths.Length, paths.Distinct().Count());
        Assert.Single(cut.FindAll(".rz-notification"));
    }

    [Theory]
    [InlineData("/WIKICHAT", "Wikipedia agent", "WikiChatPlugin")]
    [InlineData("/RePl", "C# / Blazor REPL", "CodeExecutorPlugin")]
    [InlineData("/DND", "D&D story agent", "DndPlugin")]
    public void RouteCasingDoesNotChangeTheSelectedAgentMode(string path, string title, string tool)
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo(path);
        var cut = Render<Routes>();
        Assert.Equal(title, cut.Find("main h1").TextContent);
        var label = cut.FindAll("label").Single(label => label.TextContent.Trim() == tool);
        Assert.True(label.QuerySelector("input")!.HasAttribute("checked"));
    }

    [Fact]
    public void DefinitionDownloadIncludesEditsBeforeApplyingOrSending()
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo("/simpleAgent");
        var cut = Render<AgentChat>();
        cut.Find("input.form-control").Change("MathApiPlugin_Multiply");
        cut.FindAll("button").Single(button => button.TextContent == "Download definition").Click();
        var invocation = Assert.Single(JSInterop.Invocations["downloadFile"]);
        using var json = JsonDocument.Parse((string)invocation.Arguments[1]!);
        Assert.Equal("MathApiPlugin_Multiply", Assert.Single(json.RootElement.GetProperty("ExcludedFunctionNames").EnumerateArray()).GetString());
    }

    [Fact]
    public async Task AnswerSubmissionWorksWithClickRatherThanMouseUpOnly()
    {
        var service = Services.GetRequiredService<AskUserService>();
        var cut = Render<AskUser>();
        Task<AskUserResults?>? pending = null;
        await cut.InvokeAsync(() => { pending = service.OpenAsync<SimpleInput>(); });
        cut.Find("textarea").Change("Keyboard answer");
        cut.Find("button.primary-button").Click();
        var result = await pending!.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal("Keyboard answer", result!.Parameters.Get<string>("Value"));
    }

    [Fact]
    public async Task DoneActuallyCompletesTheOutputInteraction()
    {
        var service = Services.GetRequiredService<AskUserService>();
        var cut = Render<AskUser>();
        Task<AskUserResults?>? pending = null;
        await cut.InvokeAsync(() => { pending = service.OpenAsync<SimpleOutput>(); });
        cut.Find("button.info-button").Click();
        Assert.True((await pending!.WaitAsync(TimeSpan.FromSeconds(2)))!.IsSuccess);
        Assert.Empty(cut.FindAll("[role='dialog']"));
    }

    [Fact]
    public async Task ClosingAQuestionAlsoCompletesTheAwaitingTool()
    {
        var service = Services.GetRequiredService<AskUserService>();
        var cut = Render<AskUser>();
        Task<AskUserResults?>? pending = null;
        await cut.InvokeAsync(() => { pending = service.OpenAsync<SimpleInput>(options: new() { ShowCloseButton = true }); });
        cut.Find("button[aria-label='Close dialog']").Click();
        Assert.Null(await pending!.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.Empty(cut.FindAll("[role='dialog']"));
    }

    [Fact]
    public void MarkdownMessagesUseThemedClassesAndDoNotInjectPageStyles()
    {
        var cut = Render<MessageView>(parameters => parameters.Add(p => p.Message,
            Message.AssistantMessage("## Reply\n\n`code`\n\n<style>body{display:none}</style>", 1)));
        Assert.Single(cut.FindAll(".chat-message.assistant"));
        Assert.Single(cut.FindAll(".chat-message-header time"));
        Assert.Single(cut.FindAll("h2"));
        Assert.Single(cut.FindAll("code"));
        Assert.Empty(cut.FindAll("style"));
        Assert.DoesNotContain("color:white", cut.Markup);
    }

    [Fact]
    public async Task RerenderingChatDoesNotMultiplyStateSubscriptions()
    {
        var cut = Render<ChatView>();
        cut.Render(parameters => parameters.Add(p => p.Height, "40vh"));
        await cut.InvokeAsync(() => cut.Instance.ChatState.AddUserMessage("One event"));
        cut.WaitForAssertion(() => Assert.Single(JSInterop.Invocations["scrollDown"]));
    }

    [Fact]
    public async Task ServerRenderingAndDisposingTheEditorDoesNotInvokeJavascript()
    {
        await using (var scope = Services.CreateAsyncScope())
        {
            await using var renderer = new HtmlRenderer(scope.ServiceProvider, scope.ServiceProvider.GetRequiredService<ILoggerFactory>());
            await renderer.Dispatcher.InvokeAsync(() => renderer.RenderComponentAsync<AceEditor>());
        }
        Assert.Empty(JSInterop.Invocations);
    }

    [Fact]
    public void CompletingStreamingStateDoesNotAlterTheAccumulatedAnswer()
    {
        var state = new ChatState();
        state.AddAssistantMessage("", isStreaming: true);
        state.UpdateAssistantMessage("Hello");
        state.UpdateAssistantMessage(" world");
        state.CompleteAssistantMessage();
        Assert.Equal("Hello world", Assert.Single(state.ChatMessages).Content);
        Assert.False(state.ChatMessages[0].IsActiveStreaming);
    }

    private sealed class AnonymousAuthentication : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));
    }
}
