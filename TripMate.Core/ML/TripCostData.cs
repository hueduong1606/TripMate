using Microsoft.ML.Data;

namespace TripMate.Core.ML
{
    // One row of the training data (one past student trip).
    // LoadColumn = the column number in trip_costs_sample.csv
    public class TripCostData
    {
        [LoadColumn(0)]
        public string Destination { get; set; } = "";

        [LoadColumn(1)]
        public string AccommodationType { get; set; } = "";

        [LoadColumn(2)]
        public float Days { get; set; }

        [LoadColumn(3)]
        public float Travellers { get; set; }

        [LoadColumn(4)]
        public float ActivitiesPerDay { get; set; }

        // The value we want to predict is called the "Label" in ML.NET
        [LoadColumn(5), ColumnName("Label")]
        public float CostPerPerson { get; set; }
    }

    // What the model returns. Regression models put their answer in "Score".
    public class TripCostPrediction
    {
        [ColumnName("Score")]
        public float CostPerPerson { get; set; }
    }
}
