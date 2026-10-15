using System.ComponentModel.DataAnnotations.Schema;

namespace TripMate.Core.Models
{
    public class Trip
    {
        // Category used for "X paid Y back" payments recorded from the Settle Up window.
        // They change the balances but are not spending, so charts and totals leave them out.
        public const string SettlementCategory = "Settlement";

        public Trip()
        {
            Members = new List<Member>();
            Activities = new List<Activity>();
            Expenses = new List<Expense>();
            StartDate = DateTime.Today;
            EndDate = DateTime.Today.AddDays(3);
        }

        public int TripId { get; set; }
        public string Name { get; set; } = "";
        public string Destination { get; set; } = "";

        // Destination coordinates, used for weather and attraction search
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        // Currency the group settles up in, and the currency used at the destination
        public string HomeCurrency { get; set; } = "AUD";
        public string LocalCurrency { get; set; } = "AUD";

        // Navigation properties (one Trip has many Members, Activities and Expenses)
        public IList<Member> Members { get; set; }
        public IList<Activity> Activities { get; set; }
        public IList<Expense> Expenses { get; set; }

        // Real spending only, without settle-up payments (LINQ with a lambda expression)
        [NotMapped]
        public List<Expense> SpendingExpenses
        {
            get { return Expenses.Where(e => e.Category != SettlementCategory).ToList(); }
        }

        // Number of days in the trip, including the first and last day
        [NotMapped]
        public int NumberOfDays
        {
            get { return Math.Max(1, (EndDate.Date - StartDate.Date).Days + 1); }
        }

        // Method to build the list of days shown in the itinerary
        public List<TripDay> GetDays()
        {
            List<TripDay> days = new List<TripDay>();
            for (int i = 0; i < NumberOfDays; i++)
            {
                days.Add(new TripDay(i + 1, StartDate.Date.AddDays(i)));
            }
            return days;
        }

        // Override the ToString() method to display the trip in the ComboBox
        public override string ToString()
        {
            return Name + " - " + Destination + " (" + StartDate.ToString("dd MMM") + " to " + EndDate.ToString("dd MMM yyyy") + ")";
        }
    }
}