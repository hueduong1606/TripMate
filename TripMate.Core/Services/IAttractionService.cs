using TripMate.Core.Models;

namespace TripMate.Core.Services
{
    // Interface for searching places to visit near a point
    public interface IAttractionService
    {
        Task<List<Attraction>> SearchNearbyAsync(double latitude, double longitude, int radiusKm);
    }
}