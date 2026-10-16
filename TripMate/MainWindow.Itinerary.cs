using System.Windows;
using System.Windows.Controls;
using TripMate.Core.Algorithms;
using TripMate.Core.Data;
using TripMate.Core.Models;
using TripMate.Core.Services;

/*
 Itinerary tab: day-by-day plan with route optimisation.
 Activities can be added with a location (found with OpenStreetMap), moved up and down,
 and "Optimise Route" re-orders the day with nearest neighbour + 2-opt (see RouteOptimiser).
*/

namespace TripMate
{
    public partial class MainWindow
    {
        // Called from the MainWindow constructor
        partial void SetupItineraryTab()
        {
            // Redraw the itinerary whenever the trip data changes
            refreshTripViews += ShowItinerary;
        }

        // Method to fill the day list and show the activities of the selected day
        private void ShowItinerary()
        {
            if (currentTrip == null) return;

            // Remember which day was selected before the refresh
            TripDay? previousDay = dayComboBox.SelectedItem as TripDay;
            int selectedDay = previousDay != null ? previousDay.DayNumber : 1;

            List<TripDay> days = currentTrip.GetDays();
            dayComboBox.ItemsSource = days;
            dayComboBox.SelectedItem = days.FirstOrDefault(d => d.DayNumber == selectedDay) ?? days[0];

            ShowDayActivities();
        }

        // Activities of the selected day, sorted with IComparable
        private List<Activity> GetSelectedDayActivities()
        {
            TripDay? day = dayComboBox.SelectedItem as TripDay;
            if (currentTrip == null || day == null) return new List<Activity>();

            List<Activity> dayActivities = currentTrip.Activities.Where(a => a.DayNumber == day.DayNumber).ToList();
            dayActivities.Sort();
            return dayActivities;
        }

        // Method to show the activities of the selected day
        private void ShowDayActivities()
        {
            List<Activity> dayActivities = GetSelectedDayActivities();
            activitiesListView.ItemsSource = dayActivities;
        }

        // A different day was chosen: show its activities
        private void dayComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ShowDayActivities();
        }

        // Validate the form and add the activity to the selected day
        private void addActivityButton_Click(object sender, RoutedEventArgs e)
        {
            TripDay? day = dayComboBox.SelectedItem as TripDay;
            if (currentTrip == null || day == null) return;

            // Validate the input
            string name = activityNameTextBox.Text.Trim();
            if (name == "")
            {
                MessageBox.Show("Please enter the activity name.", "TripMate");
                return;
            }
            if (!int.TryParse(durationTextBox.Text, out int duration) || duration < 0)
            {
                MessageBox.Show("Duration must be a whole number of minutes.", "TripMate");
                return;
            }

            // The location is optional, but both values are needed for routing
            double? lat = null, lon = null;
            if (latitudeTextBox.Text.Trim() != "" || longitudeTextBox.Text.Trim() != "")
            {
                if (!double.TryParse(latitudeTextBox.Text, out double latValue) || !double.TryParse(longitudeTextBox.Text, out double lonValue) ||
                    latValue < -90 || latValue > 90 || lonValue < -180 || lonValue > 180)
                {
                    MessageBox.Show("Please enter a valid latitude and longitude, or leave both empty.", "TripMate");
                    return;
                }
                lat = latValue;
                lon = lonValue;
            }

            AddActivity(name, day.DayNumber, duration, lat, lon, notesTextBox.Text.Trim());

            // Clear the form
            activityNameTextBox.Clear();
            latitudeTextBox.Clear();
            longitudeTextBox.Clear();
            notesTextBox.Clear();
        }

        // Method to save a new activity at the end of a day
        private void AddActivity(string name, int dayNumber, int duration, double? lat, double? lon, string notes)
        {
            if (currentTrip == null) return;

            // Next position in the day = highest order so far + 1
            int nextOrder = currentTrip.Activities.Where(a => a.DayNumber == dayNumber).Select(a => a.Order).DefaultIfEmpty(0).Max() + 1;

            using (var context = new Context())
            {
                context.Activities.Add(new Activity
                {
                    Name = name,
                    DayNumber = dayNumber,
                    Order = nextOrder,
                    DurationMinutes = duration,
                    Latitude = lat,
                    Longitude = lon,
                    Notes = notes,
                    TripId = currentTrip.TripId
                });
                context.SaveChanges();
            }
            ReloadCurrentTrip();
            SetStatus(name + " added to day " + dayNumber);
        }
    }
}