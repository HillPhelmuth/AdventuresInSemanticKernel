using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static SkPluginLibrary.CoreKernelService;
using Microsoft.SemanticKernel.Connectors.Sqlite;
using UglyToad.PdfPig;
using Microsoft.SemanticKernel.Text;
using NRedisStack.Search;
using Microsoft.SemanticKernel.Memory;

namespace SkPluginLibrary.Plugins
{
    internal class BlazorMemoryPlugin
    {
        public const string ChatWithBlazorSystemPromptTemplate =
            """
            Use the [Blazor CONTEXT] below to answer the user's questions.
            Blazor is a .NET frontend web framework that supports both server-side rendering and
            client interactivity in a single programming model.            

            [Blazor CONTEXT]
            {{$memory_context}}

            """;
        [KernelFunction, Description("Retreive relevant information from Blazor documentation to form Blazor specific chat instructions")]
        [return: Description("Chat instructions with relevant information to provide additional context")]
        public async Task<string> LearnAboutBlazor([Description("Latest user chat query")] string query, [Description("chat history to include as part of the search query")] string? history = null, [Description("Number of most similar items to return from search")] int topN = 10)
        {
            var ready = await CollectionExists();
            //if (!ready)
            //{
            await LoadMemories();
            //}
            var kernel = Kernel.CreateBuilder()
                .AddOpenAIChatCompletion(TestConfiguration.OpenAI.ModelId, TestConfiguration.OpenAI.ApiKey)
                .Build();
            var semanticMemory = await GetSemanticTextMemory();
            var memoryItems = await semanticMemory.SearchAsync(CollectionName.BlazorDocsCollection, $"{query} {history}", topN, 0.78).ToListAsync();
            var memory = string.Join("\n", memoryItems.Select(x => x.Metadata.Text));
            ConsoleLogger.LoggerFactory.CreateLogger<KernelChatPlugin>().LogInformation("Memory:\n {memory}", memory);
            var args = new KernelArguments
            {
                ["memory_context"] = memory
            };

            var promptTemplateFactory = new KernelPromptTemplateFactory();
            var engine = promptTemplateFactory.Create(new PromptTemplateConfig(ChatWithBlazorSystemPromptTemplate));
            var systemPrompt = await engine.RenderAsync(kernel, args);
            return systemPrompt;
            //var chatService = new OpenAIChatCompletionService(TestConfiguration.OpenAI!.ChatModelId, TestConfiguration.OpenAI.ApiKey);
            //var chat = new ChatHistory(systemPrompt);
            //chat.AddUserMessage(query);
            //await foreach (var token in chatService.GetStreamingChatMessageContentsAsync(chat, new OpenAIPromptExecutionSettings { MaxTokens = 2000, Temperature = 1 }))
            //{
            //    yield return token.Content ?? "";
            //}

        }
        protected static async Task<bool> CollectionExists()
        {
            var sqliteMemoryStore = await SqliteMemoryStore.ConnectAsync(TestConfiguration.Sqlite!.ChatContentConnectionString!);
            var collections = await sqliteMemoryStore.GetCollectionsAsync().ToListAsync();
            return collections.Contains(CollectionName.BlazorDocsCollection);
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
        protected static async Task LoadMemories()
        {
            if (!await CollectionExists())
            {
                var sqliteMemoryStore = await SqliteMemoryStore.ConnectAsync(TestConfiguration.Sqlite!.ChatContentConnectionString!);
                await LoadCollection(sqliteMemoryStore, CollectionName.BlazorDocsCollection);
            }
        }

        protected static async Task LoadCollection(IMemoryStore sqliteMemoryStore, string docsCollectionName)
        {
            await sqliteMemoryStore.CreateCollectionAsync(docsCollectionName);
            var chunks = ReadAndChunkBlazorPdf();
            var memory = await GetSemanticTextMemory();
            var chunkNumber = 687;
            foreach (var chunk in chunks.Skip(686))
            {
                var id = await memory.SaveInformationAsync(docsCollectionName, chunk, $"BlazorPdf_{chunkNumber}");
                Console.WriteLine($"Saved Chunk {chunkNumber} (id:{id})");
                chunkNumber++;
            }
        }

        private static List<string> ReadAndChunkBlazorPdf()
        {
            var chunckedResults = new List<string>();
            var docBuilder = new StringBuilder();
            var path = @"C:\Users\adamh\Downloads\aspnet core-aspnetcore-8.0 _Blazor_Microsoft Learn.pdf";
            using var document = PdfDocument.Open(path, new ParsingOptions { UseLenientParsing = true });
            foreach (var page in document.GetPages())
            {
                var pageBuilder = new StringBuilder();
                foreach (var word in page.GetWords())
                {
                    pageBuilder.Append(word.Text);
                    pageBuilder.Append(' ');
                }
                var pageText = pageBuilder.ToString();
                docBuilder.AppendLine(pageText);
            }
            var textString = docBuilder.ToString();
            var lines = TextChunker.SplitPlainTextLines(textString, 128, StringHelpers.GetTokens);
            var paragraphs = TextChunker.SplitPlainTextParagraphs(lines, 512, 96, "## Blazor Documentation\n", StringHelpers.GetTokens);
            return paragraphs;
        }
    }
}
