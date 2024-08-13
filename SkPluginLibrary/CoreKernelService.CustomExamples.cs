using System.Globalization;
using System.Net;
using System.Runtime.CompilerServices;
using SkPluginLibrary.Plugins;
using System.Text.Json;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SkPluginLibrary.Models.Hooks;
using System.Text.RegularExpressions;
using System.Text;
using Microsoft.SemanticKernel.TextToAudio;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.SemanticKernel.TextToImage;
using Polly;
using System.Text.Json.Serialization;

namespace SkPluginLibrary;

public partial class CoreKernelService
{
	#region Custom Plugins - crawl wiki,c# repl, web search chat, novel writer, prompt engineer

	public async Task<CodeOutputModel> GenerateCompileAndExecuteReplPlugin(string input, string code = "",
		ReplType replType = ReplType.ReplConsole)
	{
		var kernel = CreateKernel();
		var chatService = kernel.Services.GetRequiredService<IChatCompletionService>();
		var replSkill = new ReplCsharpPlugin(kernel, _scriptService, _compilerService);
		var repl = kernel.ImportPluginFromObject(replSkill);
		var kernelArgs = new KernelArguments
		{
			["existingCode"] = code,
			["input"] = input,
			["sessionId"] = Guid.NewGuid().ToString()
		};

		FunctionResult kernelResult = await kernel.InvokeAsync(repl[replType.ToString()], kernelArgs);
		Console.WriteLine(kernelResult.ToString());

		var codeResult = JsonSerializer.Deserialize<CodeOutputModel>(kernelResult.Result());
		var outCode = $"{code}\n{codeResult?.Code}";
		var output = codeResult?.Output;
		return new CodeOutputModel { Code = outCode.TrimStart('\n'), Output = output };
	}
	public async IAsyncEnumerable<PfEvalInput> GenerateEvalInputsFromWeb(List<QnAInput> qnaInputs)
	{
		var kernel = CreateKernel(AIModel.Gpt4Turbo);
		var webPlugin = new WebCrawlPlugin(_bingSearchService);
		var webPluginInstance = kernel.ImportPluginFromObject(webPlugin, "WebSearchPlugin");
		var settings = new OpenAIPromptExecutionSettings { ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions, ResponseFormat = "json_object", Temperature = 0.3 };
		var systemPrompt =
			"""
			### Instructions
			Answer the user questions by searching the web.
			Always search the web before responding.
			Respond in json format to include an answer to the user's question in 100 words or less and a complete summary of the web content used as context to generate the answer. Content summary must 2-3 paragraphs that should be 200-300 words long.

			### Example Output Format
			```json
			{
				"Answer": "<An answer to the question in 100 words or less>",
				"Context": "<Summary of web content used to answer questions in 2-3 paragraphs (200-300 words)>"
			}
			```

			""";
		var chat = kernel.Services.GetRequiredService<IChatCompletionService>();
		foreach (var input in qnaInputs)
		{
			var chatHistory = new ChatHistory(systemPrompt);
			chatHistory.AddUserMessage(input.Question);
			var response = await chat.GetChatMessageContentAsync(chatHistory, settings, kernel);
			var output = JsonSerializer.Deserialize<QnAOutput>(response.Content);
			yield return new PfEvalInput { Question = input.Question, Answer = input.Answer, Context = output.Context, GroundTruth = output.Answer };
		}
	}
	public async IAsyncEnumerable<string> RunWebSearchChat(string query)
	{
		var kernel = CreateKernel(AIModel.Gpt4Turbo);
		var webPlugin = new WebCrawlPlugin(_bingSearchService);
		var webPluginInstance = kernel.ImportPluginFromObject(webPlugin, "WebSearchPlugin");
		var sysPromptTemplate =
			"""
            Answer the user's query using the web search results below. 
            Always search the web before responding. 
            Always include CITATIONS in your response.
            """;
		var functionHook = new FunctionFilterHook();
		kernel.FunctionInvocationFilters.Add(functionHook);
		functionHook.FunctionInvoking += (_, e) =>
		{
			var soemthing = e.Function;
			if (e.Function.Name.StartsWith("func")) return;
			AdditionalAgentText?.Invoke($"\n<h4> Executing {soemthing.Name} {soemthing.Metadata.PluginName}</h4>\n\n");
		};
		functionHook.FunctionInvoked += HandleCustomFunctionInvoked;
		query = $"""
                 Find the answer to the user question on the web

                 [User Question]

                 {query}

                 Include web page links
                 """;
		await foreach (var update in kernel.InvokePromptStreamingAsync(query, new KernelArguments(new OpenAIPromptExecutionSettings { MaxTokens = 1500, Temperature = 1.0, ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions, ChatSystemPrompt = sysPromptTemplate })))
		{
			var content = update.ToString();

			yield return content;
		}

	}
	public event Action<string>? AdditionalAgentText;
	public event EventHandler<string>? TextToImageUrl;


