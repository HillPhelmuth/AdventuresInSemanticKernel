using System.Reflection;
using System.Text.Json;

namespace SkPluginLibrary.Models.Helpers;

public class ConfigurationHelper
{
    public static List<ConfigurationSection> GetConfigurationSections()
    {
        var parentType = typeof(TestConfiguration);
        var types = parentType.GetNestedTypes();
        var resultList = new List<ConfigurationSection>();
        foreach (var type in types)
        {
            var configSection = new ConfigurationSection(type.Name, type, GetStaticPropertyValue(type.Name.Replace("Config", "")));
            var properties = GetInstanceProperties(type);
            configSection.ConfigurationProperties = new List<ConfigurationProperty>();
            foreach (var prop in properties)
            {
                var propValue = GetInstancePropertyValue(configSection.Instance, prop.Name);
                configSection.ConfigurationProperties.Add(prop with { Value = propValue });
            }

            resultList.Add(configSection);
        }
        return resultList;

    }

    public static void SetConfigurationSection(ConfigurationSection section)
    {
        var type = section.Type;
        var instance = section.Instance;
        var properties = section.ConfigurationProperties;
        foreach (var property in properties)
        {
            var value = property.Value;
            SetInstancePropertyValue(instance, property.Name, value);
            Console.WriteLine($"Config Property {property.Name} of type {section.Name} set to {value}");
        }
        SetStaticPropertyValue(type.Name.Replace("Config", ""), instance);
        var json = JsonSerializer.Serialize(TestConfiguration.Qdrant, new JsonSerializerOptions { WriteIndented = true });
        Console.WriteLine($"Qdrant set to:\n{json}");

    }
    private static void SetStaticPropertyValue(string propertyName, object value)
    {
        var type = typeof(TestConfiguration);
        var property = type.GetProperty(propertyName, BindingFlags.Static | BindingFlags.Public);
        if (property != null && property.CanWrite)
        {
            property.SetValue(null, value, null);
        }
    }
    private static IEnumerable<ConfigurationProperty> GetInstanceProperties(Type type)
    {
        var properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
        return properties.Select(x => new ConfigurationProperty(x.Name));
    }

    private static void SetInstancePropertyValue(object obj, string propertyName, object value)
    {
        var property = obj.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        if (property != null && property.CanWrite)
        {
            property.SetValue(obj, value, null);
        }
    }
    private static object GetStaticPropertyValue(string propertyName)
    {
        var type = typeof(TestConfiguration);
        var property = type.GetProperty(propertyName, BindingFlags.Static | BindingFlags.Public);
        return property?.GetValue(null, null);
    }

    private static string? GetInstancePropertyValue(object obj, string propertyName)
    {
        if (obj == null) throw new ArgumentNullException(nameof(obj), $"obj is null for property {propertyName}");

        var property = obj.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        return property?.GetValue(obj, null)?.ToString();
    }
    public static T CreateInstanceOf<T>() where T : new()
    {
        return new T();
    }

    public static object CreateInstanceOf(Type type)
    {
        if (type == null)
            throw new ArgumentNullException(nameof(type));

        return Activator.CreateInstance(type);
    }

}

public record ConfigurationSection(string Name, Type Type, object Instance)
{
    public List<ConfigurationProperty>? ConfigurationProperties { get; set; }
}

public record ConfigurationProperty(string Name)
{
    public string? Value { get; set; }
}