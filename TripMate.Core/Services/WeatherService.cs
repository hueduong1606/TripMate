using System.Text.Json;

namespace TripMate.Core.Services
{
    // One day of the weather forecast
    public class WeatherDay
    {
        public DateTime Date { get; set; }
        public string Conditions { get; set; } = "";
        public double MinTemp { get; set; }
        public double MaxTemp { get; set; }
        public int? RainChance { get; set; }

        public string RainText
        {
            get { return RainChance.HasValue ? RainChance.Value + "%" : "-"; }
        }
    }

    public class WeatherService : IWeatherService
    {
        // Method to get the daily forecast from Open-Meteo (free, no API key, up to 16 days)
        public async Task<List<WeatherDay>> GetForecastAsync(double latitude, double longitude, int days)
        {
            days = Math.Clamp(days, 1, 16);
            string url = "https://api.open-meteo.com/v1/forecast" +
                         "?latitude=" + ApiClient.Num(latitude) +
                         "&longitude=" + ApiClient.Num(longitude) +
                         "&daily=weather_code,temperature_2m_max,temperature_2m_min,precipitation_probability_max" +
                         "&timezone=auto&forecast_days=" + days;

            List<WeatherDay> forecast = new List<WeatherDay>();
            using (JsonDocument doc = await ApiClient.GetJsonAsync(url))
            {
                // The "daily" object holds one array per value, all the same length
                JsonElement daily = doc.RootElement.GetProperty("daily");
                JsonElement dates = daily.GetProperty("time");
                JsonElement codes = daily.GetProperty("weather_code");
                JsonElement maxTemps = daily.GetProperty("temperature_2m_max");
                JsonElement minTemps = daily.GetProperty("temperature_2m_min");
                JsonElement rain = daily.GetProperty("precipitation_probability_max");

                for (int i = 0; i < dates.GetArrayLength(); i++)
                {
                    forecast.Add(new WeatherDay
                    {
                        Date = DateTime.Parse(dates[i].GetString()!),
                        Conditions = Describe(codes[i].ValueKind == JsonValueKind.Number ? codes[i].GetInt32() : -1),
                        MaxTemp = maxTemps[i].ValueKind == JsonValueKind.Number ? maxTemps[i].GetDouble() : 0,
                        MinTemp = minTemps[i].ValueKind == JsonValueKind.Number ? minTemps[i].GetDouble() : 0,
                        RainChance = rain[i].ValueKind == JsonValueKind.Number ? (int)rain[i].GetDouble() : null
                    });
                }
            }
            return forecast;
        }

        // Method to turn a WMO weather code into words
        public static string Describe(int code)
        {
            if (code == 0) return "Clear sky";
            if (code == 1 || code == 2) return "Partly cloudy";
            if (code == 3) return "Overcast";
            if (code == 45 || code == 48) return "Fog";
            if (code >= 51 && code <= 57) return "Drizzle";
            if (code >= 61 && code <= 67) return "Rain";
            if (code >= 71 && code <= 77) return "Snow";
            if (code >= 80 && code <= 82) return "Rain showers";
            if (code == 85 || code == 86) return "Snow showers";
            if (code >= 95) return "Thunderstorm";
            return "Unknown";
        }
    }
}