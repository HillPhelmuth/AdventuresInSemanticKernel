using Markdig;
using Microsoft.AspNetCore.Components;
using SkPluginLibrary.Abstractions;
using SkPluginLibrary.Models.Helpers;
using System.Text.Json;

namespace BlazorWithSematicKernel.Pages
{
    public partial class DndStoryPlannerExample : ComponentBase
    {
        [Inject] private ICustomCombinations CoreKernelService { get; set; } = default!;
        [Inject] private ICoreKernelExecution CoreKernelExecution { get; set; } = default!;
        [Inject] private IJSRuntime JsRuntime { get; set; } = default!;

        private readonly List<string> _classes =
        [
            "barbarian",
            "bard",
            "cleric",
            "druid",
            "fighter",
            "monk",
            "paladin",
            "ranger",
            "rogue",
            "sorcerer",
            "warlock",
            "wizard"
        ];

        private readonly List<string> _alignments =
        [
            "chaotic-evil",
            "chaotic-good",
            "chaotic-neutral",
            "lawful-evil",
            "lawful-good",
            "lawful-neutral",
            "neutral",
            "neutral-evil",
            "neutral-good"
        ];

        private readonly List<string> _races =
        [
            "dragonborn",
            "dwarf",
            "elf",
            "gnome",
            "half-elf",
            "half-orc",
            "halfling",
            "human",
            "tiefling"
        ];

        private class DndPlanForm
        {
            public string? Input { get; set; }
            public string? Race { get; set; }
            public string? Class { get; set; }
            public string? Alignment { get; set; }
            public bool UseStepwisePlanner { get; set; }
        }

        private readonly DndPlanForm _dndPlanForm = new();
        private string? _sequentialOutput;
        private bool _isBusy;
        private CancellationTokenSource _cancellationSource = new();
        private void Cancel() => _cancellationSource.Cancel();
        private async void Submit(DndPlanForm dndPlan)
        {
            if (string.IsNullOrEmpty(dndPlan?.Input)) return;

            _isBusy = true;
            StateHasChanged();
            await Task.Delay(1);
            var token = _cancellationSource.Token;
            _sequentialOutput = await CoreKernelService.FunctionCallStepwiseDndApi(dndPlan.Input, (dndPlan.Race, dndPlan.Class, dndPlan.Alignment), token);


            _isBusy = false;
            StateHasChanged();

        }
        private string _planString = "";
        private List<SimpleChatMessage> _chatMessages = [];
        
        protected override Task OnInitializedAsync()
        {
            CoreKernelExecution.YieldAdditionalText += (text) =>
            {
                var message = JsonSerializer.Deserialize<SimpleChatMessage>(text);
                _chatMessages.Add(message!);
                StateHasChanged();
            };
            CoreKernelExecution.DndPlannerFunctionHook += HandleMessage;
            return base.OnInitializedAsync();
        }
        private async Task DownloadToFile()
        {
            if (string.IsNullOrEmpty(_sequentialOutput)) return;
            var fileContent = FileHelper.GenerateTextFile(_sequentialOutput);

            await JsRuntime.InvokeVoidAsync("downloadFile", "DndCharacterStory.txt", fileContent);
        }
        private string MarkdownAsHtml(string? text)
        {
            if (text == null) return "";
            var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
            var result = Markdown.ToHtml(text, pipeline);
            return result;

        }
        private void HandleMessage(SimpleChatMessage simpleChatMessage)
        {
            _chatMessages.Add(simpleChatMessage);
            StateHasChanged();
        }
    }
}
