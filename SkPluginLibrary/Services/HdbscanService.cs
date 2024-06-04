using HdbscanSharp.Distance;
using HdbscanSharp.Runner;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.Memory;
using System.ComponentModel;
using System.Diagnostics;

namespace SkPluginLibrary.Services;

public enum DistanceFunction
{
    [Description("Measures the cosine of the angle between two non-zero vectors.")]
    CosineSimilarity,

    [Description("Measures the straight-line distance between two points in Euclidean space.")]
    EuclideanDistance,

    [Description("Computes the total absolute difference of their Cartesian coordinates.")]
    ManhattanDistance,

    [Description("Measures the statistical correlation between two sets of data.")]
    PearsonCorrelation,

    [Description("Represents the maximum absolute difference between components of two vectors.")]
    SupremumDistance
}
public class HdbscanService(ILoggerFactory loggerFactory)
{
    private readonly ILogger<HdbscanService> _logger = loggerFactory.CreateLogger<HdbscanService>();

    private IDistanceCalculator<double[]> GetDistanceFunction(DistanceFunction distanceFunction)
    {
        return distanceFunction switch
        {
            DistanceFunction.CosineSimilarity => new CosineSimilarity(),
            DistanceFunction.EuclideanDistance => new EuclideanDistance(),
            DistanceFunction.ManhattanDistance => new ManhattanDistance(),
            DistanceFunction.PearsonCorrelation => new PearsonCorrelation(),
            DistanceFunction.SupremumDistance => new SupremumDistance(),
            _ => new CosineSimilarity()
        };
    }
    public List<MemoryResult> ClusterAsync(IEnumerable<MemoryRecord> memoryRecords, int minPoints = 3, int minCluster = 3, DistanceFunction distanceFunction = DistanceFunction.CosineSimilarity)
    {
        var memories = memoryRecords.Select(x => new MemoryQueryResult(x.Metadata, 0, x.Embedding));
        return ClusterAsync(memories, minPoints, minCluster);

    }
    public List<MemoryResult> ClusterAsync(IEnumerable<MemoryQueryResult> memories, int minPoints = 3, int minCluster = 3, DistanceFunction distanceFunction = DistanceFunction.CosineSimilarity)
    {
        var memCount = memories.Count();
        _logger.LogInformation("Starting Hdbscan of {memCount} items.\nMinPoints: {minPoints}\nMinClusterSize: {minCluster}", memCount, minPoints, minCluster);
        var sw = new Stopwatch();
        sw.Start();
        var data = memories.Select(x => x.Embedding.Value.ToArray().Select(y => (double)y).ToArray()).ToArray();

        var result = HdbscanRunner.Run(new HdbscanParameters<double[]>
        {
            DataSet = data,
            MinPoints = minPoints,
            MinClusterSize = minCluster,
            DistanceFunction = GetDistanceFunction(distanceFunction),
            MaxDegreeOfParallelism = 4

        });
        sw.Stop();
        var scantime = sw.Elapsed.TotalSeconds;
        _logger.LogInformation("Completed Hdbscan in {scantime}s", scantime);

        var labels = result.Labels;
        var results = new List<MemoryResult>();
        for (var i = 0; i < memories.Count(); i++)
        {
            if (labels[i] == -1) continue;
            var memoryRecordMetadata = memories.ElementAt(i).Metadata;
            results.Add(new MemoryResult(memoryRecordMetadata.Description, memoryRecordMetadata.Text, labels[i]) { MetadataId = memoryRecordMetadata.Id});
        }

        return results;
    }
    
}