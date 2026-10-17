using TripMate.Core.Models;

namespace TripMate.UnitTests
{
    public class ActivityLocationTests
    {
        [TestCase(double.NaN, 0)]
        [TestCase(0, double.NaN)]
        [TestCase(double.PositiveInfinity, 0)]
        [TestCase(0, double.NegativeInfinity)]
        [TestCase(91, 0)]
        [TestCase(0, 181)]
        public void InvalidCoordinatesCannotBeRouted(double latitude, double longitude)
        {
            var activity = new Activity { Latitude = latitude, Longitude = longitude };
            Assert.That(activity.HasLocation, Is.False);
        }

        [TestCase(-90, -180)]
        [TestCase(90, 180)]
        [TestCase(0, 0)]
        public void ValidCoordinatesCanBeRouted(double latitude, double longitude)
        {
            var activity = new Activity { Latitude = latitude, Longitude = longitude };
            Assert.That(activity.HasLocation, Is.True);
        }
    }
}
