using Markdig;
using Microsoft.AspNetCore.Components;

namespace BlazorWithSematicKernel.Components.ModalDialogComponents;

public partial class ConvoSummary : ComponentBase
{
    private ChatView? _chatView;
    [Parameter]
    public string? ConversationSummary { get; set; }
    [Inject]
    private DialogService DialogService { get; set; } = default!;
    protected override Task OnParametersSetAsync()
    {
        _chatView?.ChatState.AddAssistantMessage(ConversationSummary ?? "");
        return base.OnParametersSetAsync();
    }
    private void Close()
    {
        DialogService.Close();
    }
    private string AsHtml(string? text)
    {
        if (text == null) return "";
        var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
        var result = Markdown.ToHtml(text, pipeline);
        return result;

    }
}