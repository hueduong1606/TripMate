namespace TripMate.Core.Models
{
    // Interface for anything that can be placed on a map (Week 5: Interfaces)
    // Activity and Attraction both implement it, so the route optimiser
    // and the distance extension method can work with either type.
    public interface IGeoLocated
    {
        string Name { get; }
        double? Latitude { get; }
        double? Longitude { get; }
    }
}