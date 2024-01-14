using Microsoft.AspNetCore.Components;
using SkPluginLibrary.Abstractions;
using SkPluginLibrary.Models.Helpers;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace BlazorWithSematicKernel.Pages
{
    public partial class AddEmbeddings : ComponentBase
    {
        [Inject]
        private IMemoryConnectors CoreKernelService { get; set; } = default!;

        private readonly List<ContextItem> _contextItems = new();
        private bool _isBusy;
        private bool _isChartRendered = true;
        private MemoryStoreType _memoryStoreType = MemoryStoreType.None;
        private class MemoryStoreForm
        {
            public MemoryStoreType MemoryStoreType { get; set; }
        }
        private MemoryStoreForm _memoryStoreForm = new();
        private readonly Dictionary<MemoryStoreType, string> _memoryStoreTypes = typeof(MemoryStoreType).GetEnumsWithDescriptions<MemoryStoreType>();
        private int _currentStep;
        private async Task GetVectors()
        {
            _isBusy = true;
            StateHasChanged();
            await Task.Delay(1);
            var randos = await CoreKernelService.GenerateRandomSentances();
            if (_testForm.TestInputs.Count == 1)
            {
                _testForm.TestInputs.Clear();
            }
            //_testForm.CompareAllMap = true;

            _testForm.TestInputs.AddRange(randos.Select(x => new TestInput() { Input = RemoveNumberedIndicator(x) }));
            _isBusy = false;
            StateHasChanged();

        }
        public string RemoveNumberedIndicator(string input)
        {
            return OrderedListStartRegex().Replace(input, "");
        }
        private bool ValidateConfig(MemoryStoreType memoryStoreType)
        {
            return memoryStoreType switch
            {
                MemoryStoreType.Redis when string.IsNullOrEmpty(TestConfiguration.Redis?.Configuration) => false,
                MemoryStoreType.Redis => true,
                MemoryStoreType.Qdrant when string.IsNullOrEmpty(TestConfiguration.Qdrant?.Endpoint) => false,
                MemoryStoreType.Qdrant => true,
                MemoryStoreType.Weaviate when TestConfiguration.Weaviate?.IsValid() == false => false,
                MemoryStoreType.Weaviate => true,
                _ => true
            };
        }
        private void Submit(MemoryStoreForm memoryStoreForm)
        {
            _memoryStoreType = memoryStoreForm.MemoryStoreType;
            _currentStep++;
            StateHasChanged();
        }
        private class TestForm
        {
            [Required]
            public string? Input { get; set; }

            public List<TestInput> TestInputs { get; set; } = new() { new TestInput() { Input = "" } };
            public bool CompareAllMap { get; set; }
            public VisualType VisualType { get; set; }

            public void Reset()
            {
                Input = "";
                TestInputs.Clear();
                TestInputs.Add(new TestInput() { Input = "" });
                CompareAllMap = false;
                VisualType = VisualType.OneToManyGrid;

            }

        }

        private enum VisualType
        {
            [Description("Compare one to many. View as grid.")]
            OneToManyGrid,
            [Description("Compare many to many. View as heatmap.")]
            ManyToManyHeatMap
        }


        private bool _addInputs;
        private class TestInput
        {
            public string Input { get; set; }
        }
        private TestForm _testForm = new();

        private List<SimScore> _scores = new();

        private void Add()
        {
            _testForm.TestInputs.Add(new TestInput() { Input = "" });
            StateHasChanged();
        }

        private bool _isRenderedOnce;
        private async void HasRendered()
        {
            Console.WriteLine("HasRendered triggered from SimilarityMap in AddEmbeddings");
            if (_isRenderedOnce) return;
            _isRenderedOnce = true;
            _isChartRendered = false;
            StateHasChanged();
            await Task.Delay(100);
            _isChartRendered = true;
            StateHasChanged();

        }

        private void ResetAll()
        {
            _testForm.Reset();
            _scores.Clear();
            _memoryStoreType = MemoryStoreType.None;
            _currentStep = 0;
            StateHasChanged();
        }
        private async void Submit(TestForm testForm)
        {
            _isBusy = true;
            StateHasChanged();
            await Task.Delay(1);
            if (testForm.TestInputs.Count > 1)
            {

                var contextItems = testForm.TestInputs.Select(x => new ContextItem() { Prompt = x.Input }).ToList();
                if (testForm.VisualType == VisualType.ManyToManyHeatMap)
                    contextItems.Add(new ContextItem() { Prompt = testForm.Input });
                _contextItems.Clear();
                await foreach (var item in CoreKernelService.SaveBatchToMemory("testCollection", contextItems, _memoryStoreType, true))
                {
                    _contextItems.Add(item);
                }
            }

            switch (testForm.VisualType)
            {
                case VisualType.OneToManyGrid:
                    {
                        var scoredItems =
                            await CoreKernelService.SearchKernelMemory(testForm.Input, "testCollection", _contextItems.Count);
                        _scores = scoredItems.Select(x => new SimScore(x.Metadata.Text, x.Relevance)).ToList();
                        StateHasChanged();
                        break;
                    }
                case VisualType.ManyToManyHeatMap:
                    {
                        foreach (var item in _contextItems)
                        {
                            var scoredItems =
                                await CoreKernelService.SearchKernelMemory(item.Prompt!, "testCollection", _contextItems.Count + 1);
                            _scores.AddRange(scoredItems.Select(x => new SimScore(item.Prompt, x.Relevance) { ComparedTo = x.Metadata.Text }));
                        }
                        StateHasChanged();
                        break;
                    }
                default:
                    throw new ArgumentOutOfRangeException();
            }
            _isBusy = false;
            StateHasChanged();
            //await GetVectors();
            //await Compare();
        }

        [GeneratedRegex("^\\d+\\.\\s*")]
        private static partial Regex OrderedListStartRegex();
    }
}
