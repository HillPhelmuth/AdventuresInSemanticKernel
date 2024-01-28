using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Connectors.Sqlite;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Text;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UglyToad.PdfPig;
using static SkPluginLibrary.CoreKernelService;

namespace SkPluginLibrary.Plugins
{
    internal class LangchainChatPlugin
    {
        protected string Name = "LangchainChat";
        protected const string ChatPromptTemplate =
            """
            Use the [LangChain CONTEXT] below to answer the user's questions.
            LangChain is an open-source SDK that lets you easily build genertive AI agents that can interact with your code, external REST apis and other AI agents.

            [LangChain CONTEXT]
            {{$memory_context}}
            

            """;

        protected const string DocsPath = @"C:\Users\adamh\source\repos\langchain\docs\docs";

        [KernelFunction, Description("Retreive relevant information from LangChain documentation to form LangChain specific chat instructions")]
        [return: Description("Chat instructions with relevant content documents to provide additional up-to-date context")]
        public async Task<string> LearnAboutLangChain([Description("Latest user chat query")] string query, [Description("Chat history to include as part of search query")] string? history = null, [Description("Number of most similar items to return from search")] int topN = 5)
        {
            var ready = await CollectionExists();
            if (!ready.exists)
            {
                await LoadCollection(ready.memStore);
            }
            var kernel = Kernel.CreateBuilder()
                .AddOpenAIChatCompletion(TestConfiguration.OpenAI.Gpt35ModelId, TestConfiguration.OpenAI.ApiKey)
                .Build();
            var semanticMemory = await GetSemanticTextMemory();
            var memoryItems = await semanticMemory.SearchAsync(CollectionName.LangchainDocsCollection, $"{query} {history}", topN, 0.78).ToListAsync();
            var memory = string.Join("\n", memoryItems.Select(x => x.Metadata.Text));
            ConsoleLogger.LoggerFactory.CreateLogger<LangchainChatPlugin>().LogInformation("Memory:\n {memory}", memory);
            var args = new KernelArguments
            {
                ["memory_context"] = memory
            };

            var promptTemplateFactory = new KernelPromptTemplateFactory();
            var engine = promptTemplateFactory.Create(new PromptTemplateConfig(ChatPromptTemplate));
            var systemPrompt = await engine.RenderAsync(kernel, args);
            return systemPrompt;


        }
        protected static async Task<(bool exists, IMemoryStore? memStore)> CollectionExists()
        {
            var sqliteMemoryStore = await SqliteMemoryStore.ConnectAsync(TestConfiguration.Sqlite!.ChatContentConnectionString!);
            var collections = await sqliteMemoryStore.GetCollectionsAsync().ToListAsync();
            var collectionExists = collections.Contains(CollectionName.LangchainDocsCollection);
            if (collectionExists)
                return (collectionExists, sqliteMemoryStore);
            return (false, sqliteMemoryStore);
        }
        protected static async Task LoadCollection(IMemoryStore sqliteMemoryStore)
        {
            await sqliteMemoryStore.CreateCollectionAsync(CollectionName.LangchainDocsCollection);
            var chunks = ReadAndChunkPythonMkdn();
            var memory = await GetSemanticTextMemory();
            var chunkNumber = 1;
            foreach (var chunk in chunks)
            {
                var id = await memory.SaveInformationAsync(CollectionName.LangchainDocsCollection, chunk, $"langChain_{chunkNumber}");
                Console.WriteLine($"Saved Chunk {chunkNumber} (id:{id})");
                chunkNumber++;
            }
        }
        protected static async Task<ISemanticTextMemory> GetSemanticTextMemory()
        {
            var sqliteMemoryStore = await SqliteMemoryStore.ConnectAsync(TestConfiguration.Sqlite!.ChatContentConnectionString!);
            return new MemoryBuilder()
                .WithMemoryStore(sqliteMemoryStore)
                .WithLoggerFactory(ConsoleLogger.LoggerFactory)
                .WithOpenAITextEmbeddingGeneration(TestConfiguration.OpenAI.EmbeddingModelId, TestConfiguration.OpenAI.ApiKey)
                .Build();
        }
        protected static List<string> ReadAndChunkPythonMkdn()
        {
            var result = new List<string>();
            var markdownFiles = Directory.GetFiles(DocsPath, "*.md*", SearchOption.AllDirectories);
            foreach (var mkdFile in markdownFiles)
            {
                var chunks = FileHelper.ReadAndChunkMarkdownFile(mkdFile, Path.GetFileNameWithoutExtension(mkdFile));
                result.AddRange(chunks);
            }
            return result;
        }
    }
}
