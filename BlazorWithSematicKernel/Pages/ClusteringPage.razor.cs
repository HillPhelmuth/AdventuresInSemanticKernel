using Markdig;
using Microsoft.AspNetCore.Components;
using SkPluginLibrary.Abstractions;
using SkPluginLibrary.Models.Helpers;
using SkPluginLibrary.Services;

namespace BlazorWithSematicKernel.Pages
{
    public partial class ClusteringPage : ComponentBase
    {
        private const string FileuploadCollection = "fileupload";

        [Inject]
        private IMemoryConnectors CoreKernelService { get; set; } = default!;
        [Inject]
        private NotificationService NotificationService { get; set; } = default!;
        private List<MemoryResult> _memoryResults = new();
        private string _outputText = "";
        private string _busyText = "Clustering...";
        private string _fileNameHandled = "";

        private class ClusterDisplay(int cluserLabel, string title, List<MemoryResult> memoryResults)
        {
            public int CluserLabel { get; set; } = cluserLabel;
            public string Title { get; set; } = title;
            public string? Tags { get; set; }
            public List<MemoryResult> MemoryResults { get; set; } = memoryResults;
        }

        private List<ClusterDisplay> _clusters = new();

        private readonly Dictionary<DistanceFunction, string> _distanceFunctionDescriptions = typeof(DistanceFunction).GetEnumsWithDescriptions<DistanceFunction>();
        private class ClusterForm
        {
            public int MinPoints { get; set; } = 3;
            public int MinCluster { get; set; } = 3;
            public int ItemCount { get; set; } = 100;
            public DistanceFunction DistanceFunction { get; set; } = DistanceFunction.CosineSimilarity;
            public bool UseDefaultContent { get; set; } = true;
            public FileUploadData FileUpload { get; set; } = new();
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
            if (!form.UseDefaultContent)
            {
                _busyText = "Processing File...";
                StateHasChanged();
                await HandleFile(form.FileUpload);
                _busyText = "Clustering...";
                StateHasChanged();
            }
            await RunCluster(form);
            _isBusy = false;
            StateHasChanged();
        }
        private async Task RunCluster(ClusterForm form)
        {
            var collection = form.UseDefaultContent ? null : FileuploadCollection;
            _memoryResults = await CoreKernelService.GetItemClustersFromCollection(form.ItemCount, "*", form.MinPoints, form.MinCluster, form.DistanceFunction, collection);
            var memoryGroups = _memoryResults.GroupBy(x => x.Cluster).Select(g =>
                new ClusterDisplay(g.Key, g.FirstOrDefault()?.ClusterTitle ?? "no title", g.ToList()) { Tags = g.FirstOrDefault()?.ClusterSummary });
            _clusters = memoryGroups.ToList();
            StateHasChanged();
        }

        private void ShowText(MemoryResult result)
        {
            _outputText =
                $"Cluster: {result.ClusterTitle}\nArticle: {result.Title}\n------------------------------------\n{result.Text}\n------------------------------------\n{result.ClusterSummary}";
            StateHasChanged();
        }

        private class FileUploadData
        {
            public string? FileName { get; set; }
            public long? FileSize { get; set; }
            public string? FileBase64 { get; set; }
            public const int MaxFileSize = int.MaxValue;
            
        }

        private FileUploadData _fileUploadData = new();


        private async Task HandleFile(FileUploadData fileUploadData)
        {
            if (_fileNameHandled == fileUploadData.FileName) return;
            var file = Convert.FromBase64String(fileUploadData.FileBase64!.ExtractBase64FromDataUrl());
            await CoreKernelService.ChunkAndSaveFileCluster(file, $"File: {fileUploadData.FileName}", collectionName: FileuploadCollection);
            _fileNameHandled = fileUploadData.FileName!;
            _isBusy = false;
            StateHasChanged();
        }
        private string MarkdownAsHtml(string? text)
        {
            if (text == null) return "";
            var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
            var result = Markdown.ToHtml(text, pipeline);
            return result;

        }
        private void HandleError(UploadErrorEventArgs args)
        {
            NotificationService.Notify(NotificationSeverity.Error, "Upload Error", args.Message);
            Console.WriteLine($"Error: {args.Message}\n");
        }
    }
}
