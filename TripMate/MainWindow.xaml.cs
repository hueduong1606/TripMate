using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using TripMate.Core.Data;
using TripMate.Core.Models;
using TripMate.Windows;

/*
 TripMate - a group travel planner for students.
 The main window has one tab per feature. To let three people work on it at the same time,
 the code for MainWindow is split into partial class files (one per tab):
   MainWindow.xaml.cs            - trips, Group tab and the shared code (this file)
   MainWindow.Itinerary.cs       - day-by-day plan with route optimisation
   MainWindow.Expenses.cs        - shared expenses and settle-up with the fewest transfers
   MainWindow.Explore.cs         - live weather and attraction search
   MainWindow.CurrencyConverter.cs - currency converter on the Explore tab
   MainWindow.Insights.cs        - spending charts and the ML.NET trip cost prediction
   MainWindow.ActivityEditing.cs - edit an activity (opens ActivityEditWindow)
   MainWindow.SettleUp.cs        - record settle-up payments (opens SettleUpWindow)
*/

namespace TripMate
{
    // Delegate used to tell every tab that something happened (Week 7: Multicast Delegates)
    public delegate void RefreshHandler();

    public partial class MainWindow : Window
    {
        // The trip that is currently selected, loaded from the database
        private Trip? currentTrip;

        // Multicast delegates. Each tab registers its own methods with "+=" in its Setup method:
        //   refreshTripViews - redraw the tab after the trip data changes
        //   tripSelected     - clear old results when a different trip is chosen
        //   tabChanged       - the user switched to another tab
        private RefreshHandler refreshTripViews;
        private RefreshHandler? tripSelected;
        private RefreshHandler? tabChanged;

        // Partial methods: each one is written in that tab's own file.
        // If a tab's file has not been added yet, the compiler simply skips the call.
        partial void SetupItineraryTab();
        partial void SetupExpensesTab();
        partial void SetupExploreTab();
        partial void SetupCurrencyConverter();
        partial void SetupInsightsTab();

        public MainWindow()
        {
            InitializeComponent();

            // Create the database if it does not exist yet
            using (var context = new Context())
            {
                context.Database.EnsureCreated();
            }

            // Register the Group tab's refresh methods with the delegate using "+="
            refreshTripViews = new RefreshHandler(ShowOverview);

            refreshTripViews += ShowMembers;

            // Let every other tab set itself up and register its own methods
            SetupItineraryTab();
            SetupExpensesTab();
            SetupExploreTab();
            SetupCurrencyConverter();
            SetupInsightsTab();
        }

        // Load the trips when the window first opens
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadTrips(null);
        }

        // Set the text in the status bar
        private void SetStatus(string message)
        {
            statusText.Text = message;
        }

        // ============================================================
        // Trips
        // ============================================================

        // Method to load all trips into the ComboBox and select one
        private void LoadTrips(int? selectTripId)
        {
            List<Trip> trips;
            using (var context = new Context())
            {
                // LINQ query: newest trips first
                trips = (from t in context.Trips
                         orderby t.StartDate descending
                         select t).ToList();
            }

            tripComboBox.ItemsSource = trips;

            if (trips.Count == 0)
            {
                currentTrip = null;
                mainTabs.IsEnabled = false;
                tripSummaryText.Text = "Click New Trip to get started";
                return;
            }

            // Select the requested trip, or the first one
            Trip? toSelect = trips.FirstOrDefault(t => t.TripId == selectTripId) ?? trips[0];
            tripComboBox.SelectedItem = toSelect;
        }

        // Method to load the selected trip with everything that belongs to it
        private void ReloadCurrentTrip()
        {
            Trip? selected = tripComboBox.SelectedItem as Trip;
            if (selected == null) return;

            using (var context = new Context())
            {
                currentTrip = context.Trips
                    .Include(t => t.Members)
                    .Include(t => t.Activities)
                    .Include(t => t.Expenses).ThenInclude(e => e.PaidBy)
                    .Include(t => t.Expenses).ThenInclude(e => e.Shares).ThenInclude(s => s.Member)
                    .FirstOrDefault(t => t.TripId == selected.TripId);
            }

            mainTabs.IsEnabled = currentTrip != null;
            if (currentTrip != null)
            {
                // Invoke the multicast delegate: every registered method runs in turn
                refreshTripViews();
            }
        }

        // A different trip was chosen in the dropdown: load it and tell the tabs
        private void tripComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ReloadCurrentTrip();

