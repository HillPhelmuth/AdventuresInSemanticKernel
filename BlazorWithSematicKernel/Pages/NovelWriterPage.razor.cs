using System.Text.Json;
using BlazorWithSematicKernel.Services;
using Markdig;
using Microsoft.AspNetCore.Components;
using Radzen.Blazor;
using Radzen.Blazor.Rendering;
using SkPluginLibrary.Abstractions;
using SkPluginLibrary.Models.Helpers;
using static SkPluginLibrary.Models.Helpers.EnumHelpers;

namespace BlazorWithSematicKernel.Pages;

public partial class NovelWriterPage : ComponentBase
{
    [Inject]
    private ICustomNativePlugins CustomNativePlugins { get; set; } = default!;
    [Inject] private IJSRuntime JsRuntime { get; set; } = default!;
    //private AudioService AudioService => new(JsRuntime);
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
    private record OutlineChapter(string Title, string Text)
    {
        public string? FullText { get; set; }
        public bool ShowAudio { get; set; }
    }
    private List<OutlineChapter> _chapterOutlines = [];
    private NovelWriter _novelWriter = new();
    private NovelOutline _novelOutline = new();
    private readonly MarkdownPipeline _markdownPipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
    private static Dictionary<AIModel, string> AIModelDescriptions => GetEnumsWithDescriptions<AIModel>().ToDictionary(x => x.Key, y => y.Value);
    private CancellationTokenSource _cancellationTokenSource = new();
    private Dictionary<string, string> _chapterTexts = [];
    private string _currentChapterKey = "";
	private async void Submit(NovelWriter novelWriter)
    {
        _isBusy = true;
        _step = 1;
        StateHasChanged();
        _step = 2;
        StateHasChanged();
        var ctoken = _cancellationTokenSource.Token;
        await Task.Delay(1);
        await foreach (var token in CustomNativePlugins.WriteNovel(novelWriter.Outline, novelWriter.AIModel, ctoken))
        {
            if (token.StartsWith("[CHAPTER]"))
			{
				_currentChapterKey = token.Replace("[CHAPTER]","");
				_chapterTexts.Add(_currentChapterKey, "");
			}
			else
			{
				_chapterTexts[_currentChapterKey] += token;
			}
            //_text += token;
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
        var fileContent = FileHelper.GenerateHtmlFile(_chapterTexts);

        await JsRuntime.InvokeVoidAsync("downloadFile", $"{_novelOutline.Title}.html", fileContent);
    }

    protected override Task OnInitializedAsync()
    {
        CustomNativePlugins.AdditionalAgentText += HandleChapterOutline;
        CustomNativePlugins.StringWritten += HandleUpdate;
        CustomNativePlugins.TextToImageUrl += HandleImageUrl;
        return base.OnInitializedAsync();
    }
    private void HandleImageUrl(object? sender, string url)
    {
	    _chapterTexts[_currentChapterKey] += url;

		//_text += url;
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
    private int _chapterIndex;
    private void HandleUpdate(object? sender, string args)
    {
        if (_isCheat) return;
        _chapterOutlines[_chapterIndex].FullText = args;
        _chapterIndex++;
    }
    private static Dictionary<int, byte[]> _chapters = [];
    private bool _isCheat;
  

    private void Cheat()
    {
        _isCheat = true;
        _chapterOutlines = _chapterOutlines.Select(x => new OutlineChapter(x.Title, x.Text) { FullText = TempStory }).ToList();
    }
    

    private string AsHtml(string? text)
    {
        if (text == null) return "";
        var pipeline = _markdownPipeline;
        var result = Markdown.ToHtml(text, pipeline);
        return result;

    }
    private const string TempStory = 
        """
        # Test Story

        ## Chapter 1: The Journey Begins
        The sun rose over the bustling port of New York City, casting a golden glow over the water as the team prepared for their expedition. Dr. Amelia Clarke stood at the helm of the "Sea Serpent," her eyes gleaming with excitement and determination. She was a woman of average height, with fiery red hair and a captivating presence that commanded attention. Her passion for archaeology and her relentless pursuit of knowledge had led her to this moment, embarking on a journey to a remote island filled with ancient ruins and hidden treasures.
        
        Beside her, Captain Jack Roberts, a seasoned sailor with a rugged appearance and a calm demeanor, was busy giving final instructions to his crew. He had a deep respect for the sea and its unpredictable nature, and he knew that this expedition would test their skills and endurance. Yet, he shared Dr. Clarke's enthusiasm for the adventure that lay ahead.
        
        Professor Samuel Brown, a distinguished historian with a keen intellect and a knack for deciphering ancient languages, was engrossed in his research, his spectacles perched precariously on his nose. His contributions to the field of history were numerous, and he was eager to add this expedition to his list of accomplishments.
        
        Lara Evans, a fearless journalist with an adventurous spirit, was documenting the journey. Her camera hung around her neck, ready to capture every moment of their expedition. She was determined to share their story with the world, to inspire others with their courage and their quest for knowledge.
        
       
        """;
}