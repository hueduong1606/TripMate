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
        private int locationRequestVersion;

        private readonly RouteOptimiser routeOptimiser = new RouteOptimiser();
        private readonly IGeocodingService geocodingService = new GeocodingService();

        // Called from the MainWindow constructor
        partial void SetupItineraryTab()
        {
            // Fill the travel modes: one object of each TravelMode subclass
            foreach (TravelMode mode in TravelMode.GetAll()) travelModeComboBox.Items.Add(mode);
            travelModeComboBox.SelectedIndex = 0;


            // Redraw the itinerary whenever the trip data changes
            refreshTripViews += ShowItinerary;
            tripSelected += ClearActivityForm;
            locationSearchTextBox.TextChanged += (_, _) => InvalidateLocationSearch();
            activityNameTextBox.TextChanged += (_, _) => InvalidateLocationSearch();
            latitudeTextBox.TextChanged += (_, _) => InvalidateLocationSearch();
            longitudeTextBox.TextChanged += (_, _) => InvalidateLocationSearch();
        }

        // Method to empty the "Add an activity" form, so nothing from the old trip is reused
        // Clear all activity inputs and invalidate searches from the previous trip.
        private void ClearActivityForm()
        {
            InvalidateLocationSearch();
            activityNameTextBox.Clear();
            locationSearchTextBox.Clear();
            latitudeTextBox.Clear();
            longitudeTextBox.Clear();
            notesTextBox.Clear();
            durationTextBox.Text = "60";
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
            ShowCurrentRouteInfo(dayActivities);
        }

        // Show how far the day's current route is
        private void ShowCurrentRouteInfo(List<Activity> dayActivities)
        {
            List<Activity> located = dayActivities.Where(a => a.HasLocation).ToList();
            if (located.Count < 2)
            {
                routeResultText.Text = "Add at least two stops with a location to see travel distances. The first stop of the day stays in place (e.g. your hostel).";
                return;
            }

            TravelMode mode = travelModeComboBox.SelectedItem as TravelMode ?? new WalkingMode();
            double km = routeOptimiser.RouteLength(located);
            double minutes = routeOptimiser.TravelMinutes(located, mode);
            routeResultText.Text = "Current route: " + km.ToString("0.0") + " km, about " + FormatMinutes(minutes) + " " + mode.Name.ToLower() + ".";
        }

        // Method to turn minutes into text like "1 h 25 min"
        private static string FormatMinutes(double minutes)
        {
            int total = (int)Math.Round(minutes);
            return total >= 60 ? (total / 60) + " h " + (total % 60) + " min" : total + " min";
        }

        // A different day was chosen: show its activities
        private void dayComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ShowDayActivities();
        }

        // A different travel mode was chosen: update the travel time
        private void travelModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (currentTrip != null) ShowDayActivities();
        }

        // Invalidating a lookup also permits a new search while the old one finishes.
        private void InvalidateLocationSearch()
        {
            locationRequestVersion++;
            foundLocationText.Text = "";
            findLocationButton.IsEnabled = true;
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

            int tripId = currentTrip.TripId;
            int requestVersion = ++locationRequestVersion;

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
                if (requestVersion != locationRequestVersion || currentTrip?.TripId != tripId) return;
                if (place == null)
                {
                    foundLocationText.Text = "No match found. You can type the coordinates instead.";
                    SetStatus("No location found");
                    return;
                }

                findLocationButton.IsEnabled = true;
                latitudeTextBox.Text = place.Latitude.ToString("0.000000");
                longitudeTextBox.Text = place.Longitude.ToString("0.000000");
                foundLocationText.Text = place.Name;
                SetStatus("Location found");
            }
            catch (Exception ex)
            {
                if (requestVersion == locationRequestVersion && currentTrip?.TripId == tripId)
                {
                    foundLocationText.Text = "Location search failed: " + ex.Message;
                    SetStatus("Location search failed");
                }
            }
            finally
            {
                if (requestVersion == locationRequestVersion) findLocationButton.IsEnabled = true;
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
            if (!int.TryParse(durationTextBox.Text, out int duration) || duration < 0 || duration > 24 * 60)
            {
                MessageBox.Show("Duration must be a whole number of minutes between 0 and 1440.", "TripMate");
                return;
            }

            // The location is optional, but both values are needed for routing
            double? lat = null, lon = null;
            if (latitudeTextBox.Text.Trim() != "" || longitudeTextBox.Text.Trim() != "")
            {
                if (!double.TryParse(latitudeTextBox.Text, out double latValue) || !double.TryParse(longitudeTextBox.Text, out double lonValue) ||
                    !double.IsFinite(latValue) || !double.IsFinite(lonValue) ||
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

            MessageBoxResult answer = MessageBox.Show("Remove \"" + selected.Name + "\" from the itinerary?",
                "TripMate", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes) return;

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

        // Method to re-order the day's activities to reduce travel time
        private void optimiseButton_Click(object sender, RoutedEventArgs e)
        {
            TripDay? day = dayComboBox.SelectedItem as TripDay;
            if (day == null) return;

            List<Activity> dayActivities = GetSelectedDayActivities();
            if (dayActivities.Count(a => a.HasLocation) < 3)
            {
                MessageBox.Show("Route optimisation needs at least 3 stops with a location on this day.", "TripMate");
                return;
            }

            // Polymorphism: "mode" can be any TravelMode subclass, and its own MinutesFor override is used
            TravelMode mode = travelModeComboBox.SelectedItem as TravelMode ?? new WalkingMode();

            // Run nearest neighbour + 2-opt
            RouteResult<Activity> result = routeOptimiser.Optimise(dayActivities);
            List<Activity> located = dayActivities.Where(a => a.HasLocation).ToList();
            double minutesBefore = routeOptimiser.TravelMinutes(located, mode);
            double minutesAfter = routeOptimiser.TravelMinutes(result.Route, mode);

            // New order = optimised route, then any stops without a location at the end
            List<Activity> newOrder = new List<Activity>(result.Route);
            newOrder.AddRange(result.Unlocated);
            SaveDayOrder(newOrder);
            ReloadCurrentTrip();

            // Explain what changed
            if (result.KmSaved < 0.05)
            {
                routeResultText.Text = "Day " + day.DayNumber + " is already in a good order: " + result.OptimisedDistanceKm.ToString("0.0") +
                                       " km, about " + FormatMinutes(minutesAfter) + " " + mode.Name.ToLower() + ".";
            }
            else
            {
                routeResultText.Text = "Day " + day.DayNumber + " optimised: " + result.OriginalDistanceKm.ToString("0.0") + " km -> " +
                                       result.OptimisedDistanceKm.ToString("0.0") + " km (" + result.KmSaved.ToString("0.0") + " km shorter). " +
                                       mode.Name + " time: " + FormatMinutes(minutesBefore) + " -> " + FormatMinutes(minutesAfter) +
                                       ", saving about " + FormatMinutes(minutesBefore - minutesAfter) + ".";
            }
            if (result.Unlocated.Count > 0)
            {
                routeResultText.Text += " Stops without a location were moved to the end: " + string.Join(", ", result.Unlocated.Select(a => a.Name)) + ".";
            }
            SetStatus("Route optimised");
        }
    }
}