using System.Windows;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Legends;
using OxyPlot.Series;
using TripMate.Core.ML;
using TripMate.Core.Models;

/*
 Insights tab: spending charts (OxyPlot) and the ML.NET trip cost prediction.
*/

namespace TripMate
{
    public partial class MainWindow
    {
        private readonly CostPredictor costPredictor = new CostPredictor();
        private int predictionRequestVersion;

        // Called from the MainWindow constructor
        partial void SetupInsightsTab()
        {
            foreach (string type in new[] { "Hostel", "Airbnb", "Hotel" }) accommodationComboBox.Items.Add(type);
            accommodationComboBox.SelectedIndex = 0;

            refreshTripViews += ShowCharts;
            tabChanged += InsightsTabChanged;
            tripSelected += ClearPrediction;
        }

        // When the Insights tab is opened, redraw the charts and train the model the first time
        private void InsightsTabChanged()
        {
            if (mainTabs.SelectedItem != insightsTab) return;
            ShowCharts();
            TrainModelIfNeeded();
        }

        // Clear the old prediction when a different trip is chosen
        private void ClearPrediction()
        {
            predictionRequestVersion++;
            predictionResultText.Text = "";
        }

        // Method to draw the three spending charts with OxyPlot
        private void ShowCharts()
        {
            if (currentTrip == null) return;

            // 1. Pie chart: spending by category (LINQ GroupBy, like the Week 7 file extension example)
            PlotModel categoryModel = new PlotModel();
            PieSeries pie = new PieSeries { StrokeThickness = 1, InsideLabelPosition = 0.7, InsideLabelFormat = "{2:0}%", OutsideLabelFormat = null, AngleSpan = 360, StartAngle = -90 };
            var byCategory = currentTrip.SpendingExpenses
                .GroupBy(x => x.Category)
                .Select(g => new { Category = g.Key, Total = g.Sum(x => x.Amount) })
                .OrderByDescending(g => g.Total);
            foreach (var c in byCategory)
            {
                pie.Slices.Add(new PieSlice(c.Category, (double)c.Total));
            }
            categoryModel.Series.Add(pie);
            // Keep category names outside small slices so their labels cannot overlap.
            double categoryTotal = pie.Slices.Sum(s => s.Value);
            categoryLegendText.Text = categoryTotal > 0
                ? string.Join("\n", pie.Slices.Select(s => s.Label + " (" + (s.Value / categoryTotal * 100).ToString("0") + "%)"))
                : "";
            if (pie.Slices.Count == 0) categoryModel.Title = "No expenses yet";
            categoryPlot.Model = categoryModel;

            // 2. Bar chart: what each person paid vs their fair share
            PlotModel memberModel = new PlotModel();
            memberModel.Legends.Add(new Legend { LegendPosition = LegendPosition.BottomCenter, LegendOrientation = LegendOrientation.Horizontal, LegendPlacement = LegendPlacement.Outside });
            CategoryAxis nameAxis = new CategoryAxis { Position = AxisPosition.Left };
            memberModel.Axes.Add(nameAxis);
            memberModel.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, Minimum = 0, Title = currentTrip.HomeCurrency });

            BarSeries paidSeries = new BarSeries { Title = "Paid" };
            BarSeries shareSeries = new BarSeries { Title = "Fair share" };
            foreach (Member m in currentTrip.Members.OrderBy(m => m.Name))
            {
                decimal paid = currentTrip.SpendingExpenses.Where(x => x.PaidById == m.MemberId).Sum(x => x.Amount);
                decimal share = currentTrip.SpendingExpenses.SelectMany(x => x.Shares).Where(s => s.MemberId == m.MemberId).Sum(s => s.ShareAmount);
                nameAxis.Labels.Add(m.Name);
                paidSeries.Items.Add(new BarItem((double)paid));
                shareSeries.Items.Add(new BarItem((double)share));
            }
            memberModel.Series.Add(paidSeries);
            memberModel.Series.Add(shareSeries);
            memberPlot.Model = memberModel;

