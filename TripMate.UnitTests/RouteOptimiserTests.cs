using TripMate.Core.Algorithms;
using TripMate.Core.Models;

namespace TripMate.UnitTests
{
    [TestFixture]
    public class RouteOptimiserTests
    {
        // Helper to create an activity at a position
        private static Activity Stop(string name, double? lat, double? lon)
        {
            return new Activity { Name = name, Latitude = lat, Longitude = lon };
        }

        // Sydney to Melbourne is about 714 km in a straight line
        [Test]
        public void Test_Haversine_SydneyToMelbourne()
        {
            // Act
            double km = GeoExtensions.HaversineKm(-33.8688, 151.2093, -37.8136, 144.9631);

            // Assert
            Assert.That(km, Is.EqualTo(714).Within(5), "Sydney to Melbourne should be about 714 km.");
        }

        // A stop without coordinates cannot be measured
        [Test]
        public void Test_DistanceTo_NeedsCoordinates()
        {
            // Act and Assert
            Assert.Throws<ArgumentException>(() => Stop("A", 1, 1).DistanceTo(Stop("B", null, null)));
        }

        // Stops along a line are visited in order
        [Test]
        public void Test_Optimise_StopsOnALine()
        {
            // Arrange: four stops on a straight line, entered in a zig-zag order
            List<Activity> day = new List<Activity>
            {
                Stop("Hostel", 0, 0), Stop("Far", 0, 0.03), Stop("Near", 0, 0.01), Stop("Middle", 0, 0.02)
            };
            RouteOptimiser optimiser = new RouteOptimiser();

            // Act
            RouteResult<Activity> result = optimiser.Optimise(day);

            // Assert: visits them in order along the line, starting at the hostel
            Assert.That(string.Join(",", result.Route.Select(a => a.Name)), Is.EqualTo("Hostel,Near,Middle,Far"));
            Assert.That(result.OptimisedDistanceKm, Is.LessThan(result.OriginalDistanceKm));
            Assert.That(result.KmSaved, Is.GreaterThan(0));
        }

        // The first stop stays first, and stops without a location go to the end
        [Test]
        public void Test_Optimise_KeepsFirstStopAndUnlocated()
        {
            // Arrange
            List<Activity> day = new List<Activity>
            {
                Stop("Hostel", 0, 0), Stop("Cooking class", null, null), Stop("B", 0, 2), Stop("A", 0, 1)
            };

            // Act
            RouteResult<Activity> result = new RouteOptimiser().Optimise(day);

            // Assert
            Assert.That(result.Route[0].Name, Is.EqualTo("Hostel"), "The first stop should not move.");
            Assert.That(result.Route.Count, Is.EqualTo(3), "Only stops with a location are in the route.");
            Assert.That(result.Unlocated.Single().Name, Is.EqualTo("Cooking class"), "The stop without a location should be listed separately.");
        }

        // 2-opt removes a crossing from a route
        [Test]
        public void Test_TwoOpt_FixesCrossingRoute()
        {
            // Arrange: a route that crosses itself (0,0) -> (1,1) -> (0,1) -> (1,0) -> (2,0)
            // Using a simple "Manhattan" distance delegate instead of Haversine
            DistanceCalculator manhattan = (a, b) => Math.Abs(a.Latitude!.Value - b.Latitude!.Value) + Math.Abs(a.Longitude!.Value - b.Longitude!.Value);
            RouteOptimiser optimiser = new RouteOptimiser(manhattan);

            List<Activity> crossing = new List<Activity>
            {
                Stop("S", 0, 0), Stop("A", 1, 1), Stop("B", 0, 1), Stop("C", 1, 0), Stop("D", 2, 0)
            };
            double before = optimiser.RouteLength(crossing);

            // Act
            List<Activity> improved = optimiser.TwoOpt(crossing);

            // Assert
            Assert.That(improved[0].Name, Is.EqualTo("S"), "The first stop should not move.");
            Assert.That(optimiser.RouteLength(improved), Is.LessThan(before), "2-opt should make the route shorter.");
        }

