using Markdig;
using Microsoft.AspNetCore.Components;

namespace BlazorWithSematicKernel.Components.BookWriterComponents;

public partial class BookReader : ComponentBase
{
    private readonly MarkdownPipeline _markdownPipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
    [Parameter]
    public string Text { get; set; } = "";
    [Parameter]
    public Dictionary<string, string> ChapterTexts { get; set; } = [];
    private string AsHtml(string? text)
    {
        if (text == null) return "";
        var pipeline = _markdownPipeline;
        var result = Markdown.ToHtml(text, pipeline);
        return result;

    }
}