using System.Text.Json.Serialization;

namespace SkPluginLibrary.Models;

public class NovelOutline
{
    public string? Theme { get; set; }
    public string? Characters { get; set; }
    public string? PlotEvents { get; set; }
    public string? Title { get; set; }
	[JsonIgnore]
    public int ChapterCount { get; set; } = 5;
	[JsonIgnore]
    public AIModel AIModel { get; set; }
}
public enum NovelGenre
{
	None,
	Fantasy,
	ScienceFiction,
	Mystery,
	Romance,
	Thriller,
	HistoricalFiction,
	Horror,
	YoungAdult,
	LiteraryFiction,
	Dystopian,
	Adventure,
	Crime,
	MagicalRealism,
	Contemporary,
	Western
}