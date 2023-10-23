using Microsoft.AspNetCore.Components;
using SkPluginLibrary.Abstractions;

namespace BlazorWithSematicKernel.Pages
{
    public partial class TextAndTokenization : ComponentBase
    {
        [Inject]
        private TooltipService TooltipService { get; set; } = default!;
        [Inject]
        private ITokenization CoreKernelService { get; set; } = default!;
        private class ChunkForm
        {
            public int LineMax { get; set; } = 50;
            public int ChunkMax { get; set; } = 250;
            public int Overlap { get; set; } = 25;
            public string Text { get; set; } = string.Empty;

        }

        private class TokenizedChunk
        {
            public int ChunkNumber { get; set; }
            public List<TokenString> TokenStrings { get; set; } = new();
            public string Text => string.Join("", TokenStrings.Select(x => x.StringValue)).Replace("&nbsp;", " ");
            public int TokenCount { get; set; }

            public bool IsTokenized { get; set; }
        }

        private List<TokenizedChunk> _tokenizedChunks = new();
        private Dictionary<int, (List<TokenString>, int)> _tokenizedChunksDict = new();
        private ChunkForm _chunkForm = new();
        private bool _isBusy;

        private void HandleToggle(TokenizedChunk tokenizedChunk)
        {
            var chunk = _tokenizedChunks.FirstOrDefault(x => x.ChunkNumber == tokenizedChunk.ChunkNumber);
            chunk.IsTokenized = !chunk.IsTokenized;
            Console.WriteLine($"Chunk {chunk.ChunkNumber} is tokenized: {chunk.IsTokenized}");
            StateHasChanged();
        }

        private string _input = "Write a 1200 word essay about the life of Abraham Lincoln";
        private async Task GenerateText()
        {
            _isBusy = true;
            StateHasChanged();
            await Task.Delay(1);
            await foreach (var token in CoreKernelService.GenerateLongText(_input))
            {
                _chunkForm.Text += token;
                StateHasChanged();
            }
            _isBusy = false;
            StateHasChanged();
        }
        private void Submit(ChunkForm chunkForm)
        {
            _tokenizedChunksDict = CoreKernelService.ChunkAndTokenize(chunkForm.Text, chunkForm.LineMax, chunkForm.ChunkMax, chunkForm.Overlap);
            _tokenizedChunks = _tokenizedChunksDict.Select(x => new TokenizedChunk { ChunkNumber = x.Key, TokenStrings = x.Value.Item1, TokenCount = x.Value.Item2 }).ToList();
            StateHasChanged();
        }
    }
}
