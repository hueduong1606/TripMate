using TripMate.Core.Algorithms;
using TripMate.Core.Models;

namespace TripMate.Core.Data
{
    // Creates a ready-made sample trip, so the app can be demonstrated straight away
    public static class DataSeeder
    {
        // Method to build the sample Tokyo trip used for demos and tests
        public static Trip CreateSampleTrip()
        {
            // Create entity objects
            Trip trip = new Trip
            {
                Name = "Tokyo semester break (sample)",
                Destination = "Tokyo",
                Latitude = 35.6895,
                Longitude = 139.6917,
                StartDate = DateTime.Today.AddDays(14),
                EndDate = DateTime.Today.AddDays(17),
                HomeCurrency = "AUD",
                LocalCurrency = "JPY"
            };

            Member ann = new Member { Name = "Ann" };
            Member ben = new Member { Name = "Ben" };
            Member cat = new Member { Name = "Cat" };
            Member dan = new Member { Name = "Dan" };
            trip.Members.Add(ann);
            trip.Members.Add(ben);
            trip.Members.Add(cat);
            trip.Members.Add(dan);

            // Activities are deliberately in a zig-zag order so "Optimise Route" has something to fix
            AddActivity(trip, 1, "Hostel (Asakusa)", 35.7120, 139.7950, 30);
            AddActivity(trip, 1, "Tokyo Skytree", 35.7101, 139.8107, 90);
            AddActivity(trip, 1, "Akihabara", 35.6984, 139.7731, 120);
            AddActivity(trip, 1, "Senso-ji Temple", 35.7148, 139.7967, 60);
            AddActivity(trip, 1, "Ameyoko Market", 35.7101, 139.7745, 60);
            AddActivity(trip, 1, "Ueno Park", 35.7156, 139.7745, 90);

            AddActivity(trip, 2, "Hostel (Asakusa)", 35.7120, 139.7950, 30);
            AddActivity(trip, 2, "Shibuya Crossing", 35.6595, 139.7005, 45);
            AddActivity(trip, 2, "Shinjuku Gyoen", 35.6852, 139.7101, 90);
            AddActivity(trip, 2, "Tokyo Tower", 35.6586, 139.7454, 60);
            AddActivity(trip, 2, "Meiji Shrine", 35.6764, 139.6993, 60);
            AddActivity(trip, 2, "Takeshita Street", 35.6716, 139.7031, 60);

            AddActivity(trip, 3, "Hostel (Asakusa)", 35.7120, 139.7950, 30);
            AddActivity(trip, 3, "teamLab Planets", 35.6491, 139.7898, 120);
            AddActivity(trip, 3, "Tsukiji Outer Market", 35.6655, 139.7707, 90);
            AddActivity(trip, 3, "Ramen cooking class", null, null, 120);

            // Shared expenses (amounts in AUD)
            AddExpense(trip, "Hostel, 3 nights", "Accommodation", 480.00m, ann, 0, ann, ben, cat, dan);
            AddExpense(trip, "Subway passes", "Transport", 96.00m, ben, 0, ann, ben, cat, dan);
            AddExpense(trip, "Skytree tickets", "Activities", 87.00m, dan, 0, ben, cat, dan);
            AddExpense(trip, "Sushi dinner", "Food", 164.40m, cat, 0, ann, ben, cat, dan);
            AddExpense(trip, "teamLab tickets", "Activities", 140.00m, ann, 2, ann, ben, cat, dan);
            AddExpense(trip, "Izakaya", "Food", 132.00m, dan, 1, ann, ben, cat, dan);
            AddExpense(trip, "Souvenirs", "Shopping", 45.50m, ben, 1, ben, cat);

            return trip;
        }

        // Method to add an activity at the end of a day
        private static void AddActivity(Trip trip, int day, string name, double? lat, double? lon, int minutes)
        {
            int order = trip.Activities.Count(a => a.DayNumber == day) + 1;
            trip.Activities.Add(new Activity { Name = name, DayNumber = day, Order = order, Latitude = lat, Longitude = lon, DurationMinutes = minutes });
        }

        // Method to add an expense split equally between some members
        private static void AddExpense(Trip trip, string description, string category, decimal amount, Member paidBy, int dayOffset, params Member[] sharedBy)
        {
            Expense expense = new Expense
            {
                Description = description,
                Category = category,
                Amount = amount,
                OriginalAmount = amount,
                OriginalCurrency = trip.HomeCurrency,
                Date = trip.StartDate.AddDays(dayOffset),
                PaidBy = paidBy,
                Trip = trip
            };

            decimal[] shares = DebtSettler.SplitEqually(amount, sharedBy.Length);
            for (int i = 0; i < sharedBy.Length; i++)
            {
                expense.Shares.Add(new ExpenseShare { Member = sharedBy[i], ShareAmount = shares[i] });
            }
            trip.Expenses.Add(expense);
        }
    }
}