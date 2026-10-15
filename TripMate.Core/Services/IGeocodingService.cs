namespace TripMate.Core.Services
{
    // Interface for anything that can turn a name into coordinates (Week 5: Interfaces).
    // The windows only use this interface, so the real web service could be swapped
    // for another provider (or a fake one in a test) without changing the UI code.
    public interface IGeocodingService
    {
        // Find a city, e.g. the trip destination
        Task<GeoPlace?> FindCityAsync(string cityName);

        // Find a specific place, e.g. "Senso-ji, Tokyo"
        Task<GeoPlace?> FindPlaceAsync(string query);
    }
}