using Microsoft.SemanticKernel.Memory;
using SkPluginLibrary.Services;

namespace SkPluginLibrary.Abstractions;

public interface IMemoryConnectors
{
    IAsyncEnumerable<ContextItem> SaveBatchToMemory(string collection, List<ContextItem> items,
        MemoryStoreType storeType = MemoryStoreType.InMemory, bool delete = false,
        string model = "text-embedding-3-small");
    Task<List<MemoryQueryResult>> SearchKernelMemory(string query, string collection, int topN = 3, double minThreshold = 0.0);
    Task<List<string>> GenerateRandomSentances(int count = 10);
    Task<List<MemoryResult>> GetItemClustersFromCollection(int numberOfItems = 100, string searchTerm = "*", int minpoints = 3, int minCluster = 3, DistanceFunction distanceFunction = DistanceFunction.CosineSimilarity, string? collection = null);
    Task ChunkAndSaveFileCluster(byte[] file, string filename, FileType fileType = FileType.Pdf,
        string? collectionName = null);

    Task<List<MemoryResult>> ItemClustersFromCollection(int minpoints, int minCluster,
        DistanceFunction distanceFunction,
        IEnumerable<MemoryQueryResult> itemList, Kernel kernel);
}