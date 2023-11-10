using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Functions.OpenAPI.Extensions;
using Microsoft.SemanticKernel.Functions.OpenAPI.Model;
using Microsoft.SemanticKernel.Orchestration;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace SkPluginLibrary.Plugins
{
    public class DndPlugin
    {
        private readonly IKernel _kernel;
        private readonly ISKFunction? _summarizeMonsterFunction;


        public DndPlugin(IKernel kernel)
        {
            _kernel = kernel;
            _summarizeMonsterFunction =
                kernel.ImportSemanticFunctionsFromDirectory(RepoFiles.PluginDirectoryPath, "WriterPlugin")["MonsterGen"];
        }

        public DndPlugin()
        {
            _kernel = CoreKernelService.ChatCompletionKernel("gpt-3.5-turbo-1106");
            _summarizeMonsterFunction =
                _kernel.ImportSemanticFunctionsFromDirectory(RepoFiles.PluginDirectoryPath, "WriterPlugin")["MonsterGen"];
        }
        [SKFunction, SKName("RollDice"),
         Description(
             "Roll dice using [count]D[value]+[bonus] where [count] is number of rolls, and [value] is sides on the die. (examples: 3D6, 2D4, 1D20, etc.)")]
        public string RollDice([Description("must be in nDv format like 3D6 or 3D6+1")] string countDValue)
        {
            if (!countDValue.Contains('d', StringComparison.InvariantCultureIgnoreCase))
                throw new ArgumentException("Invalid dice format");
            var dValue = countDValue.Contains('+') ? countDValue.Split('+')[0] : countDValue;
            var bValue = countDValue.Contains('+') ? countDValue.Split('+')[1] : "0";
            var countValueArray = dValue.ToLower().Split('d');
            var hasCount = int.TryParse(countValueArray[0], out var count);
            count = hasCount ? count : 1;
            var hasValue = int.TryParse(countValueArray[1], out var value);
            value = hasValue ? value : 6;
            var total = 0;
            for (var i = 0; i < count; i++)
            {
                var random = new Random();
                var diceValue = random.Next(1, value + 1);
                total += diceValue;
            }

            if (int.TryParse(bValue, out var bonus))
            {
                total += bonus;
            }

            return total.ToString();
        }

        [SKFunction, SKName("GenerateMonsterDescription"),
         Description("Generate a random monster from the Dungeons and Dragons 5e Monster Manual")]
        public async Task<string> GenerateMonsterDescription(string input, SKContext context)
        {
            Console.WriteLine("Starting generate monster");
            var dndApiSkill =
                await _kernel.ImportPluginFunctionsAsync("DndApiPlugin", Path.Combine(RepoFiles.ApiPluginDirectoryPath, "DndApiPlugin", "openapi.json"));
            var random = new Random();
            if (!context.Variables.ContainsKey("challenge_rating"))
            {
                var rating = random.Next(3, 12);
                context.Variables.Set("challenge_rating", JsonSerializer.Serialize(new List<string> { rating.ToString() }.ToArray()));
            }

            var monstersResult = await _kernel.RunAsync(context.Variables, dndApiSkill["Monsters"]);
            try
            {
                var result = monstersResult.GetValue<object>();
                var monsterList = new MonsterList() { Count = 1, Results = new List<Result> { new() { Index = "young-black-dragon", Name = "Young Black Dragon" } } };
                if (result is RestApiOperationResponse restApiResponse)
                {
                    var content = restApiResponse.Content.ToString();
                    monsterList = JsonSerializer.Deserialize<MonsterList>(content);
                }
                var index = random.Next(0, monsterList.Results.Count);
                var monsterResult = monsterList.Results[index];
                var summary = await _kernel.RunAsync(monsterResult.Name, _summarizeMonsterFunction);
                //var summary = await _summarizeMonsterFunction.InvokeAsync(monsterResult.Name, context);
                return summary.Result();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            return Regex.Unescape(monstersResult.Result());
        }
        [SKFunction, SKName("DndMonster"), Description("Get the D&D monster")]
        public string Monster(SKContext context)
        {
            return context.Variables.TryGetValue("monster", out var monster) ? monster : "young-black-dragon";
        }

        [SKFunction, SKName("StorySynopsis"),
         Description("Generate a brief 1-2 sentance summary of the character details to aid in story creation")]
        public async Task<string> StorySynopsisAsync(SKContext context)
        {
            var synopsisFunction = _kernel.CreateSemanticFunction(StorySynopsisPrompt);
            var result = await _kernel.RunAsync(context.Variables, synopsisFunction);
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
        public List<Result> Results { get; set; }
    }

    public partial class Result
    {
        [JsonPropertyName("index")]
        public string Index { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }
    }
}
