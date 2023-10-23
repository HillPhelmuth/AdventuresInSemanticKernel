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
public class HdbscanService
{
    private readonly ILogger<HdbscanService> _logger;

    public HdbscanService(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<HdbscanService>();

    }

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
            results.Add(new MemoryResult(memories.ElementAt(i).Metadata.Description, memories.ElementAt(i).Metadata.Text, labels[i]));
        }

        //var resultString = new StringBuilder();
        //resultString.AppendLine("Clustered Data:");
        //for (var j = 1; j < df.Rows.Count; j++)
        //{
        //    var cluster = df.Rows[j]["cluster"];
        //    var title = df.Rows[j]["title"];
        //    var text = df.Rows[j]["text"];
        //    resultString.AppendLine($"Cluster: {cluster} Title: {title} Text: {text}");
        //}

        //var logString = resultString.ToString();
        //_logger.LogInformation("Labels did something I guess. Here it is:\n {logString}", logString);
        return results;
    }
    public static List<List<T>> SplitList<T>(List<T> mainList, int n)
    {
        var sublists = new List<List<T>>();
        for (var i = 0; i < mainList.Count; i += n)
        {
            sublists.Add(mainList.GetRange(i, Math.Min(n, mainList.Count - i)));
        }
        return sublists;
    }
    public static T[][] SplitArray<T>(T[] mainArray, int n)
    {
        var totalArrays = (mainArray.Length + n - 1) / n;
        T[][] subarrays = new T[totalArrays][];

        for (var i = 0; i < totalArrays; i++)
        {
            var start = i * n;
            var length = Math.Min(n, mainArray.Length - start);
            subarrays[i] = new T[length];
            Array.Copy(mainArray, start, subarrays[i], 0, length);
        }

        return subarrays;
    }
}