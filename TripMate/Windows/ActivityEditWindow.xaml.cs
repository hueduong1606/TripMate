using System.Windows;
using TripMate.Core.Models;
using TripMate.Core.Services;

namespace TripMate.Windows
{
    // Dialog window to edit one activity: name, day, duration, location and notes.
    // It only changes the Activity object; MainWindow saves it to the database.
    public partial class ActivityEditWindow : Window
    {
        private readonly Activity activity;
        private readonly IGeocodingService geocodingService;

        // Constructor overloading: MainWindow normally uses the short version...
        public ActivityEditWindow(Activity activity, List<TripDay> days, string destination)
            : this(activity, days, destination, new GeocodingService())
        {
        }

        // ...and this version accepts any IGeocodingService, so the window is not tied to one web API
        public ActivityEditWindow(Activity activity, List<TripDay> days, string destination, IGeocodingService geocodingService)
        {
            InitializeComponent();
            this.activity = activity;
            this.geocodingService = geocodingService;
            Destination = destination;

            // Show the current values
            nameTextBox.Text = activity.Name;
            dayComboBox.ItemsSource = days;
            dayComboBox.SelectedItem = days.FirstOrDefault(d => d.DayNumber == activity.DayNumber) ?? days[0];
            durationTextBox.Text = activity.DurationMinutes.ToString();
            latitudeTextBox.Text = activity.Latitude.HasValue ? activity.Latitude.Value.ToString("0.000000") : "";
            longitudeTextBox.Text = activity.Longitude.HasValue ? activity.Longitude.Value.ToString("0.000000") : "";
            notesTextBox.Text = activity.Notes;
            searchTextBox.Text = activity.Name;
        }

        // Destination city, added to location searches
        public string Destination { get; private set; }

        // Method to look up the location with OpenStreetMap
        private async void findButton_Click(object sender, RoutedEventArgs e)
        {
            string query = searchTextBox.Text.Trim();
            if (query == "")
            {
                MessageBox.Show("Type a place name or address to search for.", "TripMate");
                return;
            }
            if (!query.Contains(Destination, StringComparison.OrdinalIgnoreCase))
            {
                query = query + ", " + Destination;
            }

            // Remember the coordinates before searching, to notice if the user types new ones meanwhile
            string latitudeBefore = latitudeTextBox.Text;
            string longitudeBefore = longitudeTextBox.Text;

            try
            {
                // Don't let the window be saved half-way through a search
                findButton.IsEnabled = false;
                saveButton.IsEnabled = false;
                GeoPlace? place = await geocodingService.FindPlaceAsync(query);
                if (place == null)
                {
                    locationInfoText.Text = "No match found. You can type the coordinates instead.";
                    return;
                }

                // The user typed coordinates by hand while waiting: keep theirs
                if (latitudeTextBox.Text != latitudeBefore || longitudeTextBox.Text != longitudeBefore)
                {
                    locationInfoText.Text = "Found " + place.Name + ", but you edited the coordinates, so yours were kept.";
                    return;
                }

                latitudeTextBox.Text = place.Latitude.ToString("0.000000");
                longitudeTextBox.Text = place.Longitude.ToString("0.000000");
                locationInfoText.Text = place.Name;
            }
            catch (Exception ex)
            {
                // Handle exceptions, e.g. no internet connection
                locationInfoText.Text = "Location search failed: " + ex.Message;
            }
            finally
            {
                findButton.IsEnabled = true;
                saveButton.IsEnabled = true;
            }
        }

        // Remove the location from the activity
        private void clearLocationButton_Click(object sender, RoutedEventArgs e)
        {
            latitudeTextBox.Clear();
            longitudeTextBox.Clear();
            locationInfoText.Text = "Location removed. This stop will not be included in route optimisation.";
        }

        // Method to validate the input and copy it into the Activity object
        private void saveButton_Click(object sender, RoutedEventArgs e)
        {
            string name = nameTextBox.Text.Trim();
            if (name == "")
            {
                MessageBox.Show("Please enter the activity name.", "TripMate");
                return;
            }
            TripDay? day = dayComboBox.SelectedItem as TripDay;
            if (day == null)
            {
                MessageBox.Show("Please choose a day.", "TripMate");
                return;
            }
            if (!int.TryParse(durationTextBox.Text, out int duration) || duration < 0 || duration > 24 * 60)
            {
                MessageBox.Show("Duration must be a whole number of minutes between 0 and 1440.", "TripMate");
                return;
            }

            double? lat = null, lon = null;
            if (latitudeTextBox.Text.Trim() != "" || longitudeTextBox.Text.Trim() != "")
            {
                if (!double.TryParse(latitudeTextBox.Text, out double latValue) || !double.TryParse(longitudeTextBox.Text, out double lonValue) ||
                    !double.IsFinite(latValue) || !double.IsFinite(lonValue) ||
                    latValue < -90 || latValue > 90 || lonValue < -180 || lonValue > 180)
                {
                    MessageBox.Show("Please enter a valid latitude (-90 to 90) and longitude (-180 to 180), or leave both empty.", "TripMate");
                    return;
                }
                lat = latValue;
                lon = lonValue;
            }

            // Copy the new values into the activity
            activity.Name = name;
            activity.DayNumber = day.DayNumber;
            activity.DurationMinutes = duration;
            activity.Latitude = lat;
            activity.Longitude = lon;
            activity.Notes = notesTextBox.Text.Trim();

            DialogResult = true;
        }
    }
}