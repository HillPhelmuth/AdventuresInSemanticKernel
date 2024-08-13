using Microsoft.SemanticKernel.Text;
using System.Reflection;
using System.Text;
using System.Text.Json;
using UglyToad.PdfPig;

namespace SkPluginLibrary.Models.Helpers;

public class FileHelper
{
    public static byte[] GenerateTextFile(string content)
    {
        return Encoding.UTF8.GetBytes(content);
    }
    public static byte[] GenerateHtmlFile(Dictionary<string, string> chapters)
    {
        var chapterText = string.Join("<br/>", chapters.Values);
        var htmlText = HtmlBoilerPlate.Replace("--novelText--", chapterText);
        return GenerateTextFile(htmlText);
    }
    private const string HtmlBoilerPlate = """
        <!DOCTYPE html>
        <html lang="en">
        <head>
            <meta charset="UTF-8">
            <meta name="viewport" content="width=device-width, initial-scale=1.0">
            <title>Your Novel Title</title>
            <!-- Additional meta tags can be added here for SEO, author information, etc. -->
        </head>
        <body>
            <h1>Your Novel Title</h1>
            --novelText--
        </body>
        </html>
        """;
    public static async Task<T> ExtractFromAssemblyAsync<T>(string fileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var jsonName = assembly.GetManifestResourceNames()
            .SingleOrDefault(s => s.EndsWith(fileName, StringComparison.OrdinalIgnoreCase)) ?? "";
        await using var stream = assembly.GetManifestResourceStream(jsonName);
        using var reader = new StreamReader(stream);
        object result = await reader.ReadToEndAsync();
        if (typeof(T) == typeof(string))
            return (T)result;
        return JsonSerializer.Deserialize<T>(result.ToString());
    }
    public static T ExtractFromAssembly<T>(string fileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var jsonName = assembly.GetManifestResourceNames()
            .SingleOrDefault(s => s.EndsWith(fileName, StringComparison.OrdinalIgnoreCase)) ?? "";
        using var stream = assembly.GetManifestResourceStream(jsonName);
        using var reader = new StreamReader(stream);
        object result = reader.ReadToEnd();
        if (typeof(T) == typeof(string))
            return (T)result;
        return JsonSerializer.Deserialize<T>(result.ToString());
    }
    public static List<string> ReadAndChunkPdf(string path)
    {
        var docBuilder = new StringBuilder();
        //var path = @"C:\Users\adamh\Downloads\aspnet core-aspnetcore-8.0 _Blazor_Microsoft Learn.pdf";
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
    public static List<string> ReadAndChunkMarkdownFile(string path, string chunckHeader = "")
    {
        var text = File.ReadAllText(path);
        var lines = TextChunker.SplitMarkDownLines(text, 128, StringHelpers.GetTokens);
        var paragraphs = TextChunker.SplitMarkdownParagraphs(lines, 512, 96, $"## {chunckHeader}\n", StringHelpers.GetTokens);
        return paragraphs;
    }
    public static T DeserializeFromBytes<T>(byte[] data)
    {
        // Convert byte array to string
        string json = Encoding.UTF8.GetString(data);

        // Deserialize the JSON string to the specified type
        T result = JsonSerializer.Deserialize<T>(json);

        return result;
    }
}

public enum FileType
{
    Text, Pdf, Json
}