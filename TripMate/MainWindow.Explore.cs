using System.Diagnostics;
using System.Windows;
using TripMate.Core.Data;
using TripMate.Core.Models;
using TripMate.Core.Services;

/*
 Explore tab: live weather forecast (Open-Meteo) and nearby attraction search (Wikipedia).
 Attractions can be added straight to a day of the itinerary.
*/

namespace TripMate
{
    public partial class MainWindow
    {
        private readonly IWeatherService weatherService = new WeatherService();
        private readonly IAttractionService attractionService = new AttractionService();

        // Attraction search results
        private List<Attraction> attractions = new List<Attraction>();

        // Goes up by 1 every time a trip is selected, so a slow request started before
        // the switch can tell it is out of date (even after switching A -> B -> A)
        private int exploreRequestVersion = 0;

        // Called from the MainWindow constructor
        partial void SetupExploreTab()
        {
            refreshTripViews += ShowExploreDays;
            tripSelected += ClearExploreResults;
        }

        // Fill the "add to day" list with the trip's days
        private void ShowExploreDays()
        {
            if (currentTrip == null) return;

            TripDay? previousDay = attractionDayComboBox.SelectedItem as TripDay;
            int selectedDay = previousDay != null ? previousDay.DayNumber : 1;
            List<TripDay> days = currentTrip.GetDays();
            attractionDayComboBox.ItemsSource = days;
            attractionDayComboBox.SelectedItem = days.FirstOrDefault(d => d.DayNumber == selectedDay) ?? days[0];
        }

        // Clear the results from the previous trip
        private void ClearExploreResults()
        {
            // Any request still running belongs to the old trip, so make it out of date
            exploreRequestVersion++;

            weatherDataGrid.ItemsSource = null;
            weatherInfoText.Text = "";
            attractionsListBox.ItemsSource = null;
            attractionInfoText.Text = "";
        }

        // True if the user selected another trip after the request started
        private bool IsExploreRequestOutdated(int requestedTripId, int requestVersion)
        {
            return currentTrip == null
                || currentTrip.TripId != requestedTripId
                || exploreRequestVersion != requestVersion;
        }

        // Method to make sure the trip has coordinates; looks up the destination if needed
        private async Task<bool> EnsureDestinationLocationAsync(int requestedTripId, int requestVersion)
        {
            if (IsExploreRequestOutdated(requestedTripId, requestVersion)) return false;

            Trip requestedTrip = currentTrip!;
            if (requestedTrip.Latitude.HasValue && requestedTrip.Longitude.HasValue) return true;

            // Remember the destination before waiting, because currentTrip may change
            string requestedDestination = requestedTrip.Destination;

            GeoPlace? place = await geocodingService.FindCityAsync(requestedDestination);

            // Ignore the response if the selected trip changed while we were waiting
            if (IsExploreRequestOutdated(requestedTripId, requestVersion)) return false;

            if (place == null)
            {
                MessageBox.Show("Could not find \"" + requestedDestination + "\" on the map.", "TripMate");
                return false;
            }

            // Save the coordinates to the trip that started the request
            using (var context = new Context())
            {
                Trip? trip = context.Trips.Find(requestedTripId);
                if (trip != null)
                {
                    trip.Latitude = place.Latitude;
                    trip.Longitude = place.Longitude;
                    context.SaveChanges();
                }
            }
            currentTrip!.Latitude = place.Latitude;
            currentTrip.Longitude = place.Longitude;
            ShowOverview();
            return true;
        }

        // Get the weather forecast for the destination
        private async void weatherButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentTrip == null) return;

            // Remember which trip this request is for
            int requestedTripId = currentTrip.TripId;
            int requestVersion = exploreRequestVersion;

