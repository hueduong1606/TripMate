using System.Windows;
using System.Windows.Input;
using TripMate.Core.Data;
using TripMate.Windows;
using Activity = TripMate.Core.Models.Activity;

/*
 Editing an activity on the Itinerary tab: double-click it, or right-click → Edit...
 Opens ActivityEditWindow and saves the changes with EF Core.
*/

namespace TripMate
{
    public partial class MainWindow
    {
        // Double-clicking an activity opens it for editing
        private void activitiesListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            EditSelectedActivity();
        }

        // The "Edit" item in the right-click menu
        private void editActivityMenuItem_Click(object sender, RoutedEventArgs e)
        {
            EditSelectedActivity();
        }

        // Method to open the edit window for the selected activity and save the result
        private void EditSelectedActivity()
        {
            Activity? selected = activitiesListView.SelectedItem as Activity;
            if (currentTrip == null || selected == null) return;

            int oldDay = selected.DayNumber;
            ActivityEditWindow window = new ActivityEditWindow(selected, currentTrip.GetDays(), currentTrip.Destination);
            window.Owner = this;
            if (window.ShowDialog() != true) return;

            using (var context = new Context())
            {
                Activity? activity = context.Activities.Find(selected.ActivityId);
                if (activity == null) return;

                activity.Name = selected.Name;
                activity.DurationMinutes = selected.DurationMinutes;
                activity.Latitude = selected.Latitude;
                activity.Longitude = selected.Longitude;
                activity.Notes = selected.Notes;

                // Moved to another day: put it at the end of that day
                if (selected.DayNumber != oldDay)
                {
                    int lastOrder = context.Activities
                        .Where(a => a.TripId == activity.TripId && a.DayNumber == selected.DayNumber)
                        .Select(a => a.Order)
                        .DefaultIfEmpty(0)
                        .Max();
                    activity.DayNumber = selected.DayNumber;
                    activity.Order = lastOrder + 1;
                }
                context.SaveChanges();
            }

            // Renumber the day the activity left, so the order stays 1, 2, 3...
            if (selected.DayNumber != oldDay)
            {
                List<Activity> oldDayActivities = currentTrip.Activities
                    .Where(a => a.DayNumber == oldDay && a.ActivityId != selected.ActivityId)
                    .ToList();
                oldDayActivities.Sort();
                SaveDayOrder(oldDayActivities);
            }

            ReloadCurrentTrip();
            SetStatus(selected.Name + " updated");
        }
    }
}