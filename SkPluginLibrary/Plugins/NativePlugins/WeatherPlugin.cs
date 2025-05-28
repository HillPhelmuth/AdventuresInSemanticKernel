using System.Text;
using OpenMeteo;

namespace SkPluginLibrary.Plugins.NativePlugins;

public class WeatherPlugin
{
    private OpenMeteoClient _client = new();

    [KernelFunction, Description("Gets the current weather for a location.")]
    public async Task<string> GetWeather(
        [Description(
            "Pass US Zipcode, UK Postcode, Canada Postalcode, IP address, Latitude/Longitude (decimal degree) or city name to get current weather for that location")]
        string location)
    {
        location = Uri.EscapeDataString(location);
        var weatherData = await _client.QueryAsync(location, new WeatherForecastOptions { Temperature_Unit = TemperatureUnitType.fahrenheit, Current = CurrentOptions.All, Daily = DailyOptions.All});
        var weather = weatherData.ToMarkdown(location);
        return weather;
    }
}
internal static class WeatherPluginExtensions
{
    public static string ToMarkdown(this WeatherForecast weatherForecast, string location)
    {
        var weather = weatherForecast.Current;
        var weatherForecastCurrentUnits = weatherForecast.CurrentUnits!;
        var sb = new StringBuilder();
        sb.AppendLine($"The weather in {location} is:");
        sb.AppendLine($"- Weather Condition: {weather.Weathercode?.WeathercodeDescription()}");
        sb.AppendLine($"- Temperature: {weather.Temperature}{weatherForecastCurrentUnits.Temperature}");
        sb.AppendLine($"- Feels Like: {weather.Apparent_temperature}{weatherForecastCurrentUnits.Apparent_temperature}");
        sb.AppendLine($"- Wind Speed: {weather.Windspeed_10m} {weatherForecastCurrentUnits.Windspeed_10m}");
        sb.AppendLine($"- Wind Direction: {weather.Winddirection_10m} {weatherForecastCurrentUnits.Winddirection_10m}");
        sb.AppendLine($"- Humidity: {weather.Relativehumidity_2m} {weatherForecastCurrentUnits.Relativehumidity_2m}");
        sb.AppendLine($"- Precipitation: {weather.Precipitation} {weatherForecastCurrentUnits.Precipitation}");
        sb.AppendLine($"- Pressure: {weather.Pressure_msl} {weatherForecastCurrentUnits.Pressure_msl}");
        sb.AppendLine("");
        sb.AppendLine($"{weatherForecast.ConvertWeatherForecastToMarkdownTable()}");
        return sb.ToString();
    }
    public static string ConvertWeatherForecastToMarkdownTable(this WeatherForecast forecast)
    {
        var sb = new StringBuilder();
        sb.AppendLine("| Date       | Condition | Max Temp (°F) | Min Temp (°F) | Apparent Max Temp (°F) | Apparent Min Temp (°F) | Precipitation (mm) | Wind Speed (mph) | Sunrise  | Sunset  |");
        sb.AppendLine("|------------|---------------|---------------|---------------|------------------------|------------------------|--------------------|------------------|----------|---------|");

        for (int i = 0; i < forecast.Daily.Time.Length; i++)
        {
            sb.AppendLine($"| {forecast.Daily.Time[i]} | {forecast.Daily.Weathercode?[i].WeathercodeDescription()} | {forecast.Daily.Temperature_2m_max?[i]} | {forecast.Daily.Temperature_2m_min?[i]} | {forecast.Daily.Apparent_temperature_max?[i]} | {forecast.Daily.Apparent_temperature_min?[i]} | {forecast.Daily.Precipitation_sum?[i]} | {forecast.Daily.Windspeed_10m_max?[i]} | {(forecast.Daily.Sunrise?[i])?[11..]} | {(forecast.Daily.Sunset?[i])?[11..]} |");
        }

        return sb.ToString();
    }
    public static float ToFahrenheit(this float? celsius)
    {
        if (celsius == null)
            return 0;
        return celsius.Value * 9 / 5 + 32;
    }

    public static string WeathercodeDescription(this float weathercode)
    {
        if (weathercode == null)
            return "Invalid weathercode";
        var code = (int)weathercode;
        return code.WeathercodeDescription();
    }
    public static string WeathercodeDescription(this int weathercode)
    {
        //var weathercode = current.Weathercode;
        switch (weathercode)
        {
            case 0:
                return "Clear sky";
            case 1:
                return "Mainly clear";
            case 2:
                return "Partly cloudy";
            case 3:
                return "Overcast";
            case 45:
                return "Fog";
            case 48:
                return "Depositing rime Fog";
            case 51:
                return "Light drizzle";
            case 53:
                return "Moderate drizzle";
            case 55:
                return "Dense drizzle";
            case 56:
                return "Light freezing drizzle";
            case 57:
                return "Dense freezing drizzle";
            case 61:
                return "Slight rain";
            case 63:
                return "Moderate rain";
            case 65:
                return "Heavy rain";
            case 66:
                return "Light freezing rain";
            case 67:
                return "Heavy freezing rain";
            case 71:
                return "Slight snow fall";
            case 73:
                return "Moderate snow fall";
            case 75:
                return "Heavy snow fall";
            case 77:
                return "Snow grains";
            case 80:
                return "Slight rain showers";
            case 81:
                return "Moderate rain showers";
            case 82:
                return "Violent rain showers";
            case 85:
                return "Slight snow showers";
            case 86:
                return "Heavy snow showers";
            case 95:
                return "Thunderstorm";
            case 96:
                return "Thunderstorm with light hail";
            case 99:
                return "Thunderstorm with heavy hail";
            default:
                return "Invalid weathercode";
        }
    }
}