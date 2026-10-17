using TripMate.Core.Models;

namespace TripMate.Core.Algorithms
{
    // Extension methods for anything that has a location
    public static class GeoExtensions
    {
        private const double EarthRadiusKm = 6371.0;

        // Method to calculate the straight-line distance between two places in km
        // using the Haversine formula.
        // Usage: double km = activityA.DistanceTo(activityB);
        public static double DistanceTo(this IGeoLocated from, IGeoLocated to)
        {
            if (from.Latitude == null || from.Longitude == null || to.Latitude == null || to.Longitude == null)
            {
                throw new ArgumentException("Both places need a latitude and longitude.");
            }
            return HaversineKm(from.Latitude.Value, from.Longitude.Value, to.Latitude.Value, to.Longitude.Value);
        }

        // Haversine formula on two pairs of coordinates (in degrees)
        public static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
        {
            double dLat = ToRadians(lat2 - lat1);
            double dLon = ToRadians(lon2 - lon1);

            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                       Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return EarthRadiusKm * c;
        }

        // Method to convert degrees to radians
        private static double ToRadians(double degrees)
        {
            return degrees * Math.PI / 180.0;
        }
    }
}