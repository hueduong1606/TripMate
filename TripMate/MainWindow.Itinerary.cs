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
    }
}