using Microsoft.SemanticKernel.Plugins.OpenApi;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace SkPluginLibrary.Plugins;

public class DndPlugin
{
    private readonly Kernel _kernel;
    private readonly KernelFunction? _summarizeMonsterFunction;


    public DndPlugin(Kernel kernel)
    {
            _kernel = kernel;
            
        }

    public DndPlugin()
    {
            _kernel = CoreKernelService.ChatCompletionKernel();
           
        }
    [KernelFunction("RollDice"),
     Description(
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

    [KernelFunction("GenerateMonsterDescription"),
     Description("Generate a random monster from the Dungeons and Dragons 5e Monster Manual")]
    public async Task<string> GenerateMonsterDescription(string input, KernelArguments arguments)
    {
            Console.WriteLine("Starting generate monster");
            var dndApiSkill =
                await _kernel.ImportPluginFromOpenApiAsync("DndApiPlugin", Path.Combine(RepoFiles.ApiPluginDirectoryPath, "DndApiPlugin", "openapi.json"));
            var random = new Random();
            if (!arguments.ContainsName("challenge_rating"))
            {
                var rating = random.Next(3, 12);
                arguments["challenge_rating"] = JsonSerializer.Serialize(new List<string> { rating.ToString() }.ToArray());
            }

            var monstersResult = await _kernel.InvokeAsync(dndApiSkill["Monsters"], arguments);
            try
            {
                var result = monstersResult.GetValue<object>();
                var monsterList = new MonsterList() { Count = 1, Monsters =
                    [new Monster {Index = "young-black-dragon", Name = "Young Black Dragon"}]
                };
                if (result is RestApiOperationResponse restApiResponse)
                {
                    var content = restApiResponse.Content.ToString();
                    monsterList = JsonSerializer.Deserialize<MonsterList>(content);
                }
                var index = random.Next(0, monsterList.Monsters.Count);
                var monsterResult = monsterList.Monsters[index];
                var summary = await _kernel.InvokeAsync(_summarizeMonsterFunction, new KernelArguments() { { "input", monsterResult} });
                //var summary = await _summarizeMonsterFunction.InvokeAsync(monsterResult.Name, context);
                return summary.Result();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            return Regex.Unescape(monstersResult.Result());
        }
        

    [KernelFunction("StorySynopsis"),
     Description("Generate a brief 1-2 sentance summary of the character details to aid in story creation")]
    public async Task<string> StorySynopsisAsync([Description("Race, class, alignment and other character details")]string characterDetails)
    {
            var args = new KernelArguments
            {
                ["characterDetails"] = characterDetails
            };
            var synopsisFunction = _kernel.CreateFunctionFromPrompt(StorySynopsisPrompt, executionSettings:new OpenAIPromptExecutionSettings {ChatSystemPrompt = "You are a story synopsis generator", Temperature = 1.0, TopP = 1.0, MaxTokens = 256});
            var result = await _kernel.InvokeAsync(synopsisFunction, args);
            return result.Result();
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

public partial class DndApiResponse
{
    [JsonPropertyName("content")]
    public MonsterList Content { get; set; }

    [JsonPropertyName("contentType")]
    public string ContentType { get; set; }
}

public partial class MonsterList
{
    [JsonPropertyName("count")]
    public long Count { get; set; }

    [JsonPropertyName("results")]
    public List<Monster> Monsters { get; set; }
}

public partial class Monster
{
    [JsonPropertyName("index")]
    public string Index { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; }
}