        // On 50 random days the optimised route is never longer than the original
        [Test]
        public void Test_Optimise_NeverWorse()
        {
            // Arrange: random stops with a fixed seed, so the test always gives the same result
            Random random = new Random(42);
            RouteOptimiser optimiser = new RouteOptimiser();
            for (int trial = 0; trial < 50; trial++)
            {
                List<Activity> day = new List<Activity>();
                for (int i = 0; i < 8; i++)
                {
                    day.Add(Stop("S" + i, -33.8 + random.NextDouble() * 0.1, 151.2 + random.NextDouble() * 0.1));
                }
                // Act
                RouteResult<Activity> result = optimiser.Optimise(day);

                // Assert
                Assert.That(result.OptimisedDistanceKm, Is.LessThanOrEqualTo(result.OriginalDistanceKm + 1e-9), "The optimised route should never be longer.");
                Assert.That(result.Route.Count, Is.EqualTo(8), "No stop should be lost.");
                Assert.That(result.Route[0], Is.SameAs(day[0]), "The first stop should not move.");
            }
        }

        // Each distance takes 78 minutes of travel with the 1.3 detour factor
        // (5 km walking, 20 km by public transport, 35 km driving);
        // public transport then adds a 5 minute wait, and driving adds 8 minutes to park
        // Polymorphism: each TravelMode subclass works out the time its own way
        [TestCase(0, 5, 78)]
        [TestCase(1, 20, 83)]
        [TestCase(2, 35, 86)]
        [Test]
        public void Test_TravelMode_Polymorphism(int modeIndex, double km, double expectedMinutes)
        {
            // Arrange: the base class variable holds a subclass object
            TravelMode mode = TravelMode.GetAll()[modeIndex];

            // Act: the subclass's own MinutesFor override runs
            double minutes = mode.MinutesFor(km);

            // Assert
            Assert.That(minutes, Is.EqualTo(expectedMinutes).Within(0.01), mode.Name);
        }

        // Both TravelMinutes overloads add up the travel time of a route
        [Test]
        public void Test_TravelMinutes_Overloads()
        {
            // Arrange: two legs of 1 km each
            List<Activity> route = new List<Activity> { Stop("A", 0, 0), Stop("B", 0, 1), Stop("C", 0, 2) };
            DistanceCalculator oneKmPerDegree = (a, b) => Math.Abs(a.Longitude!.Value - b.Longitude!.Value);
            RouteOptimiser optimiser = new RouteOptimiser(oneKmPerDegree);

            // Act. Overload 1: a TravelMode object (5 min wait per leg for public transport)
            double byMode = optimiser.TravelMinutes(route, new PublicTransportMode());
            // Overload 2: any Func<double, double>, here a lambda of 10 minutes per km
            double byLambda = optimiser.TravelMinutes(route, km => km * 10);

            // Assert
            Assert.That(byMode, Is.EqualTo(2 * (1 * 1.3 / 20 * 60 + 5)).Within(0.001), "Public transport: 3.9 minutes travel plus 5 minutes wait, per leg.");
            Assert.That(byLambda, Is.EqualTo(20).Within(0.001), "10 minutes per km over 2 km.");
        }

        // IComparable sorts activities by day first, then by order within the day
        [Test]
        public void Test_Activity_Sorting()
        {
            // Arrange
            List<Activity> list = new List<Activity>
            {
                new Activity { Name = "C", DayNumber = 2, Order = 1 },
                new Activity { Name = "B", DayNumber = 1, Order = 2 },
                new Activity { Name = "A", DayNumber = 1, Order = 1 }
            };
            // Act
            list.Sort();

            // Assert
            Assert.That(string.Join("", list.Select(a => a.Name)), Is.EqualTo("ABC"), "Activities should be sorted by day, then order.");
        }
    }
}