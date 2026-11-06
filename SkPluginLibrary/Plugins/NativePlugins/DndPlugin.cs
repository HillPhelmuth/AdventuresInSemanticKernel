using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;
using SkPluginLibrary.Services;

namespace SkPluginLibrary.Plugins.NativePlugins;

public class DndPlugin
{
    private readonly IChatClient _chatClient;

    public DndPlugin(IChatClient chatClient)
    {
        _chatClient = chatClient;
    }

    public DndPlugin() : this(ChatClientFactory.Create(AIModel.Gpt41Mini))
    {
    }
    [Description(
         "Roll dice using [count]D[value]+[bonus] where [count] is number of rolls, and [value] is sides on the die. (examples: 3D6, 2D4, 1D20, etc.)")]
    [return:Description("The result of the total value of the dice plus bonus")]
    public string RollDice([Description("The number of dice to roll")] int diceCount,[Description("The value or faces on each die (e.g. for a 6 sided die, dieValue = 6)")] int dieValue,[Description("the value to add or subtract from each roll total")] int bonus = 0)
    {
            
            var total = 0;
            Console.WriteLine($"Count: {diceCount}, Value: {dieValue}");
            for (var i = 0; i < diceCount; i++)
            {
                var random = new Random();
                var diceValue = random.Next(1, dieValue + 1);
                total += diceValue;
            }
            total += bonus;
		

            return total.ToString();
        }

    [Description("Generate a random monster from the Dungeons and Dragons 5e Monster Manual")]
    public async Task<string> GenerateMonsterDescription(string input, string? challengeRating = null)
    {
        Console.WriteLine("Starting generate monster");
        var dndApiPlugin = await OpenApiToolFactory.FromFileAsync("DndApiPlugin", Path.Combine(RepoFiles.ApiPluginDirectoryPath, "DndApiPlugin", "openapi.json"));
        var random = new Random();
        var arguments = new Dictionary<string, object?>
        {
            ["challenge_rating"] = challengeRating ?? JsonSerializer.Serialize(new List<string> { random.Next(3, 12).ToString() }.ToArray())
        };
        var monstersResult = await dndApiPlugin.GetFunction("Monsters")!.InvokeToStringAsync(arguments);
        try
        {
            var monsterList = JsonSerializer.Deserialize<MonsterList>(monstersResult) ?? new MonsterList
            {
                Count = 1,
                Monsters = [new Monster { Index = "young-black-dragon", Name = "Young Black Dragon" }]
            };
            var monsterResult = monsterList.Monsters[random.Next(0, monsterList.Monsters.Count)];
            var summarizer = KernelPluginExtensions.CreatePluginFromPromptDirectoryYaml("DndApiPlugin", () => _chatClient).GetFunction("SummarizeMonster");
            if (summarizer is not null)
                return await summarizer.InvokeToStringAsync(new() { ["input"] = JsonSerializer.Serialize(monsterResult) });
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }

        return Regex.Unescape(monstersResult);
    }

    [Description("Generate a brief 1-2 sentance summary of the character details to aid in story creation")]
    public async Task<string> StorySynopsisAsync([Description("Race, class, alignment and other character details")]string characterDetails)
    {
        return await PromptRunner.RunAsync(_chatClient, StorySynopsisPrompt,
            new Dictionary<string, object?> { ["characterDetails"] = characterDetails },
            new ChatOptions { Temperature = 1.0f, TopP = 1.0f, MaxOutputTokens = 256 }, "You are a story synopsis generator");
    }

    private const string StorySynopsisPrompt = """
                                               Generate a 2 sentance story synopsis for the character based on [Character Details].
                                               If the character is evil, the story will be about their dark and evil deeds.
                                               If the character is neutral, the story will be about their harrowing adventures.
                                               If the character is good, the story will about their herioc deeds.

                                               [Character Details]
                                               {{$characterDetails}}
                                               story synopsis:

                                               """;
}

public class DndApiResponse
{
    [JsonPropertyName("content")]
    public MonsterList Content { get; set; }

    [JsonPropertyName("contentType")]
    public string ContentType { get; set; }
}

public class MonsterList
{
    [JsonPropertyName("count")]
    public long Count { get; set; }

    [JsonPropertyName("results")]
    public List<Monster> Monsters { get; set; }
}

public class Monster
{
    [JsonPropertyName("index")]
    public string Index { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; }
}