using Markdig;
using Microsoft.AspNetCore.Components;
using SkPluginLibrary.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using SkPluginLibrary.Models.Helpers;
using static SkPluginLibrary.Models.Helpers.EnumHelpers;
using System.Text.Json;
using Radzen.Blazor.Rendering;
using Radzen.Blazor;

namespace BlazorWithSematicKernel.Pages;

public partial class NovelWriterPage : ComponentBase
{
    [Inject]
    private ICustomNativePlugins CustomNativePlugins { get; set; } = default!;
    [Inject] private IJSRuntime JsRuntime { get; set; } = default!;
	private bool _isBusy;
    private string _text = "";
    private int _step;
    private bool _showOutline;
    RadzenButton _button;
    Popup _popup;
    private class NovelWriter
    {
        public string Outline { get; set; } = "";
        public AIModel AIModel { get; set; }
       
    }
    private class NovelIdea
    {
        public NovelGenre NovelGenre { get; set; }
    }
    private NovelIdea _novelIdea = new();
    private List<NovelGenre> _genres = Enum.GetValues<NovelGenre>().ToList();
    private record OutlineChapter(string Title, string Text);
    private List<OutlineChapter> _chapterOutlines = [];
    private NovelWriter _novelWriter = new();
    private NovelOutline _novelOutline = new();
    private readonly MarkdownPipeline _markdownPipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
    private static Dictionary<AIModel, string> AIModelDescriptions => GetEnumsWithDescriptions<AIModel>().ToDictionary(x => x.Key, y => y.Value);
    private CancellationTokenSource _cancellationTokenSource = new();
	private async void Submit(NovelWriter novelWriter)
    {
        _isBusy = true;
        _step = 1;
        StateHasChanged();
        _step = 2;
        StateHasChanged();
		
        await Task.Delay(1);
        await foreach (var token in CustomNativePlugins.WriteNovel(novelWriter.Outline, novelWriter.AIModel))
        {
            _text += token;
            await InvokeAsync(StateHasChanged);
        }
        _isBusy = false;
        StateHasChanged();
	}
    private async void SubmitIdea(NovelIdea novelIdea)
	{
		_isBusy = true;
		
		StateHasChanged();

		await _popup.CloseAsync();
        _novelOutline = await CustomNativePlugins.GenerateNovelIdea(novelIdea.NovelGenre);

		_isBusy = false;
		StateHasChanged();
	}
    private void Cancel()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource = new CancellationTokenSource();
    }
    private async void SubmitOutline(NovelOutline novelOutline)
    {
	    _isBusy = true;
	    StateHasChanged();
	    await Task.Delay(1);
	    _novelWriter.Outline = await CustomNativePlugins.CreateNovelOutline(novelOutline.Theme, novelOutline.Characters, novelOutline.PlotEvents, novelOutline.Title, novelOutline.ChapterCount, novelOutline.AIModel);
	    _isBusy = false;
        _showOutline = true;
        StateHasChanged();
	}
    private async Task DownloadNovelToFile()
    {
	    if (string.IsNullOrEmpty(_text)) return;
	    var fileContent = FileHelper.GenerateTextFile(_text);

	    await JsRuntime.InvokeVoidAsync("downloadFile", $"{_novelOutline.Title}.txt", fileContent);
    }
    private async Task DownloadOutlineToFile()
    {
	    if (string.IsNullOrEmpty(_novelWriter.Outline)) return;
	    var fileContent = FileHelper.GenerateTextFile(_novelWriter.Outline);

	    await JsRuntime.InvokeVoidAsync("downloadFile", $"{_novelOutline.Title}_Outline.txt", fileContent);
    }
	protected override Task OnInitializedAsync()
    {
        CustomNativePlugins.AdditionalAgentText += HandleChapterOutline;
        return base.OnInitializedAsync();
    }
    private void HandleChapterOutline(string text) 
    {
        if (text.StartsWith("Plan"))
        {
	        _text += $"{text}<br/>";
            StateHasChanged();
            return;
        }
        var chapters = JsonSerializer.Deserialize<List<string>>(text);
        foreach (var chapter in chapters)
        {
            var title = chapter.Split("\n")[0];
            _chapterOutlines.Add(new OutlineChapter(title, chapter));
        }
        StateHasChanged();
    }
    private string AsHtml(string? text)
    {
        if (text == null) return "";
        var pipeline = _markdownPipeline;
        var result = Markdown.ToHtml(text, pipeline);
        return result;

    }
}