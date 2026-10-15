using System.Globalization;
using System.Text.Json;

namespace TripMate.Core.Services
{
    // One shared HttpClient for every web API call in the app.
    // All the APIs used by TripMate are free and need no API key.
    public static class ApiClient
    {
        public static readonly HttpClient Http = CreateClient();

        // Method to create the HttpClient with a User-Agent (Nominatim requires one)
        private static HttpClient CreateClient()
        {
            HttpClient client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(15);
            // OpenStreetMap Nominatim and Wikipedia ask every app to identify itself
            client.DefaultRequestHeaders.UserAgent.ParseAdd("TripMate/1.0 (student group travel planner)");
            return client;
        }

        // Method to download a URL and parse the JSON response
        public static async Task<JsonDocument> GetJsonAsync(string url)
        {
            HttpResponseMessage response = await Http.GetAsync(url);
            response.EnsureSuccessStatusCode();
            string json = await response.Content.ReadAsStringAsync();
            return JsonDocument.Parse(json);
        }

        // Numbers in URLs must always use "." as the decimal point, whatever the PC's region settings
        public static string Num(double value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}