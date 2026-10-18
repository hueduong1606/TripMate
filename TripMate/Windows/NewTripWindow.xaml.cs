using System.Windows;
using System.Windows.Controls;
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

        // The destination name those coordinates belong to
        private string? lookedUpDestination;

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

                // Remember what was searched for, in case the user edits the box while waiting
                string searchedFor = destinationTextBox.Text.Trim();
                GeoPlace? place = await geocodingService.FindCityAsync(searchedFor);

                // The destination was changed during the search, so this result is out of date
                if (destinationTextBox.Text.Trim() != searchedFor)
                {
                    locationText.Text = "The destination changed during the search. Please look it up again.";
                    return;
                }

                if (place == null)
                {
                    locationText.Text = "City not found. You can still create the trip without it.";
                    return;
                }

                // Save the coordinates and suggest the local currency
                latitude = place.Latitude;
                longitude = place.Longitude;
                // Set this before changing the text, so TextChanged keeps the coordinates
                lookedUpDestination = place.Name;
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

        // The destination text changed: forget coordinates that belong to a different place
        private void destinationTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (latitude.HasValue && destinationTextBox.Text.Trim() != lookedUpDestination)
            {
                latitude = null;
                longitude = null;
                lookedUpDestination = null;
                locationText.Text = "Look up the city to enable weather and attraction search.";
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

            // The group settles up in this currency, so live rates must be available for it
            string homeCurrency = homeCurrencyComboBox.Text.Trim().ToUpper();
            string localCurrency = localCurrencyComboBox.Text.Trim().ToUpper();
            if (!CurrencyService.IsSupported(homeCurrency))
            {
                MessageBox.Show("Please choose a currency from the list to settle up in, e.g. AUD.", "TripMate");
                return;
            }
            // The local currency can be any 3-letter code (e.g. VND), even without live rates
            if (localCurrency.Length != 3 || !localCurrency.All(char.IsLetter))
            {
                MessageBox.Show("The local currency must be a 3-letter code, e.g. JPY.", "TripMate");
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
                HomeCurrency = homeCurrency,
                LocalCurrency = localCurrency
            };
            foreach (string name in names)
            {
                NewTrip.Members.Add(new Member { Name = name });
            }

            DialogResult = true;
        }
    }
}