using System.Globalization;
using System.Text;
using System.Text.Json;
using DocumentFormat.OpenXml;
using Microsoft.Extensions.Logging;
using SkPluginLibrary.Services;

namespace SkPluginLibrary.Models;
[TypeConverter(typeof(TokenStringTypeConverter))]
public record TokenString
{
    public TokenString(string StringValue, double LogProb, int Token = 0)
    {
        this.Token = Token;
        this.LogProb = LogProb;
        this.StringValue = StringValue;
        NormalizedLogProbability = LogProb;
    }

    public string StringValue { get; set; }
    public List<TokenString> TopLogProbs { get; set; } = [];
    public double NormalizedLogProbability { get; set; }
    public int Token { get; init; }
    public double LogProb { get; init; }
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"| Property | Value |");
        sb.AppendLine($"| --- | --- |");
        sb.AppendLine($"| StringValue | {StringValue} |");
        sb.AppendLine($"| LogProb | {LogProb} |");
        sb.AppendLine($"| NormalizedLogProbability | {NormalizedLogProbability.ToString("P2")} |");
        sb.AppendLine($"| Token | {Token} |");
        sb.AppendLine();
        sb.AppendLine($"| TopLogProbs | Probability |");
        sb.AppendLine($"| --- | --- |");
        foreach (var topLogProb in TopLogProbs)
        {
            sb.AppendLine($"| {topLogProb.StringValue} | {topLogProb.NormalizedLogProbability.ToString("P2")} |");
        }
        return sb.ToString();
    }
}
public static class LoggerExtensions
{
    public static void LogInformation(this ILoggerFactory loggerFactory, string message, params object[] args)
    {
        loggerFactory.CreateLogger("Information").LogInformation(message, args);
    }
}

public class TokenStringTypeConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) => true;

    private async Task<object?> ConvertFromAsync(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string jsonString)
        {
            return await JsonSerializer.DeserializeAsync<TokenString>(new MemoryStream(Encoding.UTF8.GetBytes(jsonString)));
        }
        throw new NotSupportedException("Cannot convert from the given value.");
    }

    private async Task<object?> ConvertToAsync(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (value is TokenString tokenString && destinationType == typeof(string))
        {
            using var stream = new MemoryStream();
            await JsonSerializer.SerializeAsync(stream, tokenString);
            stream.Position = 0;
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync();
        }
        throw new NotSupportedException("Cannot convert to the given type.");
    }
    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string jsonString)
        {
            return ConvertFromAsync(context, culture, value).GetAwaiter().GetResult();
        }
        throw new NotSupportedException("Cannot convert from the given value.");
    }
    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (value is TokenString tokenString && destinationType == typeof(string))
        {
            return ConvertToAsync(context, culture, value, destinationType).GetAwaiter().GetResult();
        }
        throw new NotSupportedException("Cannot convert to the given type.");
    }
}