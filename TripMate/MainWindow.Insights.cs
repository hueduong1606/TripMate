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
        // Called from the MainWindow constructor
        partial void SetupInsightsTab()
        {
            refreshTripViews += ShowCharts;
            tabChanged += InsightsTabChanged;
        }

        // When the Insights tab is opened, redraw the charts and train the model the first time
        private void InsightsTabChanged()
        {
            if (mainTabs.SelectedItem != insightsTab) return;
            ShowCharts();
        }

        // Method to draw the three spending charts with OxyPlot
        private void ShowCharts()
        {
            if (currentTrip == null) return;

            // 1. Pie chart: spending by category (LINQ GroupBy, like the Week 7 file extension example)
            PlotModel categoryModel = new PlotModel();
            PieSeries pie = new PieSeries { StrokeThickness = 1, InsideLabelPosition = 0.6, AngleSpan = 360, StartAngle = -90 };
            var byCategory = currentTrip.SpendingExpenses
                .GroupBy(x => x.Category)
                .Select(g => new { Category = g.Key, Total = g.Sum(x => x.Amount) })
                .OrderByDescending(g => g.Total);
            foreach (var c in byCategory)
            {
                pie.Slices.Add(new PieSlice(c.Category, (double)c.Total));
            }
            categoryModel.Series.Add(pie);
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
            dailyModel.Axes.Add(new DateTimeAxis { Position = AxisPosition.Bottom, StringFormat = "dd MMM", IntervalType = DateTimeIntervalType.Days, MinorIntervalType = DateTimeIntervalType.Days });
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
    }
}
