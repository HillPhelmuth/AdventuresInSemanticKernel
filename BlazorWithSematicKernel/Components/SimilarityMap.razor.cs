using Microsoft.AspNetCore.Components;

namespace BlazorWithSematicKernel.Components
{
    public partial class SimilarityMap : ComponentBase
    {
        [Parameter] public List<SimScore> SimilarityScores { get; set; } = [];
        [Parameter] public EventCallback HasRendered { get; set; }
        private ApexChartOptions<SimScore> _options = new()
        {
            Colors = ["#008FFB"],
            PlotOptions = new PlotOptions
            {
                Heatmap = new PlotOptionsHeatmap
                {
                    ColorScale = new PlotOptionsHeatmapColorScale
                    {
                        Ranges =
                        [
                            new PlotOptionsHeatmapColorScaleRange {From = 0.0, To = 0.6, Name = "Very Dissimilar", Color = "#FFA07A"},
                            new PlotOptionsHeatmapColorScaleRange {From = 0.6, To = 0.69, Name = "Dissimilar", Color = "#FA8072"},
                            new PlotOptionsHeatmapColorScaleRange {From = 0.69, To = 0.77, Name = "Very Slightly Similar", Color = "#F08080"},
                            new PlotOptionsHeatmapColorScaleRange {From = 0.77, To = 0.85, Name = "Somewhat Similar", Color = "#CD5C5C"},
                            new PlotOptionsHeatmapColorScaleRange {From = 0.85, To = 0.92, Name = "Similar", Color = "#DC143C"},
                            new PlotOptionsHeatmapColorScaleRange {From = 0.92, To = 0.98, Name = "Extremely Similar", Color = "#B22222"},
                            new PlotOptionsHeatmapColorScaleRange {From = 0.98, To = 1.0, Name = "Nearly Exact", Color = "#8B0000"}
                        ],

                    },
                    Distributed = true,

                }
            }
        };
        private ApexChart<SimScore> _apexChart;
        private bool _isReady;
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
