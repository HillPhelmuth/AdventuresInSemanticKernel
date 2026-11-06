namespace SkPluginLibrary.Examples;

/// <summary>Structured output: ask the agent for a typed result instead of parsing text.</summary>
public static class Maf05_StructuredOutput
{
    public record Movie(string Title, int Year, string Director, string[] Genres);

    public static async Task RunAsync()
    {
        AIAgent agent = new ChatClientAgent(ChatClientFactory.Create(AIModel.Gpt41Mini), instructions: "You are a film expert.", name: "FilmBuff");
        AgentResponse<Movie> response = await agent.RunAsync<Movie>("Tell me about the movie Blade Runner.");
        var movie = response.Result;
        Console.WriteLine($"{movie.Title} ({movie.Year}) directed by {movie.Director} - {string.Join(", ", movie.Genres)}");
    }
}
