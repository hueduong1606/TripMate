using System.Text.Json;
using TripMate.Core.Models;

namespace TripMate.Core.Services
{
    public class AttractionService : IAttractionService
    {
        // Method to find places near a point, using the Wikipedia geosearch API
        // (every Wikipedia article with coordinates: landmarks, museums, parks, beaches...)
        public async Task<List<Attraction>> SearchNearbyAsync(double latitude, double longitude, int radiusKm)
        {
            int radiusMetres = Math.Clamp(radiusKm * 1000, 100, 10000);  // the API allows up to 10 km
            string url = "https://en.wikipedia.org/w/api.php?action=query&list=geosearch&format=json&gslimit=50" +
                         "&gsradius=" + radiusMetres +
                         "&gscoord=" + ApiClient.Num(latitude) + "%7C" + ApiClient.Num(longitude);

            List<Attraction> attractions = new List<Attraction>();
            using (JsonDocument doc = await ApiClient.GetJsonAsync(url))
            {
                foreach (JsonElement place in doc.RootElement.GetProperty("query").GetProperty("geosearch").EnumerateArray())
                {
                    attractions.Add(new Attraction
                    {
                        Name = place.GetProperty("title").GetString() ?? "",
                        Latitude = place.GetProperty("lat").GetDouble(),
                        Longitude = place.GetProperty("lon").GetDouble(),
                        DistanceKm = place.GetProperty("dist").GetDouble() / 1000.0,
                        Url = "https://en.wikipedia.org/?curid=" + place.GetProperty("pageid").GetInt32()
                    });
                }
            }
            return attractions;
        }

        // Method to filter the results with any condition, using LINQ Where
        // e.g. FilterAttractions(list, a => a.Name.Contains("Museum"))
        public static List<Attraction> FilterAttractions(List<Attraction> attractions, Func<Attraction, bool> condition)
        {
            return attractions.Where(condition).ToList();
        }
    }
}