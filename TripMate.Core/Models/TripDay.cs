namespace TripMate.Core.Models
{
    // A single day of a trip. Not stored in the database, it is
    // calculated from the trip's start and end dates.
    public class TripDay
    {
        public TripDay(int dayNumber, DateTime date)
        {
            DayNumber = dayNumber;
            Date = date;
        }

        public int DayNumber { get; set; }
        public DateTime Date { get; set; }

        // Override the ToString() method to show the day in a dropdown
        public override string ToString()
        {
            return "Day " + DayNumber + " - " + Date.ToString("ddd dd MMM");
        }
    }
}