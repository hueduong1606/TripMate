using System.Windows;
using TripMate.Core.Models;
using TripMate.Core.Services;

namespace TripMate.Windows
{
    // Dialog window to create a new trip. MainWindow opens it with ShowDialog()
    // and reads the NewTrip property when the user clicks Create Trip.
    public partial class NewTripWindow : Window
    {
        private readonly IGeocodingService geocodingService = new GeocodingService();

        // Coordinates found by the Look up button
        private double? latitude, longitude;

        public NewTripWindow()
        {
            InitializeComponent();

            startDatePicker.SelectedDate = DateTime.Today.AddDays(14);
            endDatePicker.SelectedDate = DateTime.Today.AddDays(18);

            // Fill the currency lists
            foreach (string currency in CurrencyService.SupportedCurrencies)
            {
                homeCurrencyComboBox.Items.Add(currency);
                localCurrencyComboBox.Items.Add(currency);
            }
            homeCurrencyComboBox.Text = "AUD";
            localCurrencyComboBox.Text = "AUD";
        }

        // The trip created by this window (null if the user cancelled)
        public Trip? NewTrip { get; private set; }

        // Method to look up the destination with the Open-Meteo geocoding API
        private async void lookupButton_Click(object sender, RoutedEventArgs e)
        {
            if (destinationTextBox.Text.Trim() == "")
            {
                MessageBox.Show("Please type a destination first.", "TripMate");
                return;
            }

            try
            {
                lookupButton.IsEnabled = false;
                locationText.Text = "Searching...";

                GeoPlace? place = await geocodingService.FindCityAsync(destinationTextBox.Text.Trim());
                if (place == null)
                {
                    locationText.Text = "City not found. You can still create the trip without it.";
                    return;
                }

                // Save the coordinates and suggest the local currency
                latitude = place.Latitude;
                longitude = place.Longitude;
                destinationTextBox.Text = place.Name;
                localCurrencyComboBox.Text = CurrencyService.CurrencyForCountry(place.CountryCode);
                locationText.Text = "Found: " + place + " (" + place.Latitude.ToString("0.00") + ", " + place.Longitude.ToString("0.00") + ")";
            }
            catch (Exception ex)
            {
                // Handle exceptions, e.g. no internet connection
                locationText.Text = "Could not reach the location service: " + ex.Message;
            }
            finally
            {
                lookupButton.IsEnabled = true;
            }
        }

        // Method to check the input and build the new Trip object
        private void createButton_Click(object sender, RoutedEventArgs e)
        {
            // Validate the input
            if (tripNameTextBox.Text.Trim() == "" || destinationTextBox.Text.Trim() == "")
            {
                MessageBox.Show("Please enter a trip name and a destination.", "TripMate");
                return;
            }
            if (startDatePicker.SelectedDate == null || endDatePicker.SelectedDate == null)
            {
                MessageBox.Show("Please choose the start and end dates.", "TripMate");
                return;
            }
            if (endDatePicker.SelectedDate < startDatePicker.SelectedDate)
            {
                MessageBox.Show("The end date must be on or after the start date.", "TripMate");
                return;
            }

            // Split the comma separated names into fields, ignoring blanks and duplicates (LINQ)
            List<string> names = membersTextBox.Text.Split(',')
                .Select(n => n.Trim())
                .Where(n => n != "")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (names.Count == 0)
            {
                MessageBox.Show("Please add at least one traveller.", "TripMate");
                return;
            }

            // Create the trip and its members
            NewTrip = new Trip
            {
                Name = tripNameTextBox.Text.Trim(),
                Destination = destinationTextBox.Text.Trim(),
                Latitude = latitude,
                Longitude = longitude,
                StartDate = startDatePicker.SelectedDate.Value,
                EndDate = endDatePicker.SelectedDate.Value,
                HomeCurrency = homeCurrencyComboBox.Text.Trim().ToUpper(),
                LocalCurrency = localCurrencyComboBox.Text.Trim().ToUpper()
            };
            foreach (string name in names)
            {
                NewTrip.Members.Add(new Member { Name = name });
            }

            DialogResult = true;
        }
    }
}