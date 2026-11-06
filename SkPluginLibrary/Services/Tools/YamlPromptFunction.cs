using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;
using SemanticKernelAgentOrchestration.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SkPluginLibrary.Services;

/// <summary>
/// A prompt template defined in YAML, exposed as an <see cref="AIFunction"/> tool. Invoking it renders the
/// template with the supplied arguments and sends it to a chat client. This keeps the app's YAML prompt
/// "plugins" usable as Agent Framework tools.
/// </summary>
public sealed partial class YamlPromptFunction : AIFunction
{
    private readonly PromptYaml _config;
    private readonly Func<IChatClient> _clientFactory;
    private IChatClient? _client;
    private readonly JsonElement _schema;

    private YamlPromptFunction(PromptYaml config, Func<IChatClient> clientFactory)
    {
        _config = config;
        _clientFactory = clientFactory;
        _schema = BuildSchema(config);
    }

    public override string Name => _config.Name ?? "PromptFunction";
    public override string Description => _config.Description ?? "";
    public override JsonElement JsonSchema => _schema;
    public IReadOnlyList<PromptInput> Inputs => _config.InputVariables ?? [];
    public string Template => _config.Template ?? "";

    public static YamlPromptFunction FromYaml(string yaml, Func<IChatClient>? clientFactory = null)
    {
        var deserializer = new DeserializerBuilder().WithNamingConvention(UnderscoredNamingConvention.Instance).IgnoreUnmatchedProperties().Build();
        var config = deserializer.Deserialize<PromptYaml>(yaml);
        return new YamlPromptFunction(config, clientFactory ?? (() => ChatClientFactory.Create(AIModel.Gpt41Mini)));
    }

    protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var prompt = Render(arguments);
        _client ??= _clientFactory();
        var settings = _config.ExecutionSettings?.GetValueOrDefault("default");
        var options = new ChatOptions();
        if (settings is not null)
        {
            if (settings.Temperature is { } t) options.Temperature = (float)t;
            if (settings.TopP is { } p) options.TopP = (float)p;
            if (settings.MaxTokens is { } m) options.MaxOutputTokens = m;
            if (settings.PresencePenalty is { } pp) options.PresencePenalty = (float)pp;
            if (settings.FrequencyPenalty is { } fp) options.FrequencyPenalty = (float)fp;
            if (settings.StopSequences is { Count: > 0 } s) options.StopSequences = s;
        }
        var response = await _client.GetResponseAsync(prompt, options, cancellationToken);
        return response.Text;
    }

    /// <summary>Replaces {{$name}} placeholders with argument values (or the input variable's default).</summary>
    public string Render(IDictionary<string, object?> arguments)
    {
        return PlaceholderRegex().Replace(_config.Template ?? "", match =>
        {
            var name = match.Groups[1].Value;
            if (arguments.TryGetValue(name, out var value) && value is not null)
                return value is JsonElement { ValueKind: JsonValueKind.String } json ? json.GetString()! : value.ToString() ?? "";
            return _config.InputVariables?.FirstOrDefault(v => v.Name == name)?.Default ?? "";
        });
    }

    private static JsonElement BuildSchema(PromptYaml config)
    {
        var properties = new Dictionary<string, object>();
        var required = new List<string>();
        foreach (var input in config.InputVariables ?? [])
        {
            if (string.IsNullOrEmpty(input.Name)) continue;
            properties[input.Name] = new Dictionary<string, object> { ["type"] = "string", ["description"] = input.Description ?? "" };
            if (input.IsRequired) required.Add(input.Name);
        }
        var schema = new Dictionary<string, object> { ["type"] = "object", ["properties"] = properties, ["required"] = required };
        return JsonSerializer.SerializeToElement(schema);
    }

    [GeneratedRegex(@"\{\{\s*\$(\w+)\s*\}\}")]
    private static partial Regex PlaceholderRegex();

    public class PromptYaml
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Template { get; set; }
        public List<PromptInput>? InputVariables { get; set; }
        public Dictionary<string, PromptSettings>? ExecutionSettings { get; set; }
    }

    public class PromptInput
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Default { get; set; }
        public bool IsRequired { get; set; }
    }

    public class PromptSettings
    {
        public double? Temperature { get; set; }
        public double? TopP { get; set; }
        public int? MaxTokens { get; set; }
        public double? PresencePenalty { get; set; }
        public double? FrequencyPenalty { get; set; }
        public List<string>? StopSequences { get; set; }
    }
}
