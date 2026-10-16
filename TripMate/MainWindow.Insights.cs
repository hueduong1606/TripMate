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
        }
    }
}
