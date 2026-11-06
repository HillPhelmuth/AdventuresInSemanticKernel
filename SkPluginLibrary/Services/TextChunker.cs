using System.Text;
using System.Text.RegularExpressions;

namespace SkPluginLibrary.Services;

/// <summary>
/// Splits text into token-bounded lines and paragraphs for embedding. (A small stand-in for the text
/// chunking helpers the app used to get from Semantic Kernel; Agent Framework leaves chunking to the app.)
/// </summary>
public static class TextChunker
{
    public delegate int TokenCounter(string input);

    private static readonly string[] PlainTextSplits = ["\n\r", ". ", "? ", "! ", "; ", ": ", ", ", ") ", "] ", "} ", " ", "-"];
    private static readonly string[] MarkdownSplits = [". ", "? ", "! ", "\n", "; ", ": ", ", ", ") ", "] ", "} ", " ", "-"];

    public static List<string> SplitPlainTextLines(string text, int maxTokensPerLine, TokenCounter? tokenCounter = null) =>
        Split(text, maxTokensPerLine, PlainTextSplits, tokenCounter);

    public static List<string> SplitMarkDownLines(string text, int maxTokensPerLine, TokenCounter? tokenCounter = null) =>
        Split(text, maxTokensPerLine, MarkdownSplits, tokenCounter);

    public static List<string> SplitPlainTextParagraphs(IEnumerable<string> lines, int maxTokensPerParagraph,
        int overlapTokens = 0, string? chunkHeader = null, TokenCounter? tokenCounter = null) =>
        Group(lines, maxTokensPerParagraph, overlapTokens, chunkHeader, tokenCounter);

    public static List<string> SplitMarkdownParagraphs(IEnumerable<string> lines, int maxTokensPerParagraph,
        int overlapTokens = 0, string? chunkHeader = null, TokenCounter? tokenCounter = null) =>
        Group(lines, maxTokensPerParagraph, overlapTokens, chunkHeader, tokenCounter);

    private static int Count(string s, TokenCounter? counter) => counter?.Invoke(s) ?? s.Length / 4;

    private static List<string> Split(string text, int maxTokens, string[] separators, TokenCounter? counter)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) return result;
        SplitRecursive(text.Replace("\r\n", "\n"), maxTokens, separators, 0, counter, result);
        return result;
    }

    private static void SplitRecursive(string text, int maxTokens, string[] separators, int level, TokenCounter? counter, List<string> output)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        if (Count(text, counter) <= maxTokens || level >= separators.Length)
        {
            if (Count(text, counter) > maxTokens)
            {
                // No separators left: hard split on character count.
                var size = Math.Max(1, maxTokens * 3);
                for (var i = 0; i < text.Length; i += size) output.Add(text.Substring(i, Math.Min(size, text.Length - i)).Trim());
            }
            else output.Add(text.Trim());
            return;
        }
        var separator = separators[level];
        var parts = separator == "\n\r" ? text.Split('\n') : SplitKeepingSeparator(text, separator);
        if (parts.Count() == 1) { SplitRecursive(text, maxTokens, separators, level + 1, counter, output); return; }
        var buffer = new StringBuilder();
        foreach (var part in parts)
        {
            if (Count(part, counter) > maxTokens)
            {
                Flush(buffer, output);
                SplitRecursive(part, maxTokens, separators, level + 1, counter, output);
                continue;
            }
            if (buffer.Length > 0 && Count(buffer + part, counter) > maxTokens) Flush(buffer, output);
            buffer.Append(part);
            if (separator == "\n\r") buffer.Append('\n');
        }
        Flush(buffer, output);
    }

    private static IEnumerable<string> SplitKeepingSeparator(string text, string separator)
    {
        var start = 0;
        int index;
        while ((index = text.IndexOf(separator, start, StringComparison.Ordinal)) >= 0)
        {
            yield return text.Substring(start, index + separator.Length - start);
            start = index + separator.Length;
        }
        if (start < text.Length) yield return text[start..];
    }

    private static void Flush(StringBuilder buffer, List<string> output)
    {
        var s = buffer.ToString().Trim();
        if (s.Length > 0) output.Add(s);
        buffer.Clear();
    }

    private static List<string> Group(IEnumerable<string> lines, int maxTokens, int overlapTokens, string? header, TokenCounter? counter)
    {
        var paragraphs = new List<string>();
        var lineList = lines.ToList();
        var headerTokens = string.IsNullOrEmpty(header) ? 0 : Count(header, counter);
        var current = new List<string>();
        var currentTokens = headerTokens;
        string Build(List<string> items) => string.IsNullOrEmpty(header) ? string.Join(" ", items) : $"{header}{string.Join(" ", items)}";
        foreach (var line in lineList)
        {
            var tokens = Count(line, counter);
            if (current.Count > 0 && currentTokens + tokens > maxTokens)
            {
                paragraphs.Add(Build(current));
                // Carry trailing lines forward as overlap.
                var overlap = new List<string>();
                var overlapCount = 0;
                for (var i = current.Count - 1; i >= 0 && overlapTokens > 0; i--)
                {
                    var t = Count(current[i], counter);
                    if (overlapCount + t > overlapTokens) break;
                    overlap.Insert(0, current[i]);
                    overlapCount += t;
                }
                current = overlap;
                currentTokens = headerTokens + overlapCount;
            }
            current.Add(line);
            currentTokens += tokens;
        }
        if (current.Count > 0) paragraphs.Add(Build(current));
        return paragraphs;
    }
}