            try
            {
                weatherButton.IsEnabled = false;
                SetStatus("Getting the forecast...");
                if (!await EnsureDestinationLocationAsync(requestedTripId, requestVersion)) return;
                if (IsExploreRequestOutdated(requestedTripId, requestVersion)) return;

                // Copy the trip details before the next await
                Trip requestedTrip = currentTrip!;
                string destination = requestedTrip.Destination;
                DateTime startDate = requestedTrip.StartDate.Date;
                DateTime endDate = requestedTrip.EndDate.Date;

                List<WeatherDay> forecast = await weatherService.GetForecastAsync(requestedTrip.Latitude!.Value, requestedTrip.Longitude!.Value, 16);

                // Only show the forecast if the user is still on the same trip
                if (IsExploreRequestOutdated(requestedTripId, requestVersion)) return;

                weatherDataGrid.ItemsSource = forecast;

                // Tell the user which forecast days fall inside the trip
                int tripDaysInForecast = forecast.Count(d => d.Date >= startDate && d.Date <= endDate);
                weatherInfoText.Text = tripDaysInForecast > 0
                    ? tripDaysInForecast + " of the forecast days are during your trip."
                    : "Forecasts only reach 16 days ahead, so this is the weather there right now.";
                SetStatus("Forecast for " + destination + " updated");
            }
            catch (Exception ex)
            {
                // No error message for a trip the user has already left
                if (IsExploreRequestOutdated(requestedTripId, requestVersion)) return;

                MessageBox.Show("Could not get the weather: " + ex.Message, "TripMate");
                SetStatus("Weather unavailable");
            }
            finally
            {
                weatherButton.IsEnabled = true;
                // The request stopped early (e.g. the place was not found): clear the old message
                if (statusText.Text == "Getting the forecast...") SetStatus("Ready");
            }
        }

        // Search for attractions near the destination within the chosen radius
        private async void searchAttractionsButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentTrip == null) return;

            // Remember which trip this request is for, and the search settings at the time of the click
            int requestedTripId = currentTrip.TripId;
            int requestVersion = exploreRequestVersion;
            int radiusKm = (int)radiusSlider.Value;
            string keyword = keywordTextBox.Text.Trim();

            try
            {
                searchAttractionsButton.IsEnabled = false;
                SetStatus("Searching for things to see...");
                if (!await EnsureDestinationLocationAsync(requestedTripId, requestVersion)) return;
                if (IsExploreRequestOutdated(requestedTripId, requestVersion)) return;

                Trip requestedTrip = currentTrip!;
                List<Attraction> found = await attractionService.SearchNearbyAsync(requestedTrip.Latitude!.Value, requestedTrip.Longitude!.Value, radiusKm);

                // Only update the list if the user is still on the same trip
                if (IsExploreRequestOutdated(requestedTripId, requestVersion)) return;

                // Optional keyword filter, passed as a Predicate using an anonymous method (Week 7)
                if (keyword != "")
                {
                    found = AttractionService.FilterAttractions(found, delegate (Attraction a)
                    {
                        // The anonymous method can use the "keyword" variable from outside it
                        return a.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase);
                    });
                }

                // LINQ with lambda expressions: closest places first, no duplicate names
                attractions = found
                    .GroupBy(a => a.Name)
                    .Select(g => g.First())
                    .OrderBy(a => a.DistanceKm)
                    .ToList();

                attractionsListBox.ItemsSource = attractions;
                attractionInfoText.Text = attractions.Count + " places found. Select one and add it to a day, or double-click to read more.";
                SetStatus("Attraction search finished");
            }
            catch (Exception ex)
            {
                // No error message for a trip the user has already left
                if (IsExploreRequestOutdated(requestedTripId, requestVersion)) return;

                MessageBox.Show("Could not search for attractions: " + ex.Message, "TripMate");
                SetStatus("Attraction search unavailable");
            }
            finally
            {
                searchAttractionsButton.IsEnabled = true;
                // The request stopped early (e.g. the place was not found): clear the old message
                if (statusText.Text == "Searching for things to see...") SetStatus("Ready");
            }
        }

        // Add the selected attraction to the chosen day of the itinerary
        private void addAttractionButton_Click(object sender, RoutedEventArgs e)
        {
            Attraction? attraction = attractionsListBox.SelectedItem as Attraction;
            if (attraction == null)
            {
                MessageBox.Show("Select a place first.", "TripMate");
                return;
            }
            TripDay? day = attractionDayComboBox.SelectedItem as TripDay;
            if (day == null) return;

            AddActivity(attraction.Name, day.DayNumber, 60, attraction.Latitude, attraction.Longitude, "From attraction search");
        }

        // Open the Wikipedia page in the default browser (like OpenUrl in the Week 10 tutorial)
        private void openAttractionButton_Click(object sender, RoutedEventArgs e)
        {
            Attraction? attraction = attractionsListBox.SelectedItem as Attraction;
            if (attraction == null) return;

            try
            {
                Process.Start(new ProcessStartInfo(attraction.Url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not open the browser: " + ex.Message, "TripMate");
            }
        }
    }
}