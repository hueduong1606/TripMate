using TripMate.Core.ML;

namespace TripMate.UnitTests
{
    [TestFixture]
    public class CostPredictorTests
    {
        // The model trains on the sample data, is accurate enough, and its predictions make sense
        [Test]
        public void Test_CostPredictor_TrainAndPredict()
        {
            // Arrange
            CostPredictor predictor = new CostPredictor();

            // Act
            predictor.Train(CostPredictor.DefaultDataPath);
            float shortTokyo = predictor.PredictCostPerPerson("Tokyo", "Hostel", 4, 4, 2);
            float longTokyo = predictor.PredictCostPerPerson("Tokyo", "Hostel", 12, 4, 2);
            float melbourne = predictor.PredictCostPerPerson("Melbourne", "Hostel", 4, 4, 2);

            // Assert
            Assert.That(predictor.IsTrained, Is.True, "The model should be trained.");
            Assert.That(predictor.TrainingRows, Is.GreaterThan(100), "Most of the sample rows should be used for training.");
            Assert.That(predictor.RSquared, Is.GreaterThan(0.8), "The model should explain most of the variation in cost.");
            // Longer trips should cost more, and Tokyo should cost more than Melbourne
            Assert.That(longTokyo, Is.GreaterThan(shortTokyo), "A 12 day trip should cost more than a 4 day trip.");
            Assert.That(shortTokyo, Is.GreaterThan(melbourne), "Tokyo should cost more than Melbourne.");
            TestContext.WriteLine($"R2 = {predictor.RSquared:0.000}, MAE = ${predictor.MeanAbsoluteError:0}, 4 days Tokyo = ${shortTokyo:0}");
        }
    }
}
