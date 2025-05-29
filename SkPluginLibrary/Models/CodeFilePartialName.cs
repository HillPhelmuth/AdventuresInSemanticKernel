namespace SkPluginLibrary.Models;

public enum CodeFilePartialName
{
    None,
    CoreKernelService,
    ChatCompletionGroup,
    CoreKernelService_CustomExamples,
    CoreKernelService_KernelBuilder,
    CoreKernelService_Memory,
    CoreKernelService_Samples,
    CoreKernelService_Tokens
}

public class CodeFilePartialPath
{
    public const string None = "";
    public const string ChatCompletionGroup = "Agents/SkAgents/ChatCompletionGroup.cs";
    public const string CoreKernelService = "CoreKernelService.cs";
    public const string CoreKernelService_CustomExamples = "CoreKernelService.CustomExamples.cs";
    public const string CoreKernelService_KernelBuilder = "CoreKernelService.KernelBuilder.cs";
    public const string CoreKernelService_Memory = "CoreKernelService.Memory.cs";
    public const string CoreKernelService_Samples = "CoreKernelService.Samples.cs";
    public const string CoreKernelService_Tokens = "CoreKernelService.Tokens.cs";
}
public static class CodeFilePartialNameExtensions
{
    public static string GetPartialName(this CodeFilePartialName partialName)
    {
        return partialName switch
        {
            CodeFilePartialName.CoreKernelService => CodeFilePartialPath.CoreKernelService,
            CodeFilePartialName.CoreKernelService_CustomExamples => CodeFilePartialPath.CoreKernelService_CustomExamples,
            CodeFilePartialName.CoreKernelService_KernelBuilder => CodeFilePartialPath.CoreKernelService_KernelBuilder,
            CodeFilePartialName.CoreKernelService_Memory => CodeFilePartialPath.CoreKernelService_Memory,
            CodeFilePartialName.CoreKernelService_Samples => CodeFilePartialPath.CoreKernelService_Samples,
            CodeFilePartialName.CoreKernelService_Tokens => CodeFilePartialPath.CoreKernelService_Tokens,
            CodeFilePartialName.None => CodeFilePartialPath.None,
            CodeFilePartialName.ChatCompletionGroup => CodeFilePartialPath.ChatCompletionGroup,
            _ => throw new ArgumentOutOfRangeException(nameof(partialName), partialName, null)
        };
    }
}