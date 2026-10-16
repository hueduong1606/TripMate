using Microsoft.ML;
using Microsoft.ML.Data;

/*
 ML.NET regression model that predicts what a trip will cost per person (in AUD,
 flights from Sydney included), from the destination, accommodation type, number of
 days, number of travellers and activities per day.

 Steps:
   1. Load the sample data from the CSV file
   2. Split it into training data (80%) and test data (20%)
   3. Build the pipeline: turn the text columns into numbers (one-hot encoding),
      join every input into a single "Features" column, then a FastTree regression trainer
   4. Train the model and measure how accurate it is on the test data
   5. Use a PredictionEngine to predict new trips
*/

namespace TripMate.Core.ML
{
    public class CostPredictor
    {
        private readonly MLContext mlContext;
        private ITransformer? model;
        private PredictionEngine<TripCostData, TripCostPrediction>? predictionEngine;

        public CostPredictor()
        {
            // Fixed seed so the model is the same every time it is trained
            mlContext = new MLContext(seed: 1);
            Destinations = new List<string>();
        }

        // Model quality on the test data
        public double RSquared { get; private set; }
        public double MeanAbsoluteError { get; private set; }
        public int TrainingRows { get; private set; }

        // Destinations found in the training data (shown in the ComboBox)
        public List<string> Destinations { get; private set; }

        public bool IsTrained
        {
            get { return predictionEngine != null; }
        }

        // Default location of the bundled sample data
        public static string DefaultDataPath
        {
            get { return Path.Combine(AppContext.BaseDirectory, "Data", "trip_costs_sample.csv"); }
        }

        // Method to train the model from a CSV file
        public void Train(string csvPath)
        {
            // Step 1: load the data
            IDataView data = mlContext.Data.LoadFromTextFile<TripCostData>(csvPath, hasHeader: true, separatorChar: ',');

            // Remember the destinations and row count using LINQ on the loaded rows
            List<TripCostData> rows = mlContext.Data.CreateEnumerable<TripCostData>(data, reuseRowObject: false).ToList();
            TrainingRows = rows.Count;
            Destinations = rows.Select(r => r.Destination).Distinct().OrderBy(d => d).ToList();

            // Step 2: split into training and test data
            DataOperationsCatalog.TrainTestData split = mlContext.Data.TrainTestSplit(data, testFraction: 0.2);

            // Step 3: build the pipeline
            var pipeline = mlContext.Transforms.Categorical.OneHotEncoding("DestinationEncoded", "Destination")
                .Append(mlContext.Transforms.Categorical.OneHotEncoding("AccommodationEncoded", "AccommodationType"))
                .Append(mlContext.Transforms.Concatenate("Features", "DestinationEncoded", "AccommodationEncoded", "Days", "Travellers", "ActivitiesPerDay"))
                .Append(mlContext.Regression.Trainers.FastTree(numberOfLeaves: 8, numberOfTrees: 200, minimumExampleCountPerLeaf: 4, learningRate: 0.1));

            // Step 4: train, then test on data the model has not seen
            model = pipeline.Fit(split.TrainSet);
            RegressionMetrics metrics = mlContext.Regression.Evaluate(model.Transform(split.TestSet));
            RSquared = metrics.RSquared;
            MeanAbsoluteError = metrics.MeanAbsoluteError;

            // Step 5: create the prediction engine
            predictionEngine = mlContext.Model.CreatePredictionEngine<TripCostData, TripCostPrediction>(model);
        }

        // Method to predict the cost per person for a new trip
        public float PredictCostPerPerson(string destination, string accommodationType, int days, int travellers, int activitiesPerDay)
        {
            if (predictionEngine == null)
            {
                throw new InvalidOperationException("Train the model before making a prediction.");
            }

            TripCostData input = new TripCostData
            {
                Destination = destination,
                AccommodationType = accommodationType,
                Days = days,
                Travellers = travellers,
                ActivitiesPerDay = activitiesPerDay
            };
            return predictionEngine.Predict(input).CostPerPerson;
        }
    }
}
