namespace SkPluginLibrary.Abstractions;

public interface ICustomNativePlugins
{
    Task<CodeOutputModel> GenerateCompileAndExecuteReplPlugin(string input, string code = "", ReplType replType = ReplType.ReplConsole);
    IAsyncEnumerable<string> RunWebSearchChat(string query);
    event EventHandler<string>? StringWritten;
    IAsyncEnumerable<string> RunWikiSearchChat(string query);
}