namespace TripMate.Core.Models
{
    // A place found by the attraction search (not stored in the database)
    public class Attraction : IGeoLocated
    {
        public string Name { get; set; } = "";
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public double DistanceKm { get; set; }
        public string Url { get; set; } = "";

        // Override the ToString() method to show the place and its distance in a list
        public override string ToString()
        {
            return Name + "  (" + DistanceKm.ToString("0.0") + " km from centre)";
        }
    }
}