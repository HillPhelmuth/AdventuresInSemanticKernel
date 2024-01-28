using Microsoft.AspNetCore.Components;
using Microsoft.SemanticKernel.Memory;
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
        private List<string> _models = ["text-embedding-3-small", "text-embedding-3-large", "text-embedding-ada-002"];
        private class SearchForm
        {
            public string? Query { get; set; }
            public int Limit { get; set; } = 1;
            public double MinThreshold { get; set; } = 0.7d;
            public string Model { get; set; } = "text-embedding-3-small";
        }
        private SearchForm _searchForm = new();
        private List<TokenizedChunk> _tokenizedChunks = new();
        private Dictionary<int, (List<TokenString>, int)> _tokenizedChunksDict = new();
        private ChunkForm _chunkForm = new();
        private bool _isBusy;
        private int _tabIndex;
#pragma warning disable SKEXP0003
        private List<MemoryQueryResult> _memoryQueryResults = new();
#pragma warning restore SKEXP0003
        private void HandleToggle(TokenizedChunk tokenizedChunk)
        {
            var chunk = _tokenizedChunks.FirstOrDefault(x => x.ChunkNumber == tokenizedChunk.ChunkNumber);
            chunk.IsTokenized = !chunk.IsTokenized;
            Console.WriteLine($"Chunk {chunk.ChunkNumber} is tokenized: {chunk.IsTokenized}");
            StateHasChanged();
        }

        private string _input = "Write a 2000 word essay about the life of Abraham Lincoln";
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
            _tokenizedChunks = _tokenizedChunksDict.Select(x => new TokenizedChunk(x.Key, x.Value.Item1, x.Value.Item2)).ToList();
            StateHasChanged();
        }
        private string _searchBusyString = "Saving Chuncks...";
        private async void HandleTabIndexChanged(int index)
        {
            if (index == 1)
            {
                _isBusy = true;
                _searchBusyString = "Saving Chuncks...";
                StateHasChanged();
                await Task.Delay(1);
                await CoreKernelService.SaveChunks(_tokenizedChunks);
                _isBusy = false;

            }
            StateHasChanged();
        }
        private async void ReSave(string model)
        {
            _isBusy = true;
            _searchBusyString = $"Saving Chuncks ({model})...";
            StateHasChanged();
            await Task.Delay(1);
            await CoreKernelService.SaveChunks(_tokenizedChunks, model);
            _isBusy = false;
            StateHasChanged();
        }
        private async void Search(SearchForm search)
        {
            _isBusy = true;
            _searchBusyString = "Searching...";
            StateHasChanged();
            await Task.Delay(1);
            var results = await CoreKernelService.SearchInChunks(search.Query, search.Limit, search.MinThreshold);

            _memoryQueryResults = results;
            _isBusy = false;
            StateHasChanged();
        }
    }
}
