using System.Text;
using Microsoft.SemanticKernel.Text;

namespace SkPluginLibrary.Plugins.NativePlugins;

[Description("Sanitize, parse or split json content")]
public class HandleJsonPlugin
{
    [KernelFunction, Description("Sanitize json content for efficient consumption by ai")]
    public string Sanitize(string input)
    {
            return input.SanitizeJson();
        }
    [KernelFunction, Description("Sanitize json content and cut to size for efficient and effective consumption by ai")]
    public string SanitizeAndSplit([Description("Full json content")] string input, int maxTokens)
    {
            var sanitized = input.SanitizeJson();
            var lines = TextChunker.SplitPlainTextLines(sanitized, maxTokens, tokenCounter: StringHelpers.GetTokens);
            var paragraphs = TextChunker.SplitPlainTextParagraphs(lines, maxTokens, tokenCounter: StringHelpers.GetTokens);
            var sb = new StringBuilder();
            var tokens = 0;
            var max = maxTokens * 3;
            foreach (var para in paragraphs)
            {
                var tokensInPara = StringHelpers.GetTokens(para);
                tokens += tokensInPara;
                if (tokens <= max)
                {
                    sb.AppendLine(para);
                }
                else
                {
                    break;
                }
            }
            return sb.ToString();
        }
}