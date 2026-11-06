using System.ComponentModel;
using System.Text.Json;
using System.Text.RegularExpressions;
using AgentFrameworkOrchestration;
using AgentFrameworkLibrary.Services;
using AgentToolComponents;
using Microsoft.Extensions.AI;
using YamlDotNet.RepresentationModel;

namespace AgentFrameworkLibrary;

public sealed record ToolDefinition(string Group, AIFunction Function, bool RequiresApproval = false, string? Prompt = null)
{
    public string Name => Function.Name;
    public string Description => Function.Description;
    public AIFunction ForAgent() => RequiresApproval ? new ApprovalRequiredAIFunction(Function) : Function;
}

public sealed class ToolCatalog
{
    public event Action<string>? ToolProgress;
    public IReadOnlyList<ToolDefinition> Tools { get; }
    public IReadOnlyList<string> Groups => Tools.Select(t => t.Group).Distinct().Order().ToArray();

    public ToolCatalog(RuntimeConfiguration config, ApiTools apis, CompilerService compiler, ScriptService script,
        AskUserPlugin ask, BlazorReplPlugin blazor, Func<ModelSettings, IChatClient>? promptClient = null)
    {
        var tools = new List<ToolDefinition>
        {
            new("TimePlugin", AIFunctionFactory.Create(() => DateTimeOffset.UtcNow.ToString("O"), "TimePlugin_UtcNow", "Current UTC date and time.")),
            new("MathApiPlugin", AIFunctionFactory.Create((double a, double b) => a + b, "MathApiPlugin_Add", "Add two numbers.")),
            new("MathApiPlugin", AIFunctionFactory.Create((double a, double b) => a * b, "MathApiPlugin_Multiply", "Multiply two numbers.")),
            new("MathApiPlugin", AIFunctionFactory.Create(apis.MathAsync, "MathApiPlugin_Evaluate")),
            new("WikiChatPlugin", AIFunctionFactory.Create(apis.WikipediaAsync, "WikiChatPlugin_Search")),
            new("WikiChatPlugin", AIFunctionFactory.Create(apis.WikipediaArticleAsync, "WikiChatPlugin_GetArticle")),
            new("WebCrawlPlugin", AIFunctionFactory.Create(apis.SearchAsync, "WebCrawlPlugin_Search")),
            new("WebCrawlPlugin", AIFunctionFactory.Create(apis.ReadPageAsync, "WebCrawlPlugin_ReadPage")),
            new("DndPlugin", AIFunctionFactory.Create(apis.DndAsync, "DndPlugin_Lookup")),
            new("WeatherPlugin", AIFunctionFactory.Create(apis.WeatherAsync, "WeatherPlugin_Forecast")),
            new("AskUserPlugin", AIFunctionFactory.Create(ask.AskUserAsync, "AskUserPlugin_AskUser")),
            new("AskUserPlugin", AIFunctionFactory.Create(ask.TellUserAsync, "AskUserPlugin_TellUser")),
            new("CodeExecutorPlugin", AIFunctionFactory.Create(async (string input, CancellationToken cancellationToken) =>
                await compiler.SubmitCode(StripFence(input), CompileResources.PortableExecutableReferences, cancellationToken),
                "CodeExecutorPlugin_ExecuteCode", "Compile and execute a C# console program. Requires explicit approval."), true),
            new("CodeExecutorPlugin", AIFunctionFactory.Create(async (string input, CancellationToken cancellationToken) => await script.EvaluateAsync(StripFence(input), cancellationToken),
                "CodeExecutorPlugin_ExecuteScript", "Execute C# in the current REPL session. Requires explicit approval."), true),
            new("BlazorReplPlugin", AIFunctionFactory.Create(blazor.CreateBlazorApp, "BlazorReplPlugin_CreateBlazorApp"), true)
        };
        var directory = Path.Combine(AppContext.BaseDirectory, "Plugins", "YamlPlugins");
        if (Directory.Exists(directory))
        {
            foreach (var path in Directory.EnumerateFiles(directory, "*.yaml", SearchOption.AllDirectories)
                .Where(p => !p.Contains("AgenticPromptsPlugin") && !p.Contains("EvalPlugin")))
            {
                var yaml = new YamlStream();
                using var reader = File.OpenText(path);
                yaml.Load(reader);
                if (yaml.Documents[0].RootNode is not YamlMappingNode map ||
                    !map.Children.TryGetValue(new YamlScalarNode("template"), out var templateNode)) continue;
                var group = Directory.GetParent(path)!.Name;
                var name = Path.GetFileNameWithoutExtension(path);
                var template = ((YamlScalarNode)templateNode).Value ?? throw new InvalidDataException($"Empty template: {path}");
                var description = map.Children.TryGetValue(new YamlScalarNode("description"), out var desc) ? desc.ToString() : name;
                var defaults = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (map.Children.TryGetValue(new YamlScalarNode("input_variables"), out var variables) && variables is YamlSequenceNode inputs)
                    foreach (var node in inputs.OfType<YamlMappingNode>())
                        if (node.Children.TryGetValue(new YamlScalarNode("default"), out var value))
                            defaults[node.Children[new YamlScalarNode("name")].ToString()] = value.ToString();
                tools.Add(new ToolDefinition(group, new PromptFunction(config, $"{group}_{name}", description, template, defaults, promptClient), Prompt: template));
            }
        }
        if (tools.GroupBy(t => t.Name).Any(g => g.Count() > 1)) throw new InvalidDataException("Tool names must be unique.");
        Tools = tools.Select(t => t with { Function = new ProgressFunction(t.Function, text => ToolProgress?.Invoke(text)) }).ToList();
    }

