using System.ComponentModel.DataAnnotations.Schema;

namespace TripMate.Core.Models
{
    // One stop in the day-by-day itinerary
    public class Activity : IGeoLocated, IComparable<Activity>
    {
        public int ActivityId { get; set; }
        public string Name { get; set; } = "";
        public string Notes { get; set; } = "";

        // Which day of the trip, and the position within that day
        public int DayNumber { get; set; }
        public int Order { get; set; }

        public int DurationMinutes { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        public int TripId { get; set; }

        // An activity can only be routed if it has coordinates
        [NotMapped]
        public bool HasLocation
        {
            get { return Latitude.HasValue && Longitude.HasValue; }
        }

        [NotMapped]
        public string LocationText
        {
            get { return HasLocation ? Latitude!.Value.ToString("0.0000") + ", " + Longitude!.Value.ToString("0.0000") : "(no location)"; }
        }

        // Implement the CompareTo method so a List<Activity> can be sorted
        // by day first, then by the order within the day
        public int CompareTo(Activity? other)
        {
            if (other == null) return 1;
            if (DayNumber != other.DayNumber) return DayNumber.CompareTo(other.DayNumber);
            return Order.CompareTo(other.Order);
        }

        // Override the ToString() method to show the activity in a list
        public override string ToString()
        {
            return Order + ". " + Name + " (" + DurationMinutes + " min)";
        }
    }
}