            // 3. Spending per day of the trip, plus the running total
            PlotModel dailyModel = new PlotModel();
            dailyModel.Legends.Add(new Legend { LegendPosition = LegendPosition.TopLeft });
            dailyModel.Axes.Add(new DateTimeAxis { Position = AxisPosition.Bottom, StringFormat = "dd MMM", IntervalType = DateTimeIntervalType.Days, MinorIntervalType = DateTimeIntervalType.Days, MinimumMajorStep = 1, MinimumPadding = 0.08, MaximumPadding = 0.08 });
            dailyModel.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Minimum = 0, Title = currentTrip.HomeCurrency });

            LinearBarSeries dailySeries = new LinearBarSeries { Title = "Spent that day", BarWidth = 20 };
            LineSeries runningSeries = new LineSeries { Title = "Running total", MarkerType = MarkerType.Circle };
            decimal running = 0m;
            var byDate = currentTrip.SpendingExpenses.GroupBy(x => x.Date.Date).OrderBy(g => g.Key);
            foreach (var d in byDate)
            {
                decimal dayTotal = d.Sum(x => x.Amount);
                running += dayTotal;
                dailySeries.Points.Add(new DataPoint(DateTimeAxis.ToDouble(d.Key), (double)dayTotal));
                runningSeries.Points.Add(new DataPoint(DateTimeAxis.ToDouble(d.Key), (double)running));
            }
            dailyModel.Series.Add(dailySeries);
            dailyModel.Series.Add(runningSeries);
            dailyPlot.Model = dailyModel;
        }

        // Train the ML.NET model the first time the Insights tab is opened.
        // Training runs on a background thread so the window does not freeze.
        private async void TrainModelIfNeeded()
        {
            if (costPredictor.IsTrained || !predictButton.IsEnabled) return;

            try
            {
                predictButton.IsEnabled = false;
                // Lock the form while training, because it is filled in automatically afterwards
                SetPredictionInputsEnabled(false);
                modelInfoText.Text = "Training the cost model...";
                await Task.Run(() => costPredictor.Train(CostPredictor.DefaultDataPath));

                predictDestinationComboBox.ItemsSource = costPredictor.Destinations;
                predictDestinationComboBox.SelectedIndex = 0;
                modelInfoText.Text = "FastTree regression trained on " + costPredictor.TrainingRows + " sample student trips (flights from Sydney included). " +
                                     "On test data it explains " + (costPredictor.RSquared * 100).ToString("0") + "% of the variation in cost (R²), " +
                                     "with an average error of $" + costPredictor.MeanAbsoluteError.ToString("0") + " per person. " +
                                     "The sample data is made up for this project, so treat predictions as a rough guide.";
                fillFromTripButton_Click(this, new RoutedEventArgs());
            }
            catch (Exception ex)
            {
                modelInfoText.Text = "Could not train the model: " + ex.Message;
            }
            finally
            {
                predictButton.IsEnabled = true;
                SetPredictionInputsEnabled(true);
            }
        }

        // Method to turn the prediction form on or off
        private void SetPredictionInputsEnabled(bool enabled)
        {
            predictDestinationComboBox.IsEnabled = enabled;
            accommodationComboBox.IsEnabled = enabled;
            predictDaysTextBox.IsEnabled = enabled;
            predictTravellersTextBox.IsEnabled = enabled;
            predictActivitiesTextBox.IsEnabled = enabled;
            fillFromTripButton.IsEnabled = enabled;
        }

        // Fill the prediction form with the current trip's details
        private void fillFromTripButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentTrip == null) return;

            // Pick the destination if the model knows it
            string? known = costPredictor.Destinations.FirstOrDefault(d => d.Equals(currentTrip.Destination, StringComparison.OrdinalIgnoreCase));
            if (known != null) predictDestinationComboBox.SelectedItem = known;

            predictDaysTextBox.Text = currentTrip.NumberOfDays.ToString();
            predictTravellersTextBox.Text = Math.Max(1, currentTrip.Members.Count).ToString();
            int perDay = (int)Math.Round((double)currentTrip.Activities.Count / currentTrip.NumberOfDays);
            predictActivitiesTextBox.Text = Math.Clamp(perDay, 1, 4).ToString();
        }

        // Validate the form and predict the cost per person with the ML.NET model
        private async void predictButton_Click(object sender, RoutedEventArgs e)
        {
            if (!costPredictor.IsTrained)
            {
                TrainModelIfNeeded();
                return;
            }

            // Validate the input
            string? destination = predictDestinationComboBox.SelectedItem as string;
            string? accommodation = accommodationComboBox.SelectedItem as string;
            if (destination == null || accommodation == null)
            {
                MessageBox.Show("Please choose a destination and accommodation type.", "TripMate");
                return;
            }
            if (!int.TryParse(predictDaysTextBox.Text, out int days) || days < 1 || days > 30 ||
                !int.TryParse(predictTravellersTextBox.Text, out int travellers) || travellers < 1 || travellers > 20 ||
                !int.TryParse(predictActivitiesTextBox.Text, out int activitiesPerDay) || activitiesPerDay < 0 || activitiesPerDay > 10)
            {
                MessageBox.Show("Days must be 1-30, travellers 1-20 and activities per day 0-10.", "TripMate");
                return;
            }

            // Keep the prediction tied to the trip and request that initiated it.
            Trip? predictionTrip = currentTrip;
            int requestVersion = ++predictionRequestVersion;
            string homeCurrency = predictionTrip?.HomeCurrency ?? "AUD";

            float perPersonAud = costPredictor.PredictCostPerPerson(destination, accommodation, days, travellers, activitiesPerDay);
            string result = "About $" + perPersonAud.ToString("0") + " AUD per person, $" + (perPersonAud * travellers).ToString("0") + " AUD for the group of " + travellers + ".";

            // Show it in the trip's home currency too, if that is not AUD
            if (predictionTrip != null && homeCurrency != "AUD")
            {
                try
                {
                    decimal converted = await currencyService.ConvertAsync((decimal)perPersonAud, "AUD", homeCurrency);
                    if (requestVersion != predictionRequestVersion || currentTrip?.TripId != predictionTrip.TripId) return;
                    result += " (" + Money(converted) + " per person)";
                }
                catch (Exception)
                {
                    // Conversion is optional, so ignore errors here
                }
            }

            if (requestVersion != predictionRequestVersion || currentTrip?.TripId != predictionTrip?.TripId) return;

            // Compare with what this trip has actually cost so far
            if (currentTrip != null && currentTrip.SpendingExpenses.Count > 0 && currentTrip.Members.Count > 0)
            {
                decimal spentPerPerson = currentTrip.SpendingExpenses.Sum(x => x.Amount) / currentTrip.Members.Count;
                result += "\nThis trip so far: " + Money(spentPerPerson) + " per person.";
            }

            predictionResultText.Text = result;
            SetStatus("Cost predicted");
        }
    }
}