    public IList<AITool> Resolve(AgentDefinition definition, ModelSettings settings)
    {
        var missing = definition.PluginNames.Except(Groups).ToArray();
        if (missing.Length != 0) throw new InvalidOperationException($"Unknown or retired tool groups: {string.Join(", ", missing)}");
        var selected = Tools.Where(t => definition.PluginNames.Contains(t.Group)).ToList();
        var invalidExclusions = definition.ExcludedFunctionNames.Where(n =>
            !selected.Any(t => t.Name == n || t.Name.EndsWith("_" + n, StringComparison.Ordinal))).ToList();
        if (invalidExclusions.Count != 0) throw new InvalidOperationException($"Unknown excluded functions: {string.Join(", ", invalidExclusions)}");
        selected.RemoveAll(t => definition.ExcludedFunctionNames.Any(n => t.Name == n || t.Name.EndsWith("_" + n, StringComparison.Ordinal)));
        if (selected.Count > 0 && !settings.SupportsTools) throw new NotSupportedException("The selected model is not configured for function tools.");
        return selected.Select(t => (AITool)t.ForAgent()).ToList();
    }

    public static string StripFence(string input) => Regex.Replace(input.Trim(), @"\A```(?:csharp|cs|json)?\s*|\s*```\z", "");

    private sealed class ProgressFunction(AIFunction inner, Action<string> report) : DelegatingAIFunction(inner)
    {
        protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
        {
            report($"Invoking {Name}");
            try
            {
                var result = await base.InvokeCoreAsync(arguments, cancellationToken);
                report($"Completed {Name}");
                return result;
            }
            catch (Exception ex)
            {
                report($"Failed {Name}: {ex.Message}");
                throw;
            }
        }
    }

    private sealed class PromptFunction : AIFunction
    {
        private readonly RuntimeConfiguration _config;
        private readonly string _name;
        private readonly string _description;
        private readonly string _template;
        private readonly string[] _variables;
        private readonly IReadOnlyDictionary<string, string> _defaults;
        private readonly Func<ModelSettings, IChatClient>? _client;
        private static readonly string[] ClockVariables = ["today", "now", "year"];
        public PromptFunction(RuntimeConfiguration config, string name, string description, string template,
            IReadOnlyDictionary<string, string> defaults, Func<ModelSettings, IChatClient>? client)
        {
            _config = config; _name = Regex.Replace(name, "[^a-zA-Z0-9_]", "_");
            _description = description; _template = template;
            _defaults = defaults; _client = client;
            _variables = Regex.Matches(template, @"\{\{\s*\$(\w+)\s*\}\}").Select(m => m.Groups[1].Value).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }
        public override string Name => _name;
        public override string Description => _description;
        public override JsonElement JsonSchema => JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = _variables.ToDictionary(v => v, _ => new { type = "string" }),
            required = _variables.Where(v => !_defaults.ContainsKey(v) && !ClockVariables.Contains(v)).ToArray()
        });
        protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
        {
            var prompt = _template;
            foreach (var variable in _variables)
            {
                var value = arguments.FirstOrDefault(kv => kv.Key.Equals(variable, StringComparison.OrdinalIgnoreCase)).Value?.ToString();
                value ??= variable switch { "today" => DateTime.UtcNow.ToString("yyyy-MM-dd"), "now" => DateTimeOffset.UtcNow.ToString("O"), "year" => DateTime.UtcNow.Year.ToString(), _ => _defaults.GetValueOrDefault(variable) };
                if (value is null) throw new ArgumentException($"Missing prompt input '{variable}'.");
                prompt = Regex.Replace(prompt, @"\{\{\s*\$" + Regex.Escape(variable) + @"\s*\}\}", _ => value, RegexOptions.IgnoreCase);
            }
            if (prompt.Contains("{{")) throw new NotSupportedException($"Prompt '{Name}' contains a retired inline tool expression. Use explicit function tools.");
            using var client = _client?.Invoke(_config.Current) ?? AgentFactory.CreateClient(_config.Current, new AgentDefinition());
            return (await client.GetResponseAsync(prompt, cancellationToken: cancellationToken)).Text;
        }
    }
}
