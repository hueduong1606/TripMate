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
        private readonly IGeocodingService geocodingService = new GeocodingService();

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

        // Method to look up an activity's location with OpenStreetMap
        private async void findLocationButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentTrip == null) return;

            string query = locationSearchTextBox.Text.Trim();
            if (query == "") query = activityNameTextBox.Text.Trim();
            if (query == "")
            {
                MessageBox.Show("Type a place name or address to search for.", "TripMate");
                return;
            }

            // Add the destination to the search so "Central Station" finds the right city
            if (!query.Contains(currentTrip.Destination, StringComparison.OrdinalIgnoreCase))
            {
                query = query + ", " + currentTrip.Destination;
            }

            try
            {
                findLocationButton.IsEnabled = false;
                SetStatus("Searching for " + query + "...");

                GeoPlace? place = await geocodingService.FindPlaceAsync(query);
                if (place == null)
                {
                    foundLocationText.Text = "No match found. You can type the coordinates instead.";
                    return;
                }

                latitudeTextBox.Text = place.Latitude.ToString("0.000000");
                longitudeTextBox.Text = place.Longitude.ToString("0.000000");
                foundLocationText.Text = place.Name;
                SetStatus("Location found");
            }
            catch (Exception ex)
            {
                foundLocationText.Text = "Location search failed: " + ex.Message;
            }
            finally
            {
                findLocationButton.IsEnabled = true;
            }
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
            locationSearchTextBox.Clear();
            latitudeTextBox.Clear();
            longitudeTextBox.Clear();
            notesTextBox.Clear();
            foundLocationText.Text = "";
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

        // Move the selected activity one place earlier in the day
        private void moveUpButton_Click(object sender, RoutedEventArgs e)
        {
            MoveSelectedActivity(-1);
        }

        // Move the selected activity one place later in the day
        private void moveDownButton_Click(object sender, RoutedEventArgs e)
        {
            MoveSelectedActivity(+1);
        }

        // Method to swap the selected activity with the one above (-1) or below (+1)
        private void MoveSelectedActivity(int direction)
        {
            Activity? selected = activitiesListView.SelectedItem as Activity;
            if (selected == null) return;

            List<Activity> dayActivities = GetSelectedDayActivities();
            int index = dayActivities.FindIndex(a => a.ActivityId == selected.ActivityId);
            int otherIndex = index + direction;
            if (index < 0 || otherIndex < 0 || otherIndex >= dayActivities.Count) return;

            // Give the activities their new positions (1, 2, 3...) with the two swapped
            Activity temp = dayActivities[index];
            dayActivities[index] = dayActivities[otherIndex];
            dayActivities[otherIndex] = temp;
            SaveDayOrder(dayActivities);

            ReloadCurrentTrip();
            activitiesListView.SelectedIndex = otherIndex;
        }

        // Delete the selected activity from the database
        private void removeActivityButton_Click(object sender, RoutedEventArgs e)
        {
            Activity? selected = activitiesListView.SelectedItem as Activity;
            if (selected == null) return;

            using (var context = new Context())
            {
                Activity? toRemove = context.Activities.Find(selected.ActivityId);
                if (toRemove != null)
                {
                    context.Activities.Remove(toRemove);
                    context.SaveChanges();
                }
            }

            // Renumber the rest of the day
            List<Activity> remaining = GetSelectedDayActivities().Where(a => a.ActivityId != selected.ActivityId).ToList();
            SaveDayOrder(remaining);
            ReloadCurrentTrip();
            SetStatus(selected.Name + " removed");
        }

        // Method to save a day's activities in the order of the list (Order = 1, 2, 3...)
        private void SaveDayOrder(List<Activity> orderedActivities)
        {
            using (var context = new Context())
            {
                for (int i = 0; i < orderedActivities.Count; i++)
                {
                    Activity? activity = context.Activities.Find(orderedActivities[i].ActivityId);
                    if (activity != null) activity.Order = i + 1;
                }
                context.SaveChanges();
            }
        }
    }
}