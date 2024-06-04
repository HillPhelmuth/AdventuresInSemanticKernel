using Microsoft.SemanticKernel.ChatCompletion;

namespace SkPluginLibrary.Abstractions;

public interface ICustomNativePlugins
{
    Task<CodeOutputModel> GenerateCompileAndExecuteReplPlugin(string input, string code = "", ReplType replType = ReplType.ReplConsole);
    IAsyncEnumerable<string> RunWebSearchChat(string query);
    event EventHandler<string>? StringWritten;
    IAsyncEnumerable<string> RunWikiSearchChat(string query);
    event Action<string>? AdditionalAgentText;
    IAsyncEnumerable<string> WriteNovel(string outline, AIModel aiModel = AIModel.Planner,
	    CancellationToken token = default);
    Task<string> CreateNovelOutline(string theme, string characterDetails = "", string plotEvents = "",
	    string novelTitle = "", int chapters = 15, AIModel aIModel = AIModel.Planner);

    Task<NovelOutline> GenerateNovelIdea(NovelGenre genre);
}