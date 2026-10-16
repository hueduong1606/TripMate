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

        // Called from the MainWindow constructor
        partial void SetupExploreTab()
        {
            tripSelected += ClearExploreResults;
        }

        // Clear the results from the previous trip
        private void ClearExploreResults()
        {
            weatherDataGrid.ItemsSource = null;
            weatherInfoText.Text = "";
        }

        // Method to make sure the trip has coordinates; looks up the destination if needed
        private async Task<bool> EnsureDestinationLocationAsync()
        {
            if (currentTrip == null) return false;
            if (currentTrip.Latitude.HasValue && currentTrip.Longitude.HasValue) return true;

            GeoPlace? place = await geocodingService.FindCityAsync(currentTrip.Destination);
            if (place == null)
            {
                MessageBox.Show("Could not find \"" + currentTrip.Destination + "\" on the map.", "TripMate");
                return false;
            }

            // Save the coordinates so we only look them up once
            using (var context = new Context())
            {
                Trip? trip = context.Trips.Find(currentTrip.TripId);
                if (trip != null)
                {
                    trip.Latitude = place.Latitude;
                    trip.Longitude = place.Longitude;
                    context.SaveChanges();
                }
            }
            currentTrip.Latitude = place.Latitude;
            currentTrip.Longitude = place.Longitude;
            ShowOverview();
            return true;
        }

        // Get the weather forecast for the destination
        private async void weatherButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentTrip == null) return;

            try
            {
                weatherButton.IsEnabled = false;
                SetStatus("Getting the forecast...");
                if (!await EnsureDestinationLocationAsync()) return;

                List<WeatherDay> forecast = await weatherService.GetForecastAsync(currentTrip.Latitude!.Value, currentTrip.Longitude!.Value, 16);
                weatherDataGrid.ItemsSource = forecast;

                // Tell the user which forecast days fall inside the trip
                int tripDaysInForecast = forecast.Count(d => d.Date >= currentTrip.StartDate.Date && d.Date <= currentTrip.EndDate.Date);
                weatherInfoText.Text = tripDaysInForecast > 0
                    ? tripDaysInForecast + " of the forecast days are during your trip."
                    : "Forecasts only reach 16 days ahead, so this is the weather there right now.";
                SetStatus("Forecast for " + currentTrip.Destination + " updated");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not get the weather: " + ex.Message, "TripMate");
                SetStatus("Weather unavailable");
            }
            finally
            {
                weatherButton.IsEnabled = true;
            }
        }
    }
}