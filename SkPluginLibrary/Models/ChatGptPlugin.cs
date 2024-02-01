using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SkPluginLibrary.Models
{
    public class ChatGptPluginManifest
    {
        [JsonPropertyName("schema_version")]
        public string? SchemaVersion { get; set; }

        [JsonPropertyName("name_for_human")]
        public string? NameForHuman { get; set; }

        [JsonPropertyName("name_for_model")]
        public string? NameForModel { get; set; }

        [JsonPropertyName("description_for_human")]
        public string? DescriptionForHuman { get; set; }

        [JsonPropertyName("description_for_model")]
        public string? DescriptionForModel { get; set; }

        [JsonPropertyName("auth")]
        public Auth? Auth { get; set; }

        [JsonPropertyName("api")]
        public Api? Api { get; set; }

        [JsonPropertyName("logo_url")]
        public Uri? LogoUrl { get; set; }

        [JsonPropertyName("contact_email")]
        public string? ContactEmail { get; set; }

        [JsonPropertyName("legal_info_url")]
        public Uri? LegalInfoUrl { get; set; }
        [JsonPropertyName("override_url")]
        public string? OverrideUrl { get; set; }
        public static List<ChatGptPluginManifest> GetAllPluginManifests() => FileHelper.ExtractFromAssembly<List<ChatGptPluginManifest>>("ChatGptPluginManifests.json");

        public static async Task GetAndSaveAllNonAuthMantifestFiles()
        {
            var nonAuth = GetAllPluginManifests().Where(x => x.Auth.TypeEnum == TypeEnum.None).ToList();
            await File.WriteAllTextAsync("ChatGptPluginManifests.json", JsonSerializer.Serialize(nonAuth, new JsonSerializerOptions { WriteIndented = true }));

        }
    }

    public class Api
    {
        [JsonPropertyName("url")]
        public Uri? Url { get; set; }
     
    }

    public class Auth
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }
        [JsonIgnore]
        public TypeEnum TypeEnum => Type is null ? TypeEnum.None : EnumExtensions.GetEnumValueFromDescription<TypeEnum>(Type);
    }
    public enum TypeEnum
    {
        [Description("none")]
        None,
        [Description("api_key")]
        ApiKey,

        [Description("oauth")]
        Oauth,

        [Description("service_http")]
        ServiceHttp,

        [Description("user_http")]
        UserHttp
    }
}
