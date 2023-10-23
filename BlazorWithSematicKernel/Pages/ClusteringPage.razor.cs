using Microsoft.AspNetCore.Components;
using SkPluginLibrary.Abstractions;
using SkPluginLibrary.Models.Helpers;
using SkPluginLibrary.Services;

namespace BlazorWithSematicKernel.Pages
{
    public partial class ClusteringPage : ComponentBase
    {
        [Inject]
        private IMemoryConnectors CoreKernelService { get; set; } = default!;
        [Inject]
        private NotificationService NotificationService { get; set; } = default!;
        private List<MemoryResult> _memoryResults = new();
        private string _outputText = "";

        private class ClusterDisplay
        {
            public ClusterDisplay(int cluserLabel, string title, List<MemoryResult> memoryResults)
            {
                CluserLabel = cluserLabel;
                Title = title;
                MemoryResults = memoryResults;

            }

            public int CluserLabel { get; set; }
            public string Title { get; set; }
            public string? Tags { get; set; }
            public List<MemoryResult> MemoryResults { get; set; }
        }

        private List<ClusterDisplay> _clusters = new();

        private readonly Dictionary<DistanceFunction, string> _distanceFunctionDescriptions = typeof(DistanceFunction).GetEnumsWithDescriptions<DistanceFunction>();
        private class ClusterForm
        {
            public int MinPoints { get; set; } = 3;
            public int MinCluster { get; set; } = 3;
            public int ItemCount { get; set; } = 100;
            public DistanceFunction DistanceFunction { get; set; } = DistanceFunction.CosineSimilarity;
        }

        private ClusterForm _clusterForm = new();
        private bool _isBusy;
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                //await RunCluster();
            }
            await base.OnAfterRenderAsync(firstRender);
        }

        private async void Submit(ClusterForm form)
        {
            _isBusy = true;
            StateHasChanged();
            await Task.Delay(1);
            await RunCluster(form);
            _isBusy = false;
            StateHasChanged();
        }
        private async Task RunCluster(ClusterForm form)
        {
            _memoryResults = await CoreKernelService.GetItemClustersFromCollection(form.ItemCount, "*", form.MinPoints, form.MinCluster, form.DistanceFunction);
            var memoryGroups = _memoryResults.GroupBy(x => x.Cluster).Select(g =>
                new ClusterDisplay(g.Key, g.FirstOrDefault()?.ClusterTitle ?? "no title", g.ToList()) { Tags = g.FirstOrDefault()?.TagString });
            _clusters = memoryGroups.ToList();
            StateHasChanged();
        }

        private void ShowText(MemoryResult result)
        {
            _outputText =
                $"Cluster: {result.ClusterTitle}\nArticle: {result.Title}\n------------------------------------\n{result.Text}\n------------------------------------\n{result.TagString}";
            StateHasChanged();
        }

        private class FileUploadForm
        {
            public string? FileName { get; set; }
            public long? FileSize { get; set; }
            public string? FileBase64 { get; set; }
            public const int MaxFileSize = int.MaxValue;
        }

        private FileUploadForm _fileUploadForm = new();


        private async void HandleFile(FileUploadForm fileUploadForm)
        {
            _isBusy = true;
            StateHasChanged();
            await Task.Delay(1);
            var file = Convert.FromBase64String(fileUploadForm.FileBase64!.ExtractBase64FromDataUrl());
            await CoreKernelService.ChunkAndSaveFileCluster(file, $"File: {fileUploadForm.FileName}");
            _isBusy = false;
            StateHasChanged();
        }
       
        private void HandleError(UploadErrorEventArgs args)
        {
            NotificationService.Notify(NotificationSeverity.Error, "Upload Error", args.Message);
            Console.WriteLine($"Error: {args.Message}\n");
        }
    }
}
