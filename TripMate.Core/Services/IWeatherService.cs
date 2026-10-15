namespace TripMate.Core.Services
{
    // Interface for a daily weather forecast provider (Week 5: Interfaces)
    public interface IWeatherService
    {
        Task<List<WeatherDay>> GetForecastAsync(double latitude, double longitude, int days);
    }
}