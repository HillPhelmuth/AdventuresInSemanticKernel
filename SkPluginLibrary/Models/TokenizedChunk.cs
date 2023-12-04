namespace SkPluginLibrary.Models;

public class TokenizedChunk
{
    public TokenizedChunk(int chunkNumber, List<TokenString> tokenStrings, int tokenCount)
    {
        ChunkNumber = chunkNumber;
        TokenStrings = tokenStrings;
        TokenCount = tokenCount;
    }
    public int ChunkNumber { get; set; }
    public List<TokenString> TokenStrings { get; set; } = new();
    public string Text => string.Join("", TokenStrings.Select(x => x.StringValue)).Replace("&nbsp;", " ");
    public int TokenCount { get; set; }

    public bool IsTokenized { get; set; }
}