	public async IAsyncEnumerable<string> RunWikiSearchChat(string query)
	{
		var kernel = CreateKernel(AIModel.Gpt4Turbo);
		var functionHook = new FunctionFilterHook();
		kernel.FunctionInvocationFilters.Add(functionHook);
		functionHook.FunctionInvoking += (_, e) =>
		{
			var soemthing = e.Function;
			AdditionalAgentText?.Invoke($"\n<h4> Executing {soemthing.Name} {soemthing.Metadata.PluginName}</h4>\n\n");
		};
		functionHook.FunctionInvoked += HandleCustomFunctionInvoked;
		var wikiPlugin = new WikiChatPlugin();
		var wiki = kernel.ImportPluginFromObject(wikiPlugin);
		var systemPrompt =
			"""
            Answer the user's query using Wikipedia search results. 
            Always search the Wikipedia before responding. 
            Always end your response with a list of ## Citations that include links to relevant Wikipedia pages.
            """;
		var settings = new OpenAIPromptExecutionSettings { MaxTokens = 1500, Temperature = 1.0, ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions, ChatSystemPrompt = systemPrompt };
		query = $"""
                 Find the answer to the user question on Wikipedia

                 [User Question]

                 {query}

                 Include page links
                 """;
		var args = new KernelArguments(settings)
		{
			["input"] = query
		};

		var streamingKernel = kernel.InvokePromptStreamingAsync(query, args);

		//var kernelResult = streamingKernel;
		await foreach (var result in streamingKernel)
		{
			yield return result.ToString();
		}
	}
	public async Task<NovelOutline> GenerateNovelIdea(NovelGenre genre)
	{
		var kernel = CreateKernel();
		const string Prompt = """
                              You are a novel idea generator. Provided a Genre, generate a novel idea.
                              The idea should contain a Title, a Theme, a few Character Details, and a few key Plot Events.
                              ## Output
                              Your output must be in json using the following format:
                              ```json
                              {
                              	"Title": "Title of the Novel",
                                  "Theme": "Theme of the Novel",
                                  "Characters": "Character Details",
                                  "PlotEvents": "Plot Events"
                              }
                              ```

                              ## Genre
                              {{ $genre }}
                              """;
		var settings = new OpenAIPromptExecutionSettings { MaxTokens = 1024, Temperature = 0.7, ResponseFormat = "json_object" };
		var args = new KernelArguments(settings) { ["genre"] = genre.ToString() };
		var json = await kernel.InvokePromptAsync<string>(Prompt, args);
		return JsonSerializer.Deserialize<NovelOutline>(json);
	}
	public async Task<string> CreateNovelOutline(string theme, string characterDetails = "", string plotEvents = "",
		string novelTitle = "", int chapters = 15, AIModel aIModel = AIModel.Gpt4O)
	{
		var kernel = CreateKernel(aIModel);
		var novelWriter = new NovelWriterPlugin(aIModel);
		var plugin = kernel.ImportPluginFromObject(novelWriter);
		var createOutlineFunc = plugin["CreateNovelOutline"];
		_title = novelTitle;
		var args = new KernelArguments()
		{
			["theme"] = theme,
			["characterDetails"] = characterDetails,
			["plotEvents"] = plotEvents,
			["novelTitle"] = novelTitle,
			["chapters"] = chapters
		};
		var outline = await createOutlineFunc.InvokeAsync<string>(kernel, args);
		_description =
			$"""
            Theme or topic:
            {theme}

            Character Details:
            {characterDetails}

            Plot Events:
            {plotEvents}
            """;
		//AdditionalAgentText?.Invoke($"<p>{outline}</p>");
		return outline;
	}
	private string _description = "";
	private string _title = "";
	public async IAsyncEnumerable<string> WriteNovel(string outline, AIModel aiModel = AIModel.Gpt4O,
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{

		var chapters = SplitMarkdownByHeaders(outline);
		var kernel = CreateKernel(aiModel);

		var novelPlugin = new NovelWriterPlugin(aiModel);
		var plugin = kernel.ImportPluginFromObject(novelPlugin);
		var initialContext = new KernelArguments()
		{
			["chapters"] = outline,
			["description"] = _description,
			["title"] = _title
		};
		var writeChapterFunc = plugin["WriteChapterStreaming"];
		var summarizeChapterFunc = plugin["SummarizeChapter"];
		var summaryBuilder = new StringBuilder();
		var previousChapter = "none, 1st chapter";
		AdditionalAgentText?.Invoke(JsonSerializer.Serialize(chapters));
		var imageOutlines = "";
		foreach (var chapter in chapters)
		{
			if (cancellationToken.IsCancellationRequested) break;

			var chapterCopy = chapter + "\n\n**Word Count:** 3000 words";

			var writeArgs = new KernelArguments()
			{
				["outline"] = chapterCopy,
				["previousChapter"] = previousChapter,
				["summary"] = summaryBuilder.ToString(),
				["storyDescription"] = _description
			};
			var chapterText = "";
			imageOutlines += $"\n{chapter}";

			yield return $"[CHAPTER] {chapter.Split('\n')[0]}";
			await foreach (var token in writeChapterFunc.InvokeStreamingAsync<string>(kernel, writeArgs, cancellationToken))
			{
				yield return token;
				chapterText += token;
			}
			if (chapters.IndexOf(chapter) == 0 || chapters.IndexOf(chapter) % 3 == 0)
			{
				var imageUrl = await TextToImage(imageOutlines.TrimStart('\n'));
				TextToImageUrl?.Invoke(this, $"<img src='{imageUrl}' width='512' height='512'>");
				imageOutlines = "";
			}
			StringWritten?.Invoke(this, chapterText);
			yield return "\n\n  ";
			//AdditionalAgentText?.Invoke($"<p>{chapterText}</p>");
			previousChapter = chapterText;
			var summarizeArgs = new KernelArguments()
			{
				["chapterText"] = chapterText
			};
			var summary = await summarizeChapterFunc.InvokeAsync<string>(kernel, summarizeArgs, cancellationToken);
			summaryBuilder.AppendLine(summary);

		}
	}
	private async Task<string> TextToImage(string prompt)
	{
		var kernelBuilder = Kernel.CreateBuilder();
		kernelBuilder.Services.AddLogging(builder =>
		{
			builder.AddConsole();
		});
		kernelBuilder.Services.AddSingleton(_configuration);
		kernelBuilder.Services.ConfigureHttpClientDefaults(c =>
		{
			c.AddStandardResilienceHandler().Configure(o =>
			{
				o.Retry.ShouldHandle = args => ValueTask.FromResult(args.Outcome.Result?.StatusCode is HttpStatusCode.TooManyRequests);
				o.Retry.BackoffType = DelayBackoffType.Exponential;
				o.AttemptTimeout = new HttpTimeoutStrategyOptions { Timeout = TimeSpan.FromSeconds(90) };
				o.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(180);
				o.TotalRequestTimeout = new HttpTimeoutStrategyOptions { Timeout = TimeSpan.FromMinutes(5) };
			});
		});
		var kernel = kernelBuilder
			.AddOpenAITextToImage(TestConfiguration.OpenAI.ApiKey, modelId: "dall-e-3")
			.AddOpenAIChatCompletion("gpt-4o", TestConfiguration.OpenAI.ApiKey)
			.Build();
		var imageService = kernel.GetRequiredService<ITextToImageService>();
		var imageContent = await imageService.GenerateImageAsync($"Generate a photo-realistic image for this part of a novel. \n\n## Chapter Outline\n\n {prompt}", 1024, 1024);
		return imageContent;

	}
	public async IAsyncEnumerable<ReadOnlyMemory<byte>?> TextToAudioAsync(string text, [EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		var kernelBuilder = Kernel.CreateBuilder();
		kernelBuilder.Services.AddLogging(builder =>
		{
			builder.AddConsole();
		});
		kernelBuilder.Services.AddSingleton(_configuration);
		kernelBuilder.Services.ConfigureHttpClientDefaults(c =>
		{
			c.AddStandardResilienceHandler().Configure(o =>
			{
				o.Retry.ShouldHandle = args => ValueTask.FromResult(args.Outcome.Result?.StatusCode is HttpStatusCode.TooManyRequests);
				o.Retry.BackoffType = DelayBackoffType.Exponential;
				o.AttemptTimeout = new HttpTimeoutStrategyOptions { Timeout = TimeSpan.FromSeconds(90) };
				o.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(180);
				o.TotalRequestTimeout = new HttpTimeoutStrategyOptions { Timeout = TimeSpan.FromMinutes(5) };
			});
		});
		var kernel = kernelBuilder
			.AddOpenAITextToAudio(
				modelId: "tts-1",
				apiKey: TestConfiguration.OpenAI.ApiKey)
			.Build();

		var textToAudioService = kernel.GetRequiredService<ITextToAudioService>();

		OpenAITextToAudioExecutionSettings executionSettings = new()
		{
			Voice = "onyx", // The voice to use when generating the audio.
			ResponseFormat = "mp3", // The format to audio in.
			Speed = 1.0f // The speed of the generated audio.
		};
		foreach (var segment in text.SplitText(4096))
		{
			var audioContent = await textToAudioService.GetAudioContentAsync(segment, executionSettings, kernel, cancellationToken: cancellationToken);
			yield return audioContent.Data;
		}
		// Convert text to audio

		// Save audio content to a file
		// await File.WriteAllBytesAsync(AudioFilePath, audioContent.Data!.ToArray());
	}
	public static List<string> SplitMarkdownByHeaders(string markdownText)
	{
		var result = new List<string>();
		var headerPattern = new Regex(@"^(## .+)$", RegexOptions.Multiline);

		var matches = headerPattern.Matches(markdownText);

		var lastIndex = 0;
		foreach (Match match in matches)
		{
			if (lastIndex != match.Index)
			{
				if (lastIndex != 0)
				{
					var segment = markdownText.Substring(lastIndex, match.Index - lastIndex).Trim();
					result.Add(segment);
				}
				else
				{
					// Capture the first header and content if the first header is at the start
					var firstSegment = markdownText[..match.Index].Trim();
					if (!string.IsNullOrEmpty(firstSegment))
					{
						result.Add(firstSegment);
					}
				}
			}
			lastIndex = match.Index;
		}

		if (lastIndex == 0) return result;
		var lastSegment = markdownText[lastIndex..].Trim();
		result.Add(lastSegment);

		return result;
	}
	private void HandleCustomFunctionInvoked(object? sender, FunctionInvocationContext invokedArgs)
	{
		var function = invokedArgs.Function;
		if (invokedArgs.Function.Name.StartsWith("func")) return;
		AdditionalAgentText?.Invoke($"\n<h4> {function.Name} {function.Metadata.PluginName} Completed</h4>\n\n");
		var result = $"<p>{invokedArgs.Result}</p>";
		var resultsExpando = $"""

                              <details>
                                <summary>See Results</summary>
                                
                                <h5>Results</h5>
                                <p>
                                {result}
                                </p>

                                <br/>
                              </details>
                              """;

		AdditionalAgentText?.Invoke(resultsExpando);

	}
	#endregion
}
[TypeConverter(typeof(PfEvalInputConverter))]
public class PfEvalInput
{
	[JsonPropertyName("question")]
	public string? Question { get; set; }

	[JsonPropertyName("ground_truth")]
	public string? GroundTruth { get; set; }

	[JsonPropertyName("answer")]
	public string? Answer { get; set; }

	[JsonPropertyName("context")]
	public string? Context { get; set; }
}
public class PfEvalInputConverter : TypeConverter
{
	public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) => true;

	/// <summary>
	/// This method is used to convert object from string to actual type. This will allow to pass object to
	/// method function which requires it.
	/// </summary>
	public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
	{
		return JsonSerializer.Deserialize<PfEvalInput>((string)value);
	}

	/// <summary>
	/// This method is used to convert actual type to string representation, so it can be passed to AI
	/// for further processing.
	/// </summary>
	public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
	{
		return JsonSerializer.Serialize(value);
	}
}
public record QnAInput([property: JsonPropertyName("question")] string Question, [property: JsonPropertyName("answer")] string Answer);

public record QnAOutput(string Answer, string Context);