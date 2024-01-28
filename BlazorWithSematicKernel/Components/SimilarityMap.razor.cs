using Microsoft.AspNetCore.Components;

namespace BlazorWithSematicKernel.Components
{
    public partial class SimilarityMap : ComponentBase
    {
        [Parameter] public List<SimScore> SimilarityScores { get; set; } = [];
        [Parameter] public EventCallback HasRendered { get; set; }
        [Parameter] public string Model { get; set; } = TestConfiguration.OpenAI!.EmbeddingModelId;
        private ApexChartOptions<SimScore> _options = new()
        {
            Colors = ["#008FFB"],
            PlotOptions = new PlotOptions
            {
                Heatmap = new PlotOptionsHeatmap
                {
                    ColorScale = new PlotOptionsHeatmapColorScale
                    {
                        Ranges = _heatMapColorScale,

                    },
                    Distributed = true,

                }
            }
        };
        private ApexChart<SimScore> _apexChart;
        private bool _isReady;
        private static List<PlotOptionsHeatmapColorScaleRange> _heatMapColorScale = [];
        private static List<PlotOptionsHeatmapColorScaleRange> _heatmapColorScaleRanges003 = [
            new PlotOptionsHeatmapColorScaleRange { From = 0.01, To = 20.00, Name = "Extremely Dissimilar", Color = "#000000" },
            new PlotOptionsHeatmapColorScaleRange { From = 20.01, To = 40.00, Name = "Dissimilar", Color = "#cc9900" },
            new PlotOptionsHeatmapColorScaleRange { From = 40.01, To = 60.00, Name = "Somewhat Similar", Color = "#ff3333" },
            new PlotOptionsHeatmapColorScaleRange { From = 60.01, To = 80.00, Name = "Similar", Color = "#008000" },
            new PlotOptionsHeatmapColorScaleRange { From = 80.01, To = 100, Name = "Extremely Similar", Color = "#0000ff" },
        ];
        private static List<PlotOptionsHeatmapColorScaleRange> _heatMapColorRanges002 = [
            new PlotOptionsHeatmapColorScaleRange { From = 0.01, To = 60.00, Name = "Extremely Dissimilar", Color = "#000000" },
            new PlotOptionsHeatmapColorScaleRange { From = 60.01, To = 70.00, Name = "Dissimilar", Color = "#cc9900" },
            new PlotOptionsHeatmapColorScaleRange { From = 70.01, To = 80.00, Name = "Somewhat Similar", Color = "#ff3333" },
            new PlotOptionsHeatmapColorScaleRange { From = 80.01, To = 90.00, Name = "Similar", Color = "#008000" },
            new PlotOptionsHeatmapColorScaleRange { From = 90.01, To = 100, Name = "Extremely Similar", Color = "#0000ff" },
        ];
       
        protected override Task OnParametersSetAsync()
        {
            _heatMapColorScale = Model.Contains("002") ? _heatMapColorRanges002 : _heatmapColorScaleRanges003;
            _options = new ApexChartOptions<SimScore>
            {
                Colors = ["#008FFB"],
                PlotOptions = new PlotOptions
                {
                    Heatmap = new PlotOptionsHeatmap
                    {
                        ColorScale = new PlotOptionsHeatmapColorScale
                        {
                            Ranges = _heatMapColorScale,

                        },
                        Distributed = true,

                    }
                }
            };
            return base.OnParametersSetAsync();
        }
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                Console.WriteLine($"OnAfterRender - first render for {nameof(SimilarityMap)}.razor ");
                Console.WriteLine($"{SimilarityScores.Count} Similarity Scores");
                await Task.Delay(1000);
                _isReady = true;
                StateHasChanged();
            }
            await base.OnAfterRenderAsync(firstRender);
        }

        private async void HandleRender()
        {
            Console.WriteLine("HandleRender triggered from ApexChart in SimilarityMap");
            StateHasChanged();
            await HasRendered.InvokeAsync();
            await Task.Delay(1000);
            StateHasChanged();
        }
    }
}
