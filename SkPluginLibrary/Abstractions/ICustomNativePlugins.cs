using Microsoft.SemanticKernel.ChatCompletion;

namespace SkPluginLibrary.Abstractions;

public interface ICustomNativePlugins
{
    Task<CodeOutputModel> GenerateCompileAndExecuteReplPlugin(string input, string code = "", ReplType replType = ReplType.ReplConsole);
    IAsyncEnumerable<string> RunWebSearchChat(string query);
    event EventHandler<string>? StringWritten;
    IAsyncEnumerable<string> RunWikiSearchChat(string query);
    event Action<string>? AdditionalAgentText;
    event EventHandler<string>? TextToImageUrl;
    IAsyncEnumerable<string> WriteNovel(string outline, AIModel aiModel = AIModel.Gpt4OCurrent,
	    CancellationToken token = default);
    Task<string> CreateNovelOutline(string theme, string characterDetails = "", string plotEvents = "",
	    string novelTitle = "", int chapters = 15, AIModel aIModel = AIModel.Gpt4OCurrent);

    Task<NovelOutline> GenerateNovelIdea(NovelGenre genre);
    IAsyncEnumerable<ReadOnlyMemory<byte>?> TextToAudioAsync(string text, CancellationToken cancellationToken = default);
    IAsyncEnumerable<PfEvalInput> GenerateEvalInputsFromWeb(List<QnAInput> qnaInputs);
}