            if (currentTrip != null)
            {
                // Tell the tabs a different trip was chosen, so they can clear old results
                if (tripSelected != null) tripSelected();
                SetStatus("Opened " + currentTrip.Name);
            }
        }

        // Open the New Trip window and save the trip it creates
        private void newTripButton_Click(object sender, RoutedEventArgs e)
        {
            // Open the New Trip dialog
            NewTripWindow window = new NewTripWindow();
            window.Owner = this;

            if (window.ShowDialog() == true && window.NewTrip != null)
            {
                // Save the new trip (and its members) to the database
                using (var context = new Context())
                {
                    context.Trips.Add(window.NewTrip);
                    context.SaveChanges();
                }
                LoadTrips(window.NewTrip.TripId);
                SetStatus("Created " + window.NewTrip.Name);

            }
        }
        // Add the ready-made Tokyo trip, useful for a demo
        private void sampleTripButton_Click(object sender, RoutedEventArgs e)
        {
            Trip sample = DataSeeder.CreateSampleTrip();
            using (var context = new Context())
            {
                context.Trips.Add(sample);
                context.SaveChanges();
            }
            LoadTrips(sample.TripId);
            SetStatus("Sample trip added. Try Optimise Route on day 1 and Settle Up on the Expenses tab.");
        }


        // Delete the current trip after asking the user to confirm
        private void deleteTripButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentTrip == null) return;

            MessageBoxResult answer = MessageBox.Show("Delete \"" + currentTrip.Name + "\" and all of its activities and expenses?",
                "TripMate", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (answer != MessageBoxResult.Yes) return;

            using (var context = new Context())
            {
                // Load the trip with everything that belongs to it, then remove it
                Trip? trip = context.Trips
                    .Include(t => t.Members)
                    .Include(t => t.Activities)
                    .Include(t => t.Expenses).ThenInclude(x => x.Shares)
                    .FirstOrDefault(t => t.TripId == currentTrip.TripId);

                if (trip != null)
                {
                    context.Trips.Remove(trip);
                    context.SaveChanges();
                }
            }
            SetStatus("Trip deleted");
            LoadTrips(null);
        }

        // Method to show the trip summary on the Group tab and the top bar
        private void ShowOverview()
        {
            if (currentTrip == null) return;

            // Totals calculated with LINQ (decimals are summed in memory because SQLite cannot sum them)
            decimal totalSpent = currentTrip.SpendingExpenses.Sum(x => x.Amount);
            int people = Math.Max(1, currentTrip.Members.Count);

            overviewTitleText.Text = currentTrip.Name;
            overviewText.Text =
                "Destination: " + currentTrip.Destination + "\n" +
                "Dates: " + currentTrip.StartDate.ToString("ddd dd MMM yyyy") + " to " + currentTrip.EndDate.ToString("ddd dd MMM yyyy") +
                " (" + currentTrip.NumberOfDays + " days)\n" +
                "Travellers: " + currentTrip.Members.Count + "\n" +
                "Activities planned: " + currentTrip.Activities.Count + "\n" +
                "Spent so far: " + Money(totalSpent) + " (" + Money(totalSpent / people) + " per person)\n" +
                "Settle up in: " + currentTrip.HomeCurrency + "    Local currency: " + currentTrip.LocalCurrency + "\n" +
                "Location: " + (currentTrip.Latitude.HasValue ? currentTrip.Latitude.Value.ToString("0.00") + ", " + currentTrip.Longitude!.Value.ToString("0.00") : "not set (it will be looked up when needed)");

            int daysToGo = (currentTrip.StartDate.Date - DateTime.Today).Days;
            tripSummaryText.Text = daysToGo > 0 ? daysToGo + " days to go!" : (DateTime.Today <= currentTrip.EndDate.Date ? "Trip in progress" : "Trip finished");
        }

        // Format money in the trip's home currency
        private string Money(decimal amount)
        {
            string currency = currentTrip == null ? "" : " " + currentTrip.HomeCurrency;
            return amount.ToString("0.00") + currency;
        }

        // The user switched tabs: invoke the tabChanged delegate
        private void mainTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // SelectionChanged also bubbles up from ComboBoxes inside the tabs, so only react to the tabs themselves
            if (e.Source != mainTabs) return;
            if (tabChanged != null) tabChanged();
        }

        // ============================================================
        // Group
        // ============================================================

        // Method to show the travellers of the current trip on the Group tab
        private void ShowMembers()
        {
            if (currentTrip == null) return;

            List<Member> members = currentTrip.Members.OrderBy(m => m.Name).ToList();
            membersListBox.ItemsSource = members;
        }

        // Add a traveller to the trip, after checking the name
        private void addMemberButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentTrip == null) return;

            string name = memberNameTextBox.Text.Trim();
            if (name == "")
            {
                MessageBox.Show("Please type a name.", "TripMate");
                return;
            }
            // Names must be unique within a trip, because the settle-up uses them
            if (currentTrip.Members.Any(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show(name + " is already on this trip.", "TripMate");
                return;
            }

            using (var context = new Context())
            {
                context.Members.Add(new Member { Name = name, TripId = currentTrip.TripId });
                context.SaveChanges();
            }
            memberNameTextBox.Clear();
            ReloadCurrentTrip();
            SetStatus(name + " joined the trip");
        }

        // Remove the selected traveller, unless they are part of an expense
        private void removeMemberButton_Click(object sender, RoutedEventArgs e)
        {
            Member? member = membersListBox.SelectedItem as Member;
            if (member == null) return;

            using (var context = new Context())
            {
                // Don't remove someone who is part of an expense, or the balances would be wrong
                bool hasExpenses = context.Expenses.Any(x => x.PaidById == member.MemberId) ||
                                   context.ExpenseShares.Any(s => s.MemberId == member.MemberId);
                if (hasExpenses)
                {
                    MessageBox.Show(member.Name + " is part of some expenses. Delete those expenses first.", "TripMate");
                    return;
                }

                Member? toRemove = context.Members.Find(member.MemberId);
                if (toRemove != null)
                {
                    context.Members.Remove(toRemove);
                    context.SaveChanges();
                }
            }
            ReloadCurrentTrip();
            SetStatus(member.Name + " removed");
        }
    }
}