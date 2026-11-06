using System.ComponentModel;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using HtmlAgilityPack;

namespace AgentFrameworkLibrary;

public sealed class ApiTools(IHttpClientFactory factory, RuntimeConfiguration config)
{
    [Description("Search Wikipedia for articles. Returns article titles, excerpts and source links.")]
    public async Task<IReadOnlyList<WikiSearchResult>> WikipediaAsync(string query, CancellationToken cancellationToken = default)
    {
        var url = "https://en.wikipedia.org/w/api.php?action=query&list=search&format=json&srsearch=" + Uri.EscapeDataString(query);
        var result = await GetAsync(url, cancellationToken);
        return result.GetProperty("query").GetProperty("search").EnumerateArray().Select(item => new WikiSearchResult(
            item.GetProperty("title").GetString()!, item.GetProperty("snippet").GetString()!,
            "https://en.wikipedia.org/wiki/" + Uri.EscapeDataString(item.GetProperty("title").GetString()!))).ToArray();
    }

    [Description("Read a Wikipedia article by title, including its plain text and canonical source URL.")]
    public Task<JsonElement> WikipediaArticleAsync(string title, CancellationToken cancellationToken = default) =>
        GetAsync("https://en.wikipedia.org/w/api.php?action=query&prop=extracts%7Cinfo&inprop=url&explaintext=1&redirects=1&format=json&titles=" + Uri.EscapeDataString(title), cancellationToken);

    [Description("Evaluate a mathematical expression using the Math.js API.")]
    public async Task<JsonElement> MathAsync(string expression, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expression);
        if (expression.Length > 2048) throw new ArgumentException("Math expressions are limited to 2048 characters.");
        using var client = factory.CreateClient("public-web");
        using var response = await client.PostAsJsonAsync("https://api.mathjs.org/v4/", new { expr = expression, precision = 14 }, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
    }

    [Description("Search the web using the configured Google Programmable Search API. Returns source links and snippets.")]
    public async Task<JsonElement> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        var key = config.Get("Google:ApiKey");
        var engine = config.Get("Google:SearchEngineId");
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(engine))
            throw new InvalidOperationException("Web search requires Google:ApiKey and Google:SearchEngineId. Retired Bing Search is not used.");
        using var client = factory.CreateClient("public-web");
        client.DefaultRequestHeaders.Add("X-Goog-Api-Key", key);
        return await client.GetFromJsonAsync<JsonElement>($"https://www.googleapis.com/customsearch/v1?cx={Uri.EscapeDataString(engine)}&q={Uri.EscapeDataString(query)}", cancellationToken);
    }

    [Description("Read the text of a public HTTPS page. Returns its source URL and text.")]
    public async Task<string> ReadPageAsync(string url, CancellationToken cancellationToken = default)
    {
        var uri = new Uri(url);
        if (uri.Scheme != "https" || uri.Port != 443 || uri.UserInfo.Length != 0 || uri.IsLoopback || uri.HostNameType != UriHostNameType.Dns)
            throw new ArgumentException("Only public HTTPS hostnames are supported.");
        var addresses = await Dns.GetHostAddressesAsync(uri.Host, cancellationToken);
        if (addresses.Any(a => IPAddress.IsLoopback(a) || IsPrivate(a)))
            throw new ArgumentException("Private network addresses are not allowed.");
        using var client = factory.CreateClient("public-web");
        using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > 2_000_000) throw new InvalidOperationException("Page exceeds the 2 MB limit.");
        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var memory = new MemoryStream();
        var buffer = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (memory.Length + read > 2_000_000) throw new InvalidOperationException("Page exceeds the 2 MB limit.");
            memory.Write(buffer, 0, read);
        }
        var doc = new HtmlDocument();
        doc.LoadHtml(System.Text.Encoding.UTF8.GetString(memory.ToArray()));
        foreach (var node in doc.DocumentNode.SelectNodes("//script|//style") ?? Enumerable.Empty<HtmlNode>()) node.Remove();
        return $"Source: {uri}\n{HtmlEntity.DeEntitize(doc.DocumentNode.InnerText)}";
    }

    [Description("Read a D&D fifth edition resource, such as classes/wizard or spells/fireball.")]
    public Task<JsonElement> DndAsync(string resource, CancellationToken cancellationToken = default)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(resource, @"^[a-z0-9/-]+$") || resource.Contains(".."))
            throw new ArgumentException("Use a D&D resource path such as classes/wizard.");
        return GetAsync("https://www.dnd5eapi.co/api/2014/" + resource, cancellationToken);
    }

    [Description("Get the current weather for latitude and longitude from Open-Meteo.")]
    public Task<JsonElement> WeatherAsync(double latitude, double longitude, CancellationToken cancellationToken = default)
    {
        if (!double.IsFinite(latitude) || !double.IsFinite(longitude) || latitude is < -90 or > 90 || longitude is < -180 or > 180) throw new ArgumentOutOfRangeException(nameof(latitude));
        return GetAsync(FormattableString.Invariant($"https://api.open-meteo.com/v1/forecast?latitude={latitude}&longitude={longitude}&current=temperature_2m,wind_speed_10m"), cancellationToken);
    }
    public sealed record WikiSearchResult(string Title, string Excerpt, string Url);

    private async Task<JsonElement> GetAsync(string url, CancellationToken cancellationToken)
    {
        using var client = factory.CreateClient("public-web");
        return await client.GetFromJsonAsync<JsonElement>(url, cancellationToken);
    }

    private static bool IsPrivate(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        var b = address.GetAddressBytes();
        return b.Length == 4 ? b[0] is 0 or 10 or 127 || b[0] == 169 && b[1] == 254 ||
            b[0] == 100 && b[1] is >= 64 and <= 127 || b[0] == 198 && b[1] is 18 or 19 ||
            b[0] == 172 && b[1] is >= 16 and <= 31 || b[0] == 192 && b[1] == 168 ||
            b[0] >= 224 : b[0] is < 0x20 or > 0x3f;
    }

    public static SocketsHttpHandler CreatePublicHandler() => new()
    {
        AllowAutoRedirect = false,
        ConnectCallback = async (context, token) =>
        {
            var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, token);
            if (addresses.Length == 0 || addresses.Any(a => IPAddress.IsLoopback(a) || IsPrivate(a)))
                throw new HttpRequestException("The public HTTP tool cannot connect to private or unresolved addresses.");
            var address = addresses.OrderBy(a => a.AddressFamily != AddressFamily.InterNetwork).First();
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), token);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch { socket.Dispose(); throw; }
        }
    };
}
