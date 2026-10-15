namespace TripMate.Core.Models
{
    // A student travelling on the trip
    public class Member
    {
        public int MemberId { get; set; }
        public string Name { get; set; } = "";

        // Foreign key and navigation property back to the Trip
        public int TripId { get; set; }

        // Override the ToString() method so lists show the member's name
        public override string ToString()
        {
            return Name;
        }
    }
}