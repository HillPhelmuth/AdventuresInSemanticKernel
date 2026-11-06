using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;
using SemanticKernelAgentOrchestration.Models;

namespace SkPluginLibrary.Services;

/// <summary>
/// Turns an OpenAPI (JSON) document into a set of <see cref="AIFunction"/> tools, one per operation, so any
/// REST API described by a spec can be handed to an Agent Framework agent.
/// </summary>
public static class OpenApiToolFactory
{
    private static readonly HttpClient SharedClient = new();

    public static async Task<ToolPlugin> FromFileAsync(string pluginName, string path, string? serverOverride = null,
        HttpClient? client = null, CancellationToken cancellationToken = default)
    {
        var json = await File.ReadAllTextAsync(path, cancellationToken);
        return FromJson(pluginName, json, serverOverride, client);
    }

    public static async Task<ToolPlugin> FromUrlAsync(string pluginName, string url, string? serverOverride = null,
        HttpClient? client = null, CancellationToken cancellationToken = default)
    {
        client ??= SharedClient;
        var json = await client.GetStringAsync(url, cancellationToken);
        return FromJson(pluginName, json, serverOverride ?? new Uri(new Uri(url), "/").ToString().TrimEnd('/'), client);
    }

    public static ToolPlugin FromJson(string pluginName, string json, string? serverOverride = null, HttpClient? client = null)
    {
        var doc = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true })!.AsObject();
        var baseUrl = serverOverride ?? doc["servers"]?.AsArray().FirstOrDefault()?["url"]?.GetValue<string>() ?? "";
        var defs = doc["components"]?["schemas"]?.DeepClone();
        var functions = new List<AIFunction>();
        foreach (var (path, pathItem) in doc["paths"]?.AsObject() ?? [])
        {
            var pathParams = pathItem?["parameters"]?.AsArray();
            foreach (var (method, op) in pathItem?.AsObject() ?? [])
            {
                if (method is not ("get" or "post" or "put" or "delete" or "patch") || op is null) continue;
                functions.Add(new OpenApiOperationFunction(pluginName, baseUrl, path, method, op.AsObject(), pathParams, defs, client ?? SharedClient));
            }
        }
        var description = doc["info"]?["description"]?.GetValue<string>();
        return new ToolPlugin(pluginName, functions, description);
    }

    internal static string Sanitize(string name) => new(name.Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_').ToArray());

    private sealed class OpenApiOperationFunction : AIFunction
    {
        private readonly string _baseUrl, _path, _method;
        private readonly List<(string Name, string In, bool Required)> _parameters = [];
        private readonly bool _hasBody;
        private readonly HttpClient _client;
        private readonly JsonElement _schema;

        public OpenApiOperationFunction(string plugin, string baseUrl, string path, string method, JsonObject op,
            JsonArray? pathParams, JsonNode? defs, HttpClient client)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _path = path;
            _method = method;
            _client = client;
            var operationId = op["operationId"]?.GetValue<string>() ?? $"{method}_{path}";
            Name = Sanitize(operationId);
            Description = op["summary"]?.GetValue<string>() ?? op["description"]?.GetValue<string>() ?? Name;

            var properties = new JsonObject();
            var required = new JsonArray();
            foreach (var p in (pathParams ?? []).Concat(op["parameters"]?.AsArray() ?? []))
            {
                if (p is null) continue;
                var name = p["name"]!.GetValue<string>();
                var location = p["in"]!.GetValue<string>();
                var isRequired = p["required"]?.GetValue<bool>() ?? location == "path";
                _parameters.Add((name, location, isRequired));
                var schema = p["schema"]?.DeepClone() as JsonObject ?? new JsonObject { ["type"] = "string" };
                if (p["description"] is { } d) schema["description"] = d.GetValue<string>();
                properties[name] = schema;
                if (isRequired) required.Add(name);
            }
            var bodySchema = op["requestBody"]?["content"]?["application/json"]?["schema"];
            if (bodySchema is not null)
            {
                _hasBody = true;
                properties["body"] = bodySchema.DeepClone();
                if (op["requestBody"]?["required"]?.GetValue<bool>() == true) required.Add("body");
            }
            var root = new JsonObject { ["type"] = "object", ["properties"] = properties, ["required"] = required };
            if (defs is not null) root["$defs"] = defs.DeepClone();
            // Component references point at #/components/schemas; relocate them to the local $defs.
            _schema = JsonSerializer.SerializeToElement(JsonNode.Parse(root.ToJsonString().Replace("#/components/schemas/", "#/$defs/")));
        }

        public override string Name { get; }
        public override string Description { get; }
        public override JsonElement JsonSchema => _schema;

        protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
        {
            var path = _path;
            var query = new List<string>();
            var headers = new List<(string, string)>();
            foreach (var (name, location, _) in _parameters)
            {
                if (!arguments.TryGetValue(name, out var raw) || raw is null) continue;
                var value = raw is JsonElement { ValueKind: JsonValueKind.String } s ? s.GetString()! : raw is JsonElement e ? e.GetRawText() : raw.ToString()!;
                switch (location)
                {
                    case "path": path = path.Replace($"{{{name}}}", Uri.EscapeDataString(value)); break;
                    case "query": query.Add($"{Uri.EscapeDataString(name)}={Uri.EscapeDataString(value)}"); break;
                    case "header": headers.Add((name, value)); break;
                }
            }
            var url = _baseUrl + path + (query.Count > 0 ? "?" + string.Join("&", query) : "");
            using var request = new HttpRequestMessage(new HttpMethod(_method.ToUpperInvariant()), url);
            foreach (var (h, v) in headers) request.Headers.TryAddWithoutValidation(h, v);
            if (_hasBody && arguments.TryGetValue("body", out var body) && body is not null)
            {
                var bodyJson = body is JsonElement je ? je.GetRawText() : JsonSerializer.Serialize(body);
                request.Content = new StringContent(bodyJson, Encoding.UTF8, "application/json");
            }
            using var response = await _client.SendAsync(request, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return response.IsSuccessStatusCode ? content : $"Request failed ({(int)response.StatusCode}): {content}";
        }
    }
}
