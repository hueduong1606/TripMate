using System.Globalization;
using System.Text.Json;

namespace TripMate.Core.Services
{
    // A place found by a location search
    public class GeoPlace
    {
        public string Name { get; set; } = "";
        public string Country { get; set; } = "";
        public string CountryCode { get; set; } = "";
        public double Latitude { get; set; }
        public double Longitude { get; set; }

        // Override the ToString() method to show "City, Country"
        public override string ToString()
        {
            return Name + (Country == "" ? "" : ", " + Country);
        }
    }

    public class GeocodingService
    {
        // Method to find a city (trip destination) using the Open-Meteo geocoding API
        // e.g. https://geocoding-api.open-meteo.com/v1/search?name=Tokyo&count=1
        public async Task<GeoPlace?> FindCityAsync(string cityName)
        {
            string url = "https://geocoding-api.open-meteo.com/v1/search?count=1&language=en&format=json&name=" + Uri.EscapeDataString(cityName);

            using (JsonDocument doc = await ApiClient.GetJsonAsync(url))
            {
                // "results" is missing when nothing was found
                if (!doc.RootElement.TryGetProperty("results", out JsonElement results) || results.GetArrayLength() == 0)
                {
                    return null;
                }

                JsonElement first = results[0];
                return new GeoPlace
                {
                    Name = first.GetProperty("name").GetString() ?? cityName,
                    Country = first.TryGetProperty("country", out JsonElement c) ? c.GetString() ?? "" : "",
                    CountryCode = first.TryGetProperty("country_code", out JsonElement cc) ? cc.GetString() ?? "" : "",
                    Latitude = first.GetProperty("latitude").GetDouble(),
                    Longitude = first.GetProperty("longitude").GetDouble()
                };
            }
        }

        // Method to find a specific place (museum, beach, cafe...) using OpenStreetMap Nominatim
        // e.g. https://nominatim.openstreetmap.org/search?q=Senso-ji, Tokyo&format=jsonv2&limit=1
        public async Task<GeoPlace?> FindPlaceAsync(string query)
        {
            string url = "https://nominatim.openstreetmap.org/search?format=jsonv2&limit=1&q=" + Uri.EscapeDataString(query);

            using (JsonDocument doc = await ApiClient.GetJsonAsync(url))
            {
                if (doc.RootElement.GetArrayLength() == 0)
                {
                    return null;
                }

                JsonElement first = doc.RootElement[0];
                // Nominatim returns the coordinates as strings
                return new GeoPlace
                {
                    Name = first.GetProperty("display_name").GetString() ?? query,
                    Latitude = double.Parse(first.GetProperty("lat").GetString()!, CultureInfo.InvariantCulture),
                    Longitude = double.Parse(first.GetProperty("lon").GetString()!, CultureInfo.InvariantCulture)
                };
            }
        }
    }
}