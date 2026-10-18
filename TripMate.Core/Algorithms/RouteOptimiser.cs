using TripMate.Core.Models;

/*
 Route optimisation for one day of the itinerary.
 The first stop (e.g. the hostel) stays where it is, and the rest of the stops
 are re-ordered to reduce the total travel distance:
   1. Nearest neighbour - always walk to the closest stop not yet visited
   2. 2-opt - keep reversing parts of the route while that makes it shorter
*/

namespace TripMate.Core.Algorithms
{
    // Delegate for any method that measures the distance between two places
    // The default is the Haversine extension method.
    public delegate double DistanceCalculator(IGeoLocated from, IGeoLocated to);

    // Result of optimising one day
    public class RouteResult<T> where T : IGeoLocated
    {
        public RouteResult()
        {
            Route = new List<T>();
            Unlocated = new List<T>();
        }

        // Stops in the new visiting order (stops without coordinates are not included)
        public List<T> Route { get; set; }

        // Stops that have no coordinates, so they could not be routed
        public List<T> Unlocated { get; set; }

        public double OriginalDistanceKm { get; set; }
        public double OptimisedDistanceKm { get; set; }

        public double KmSaved
        {
            get { return OriginalDistanceKm - OptimisedDistanceKm; }
        }
    }

    public class RouteOptimiser
    {
        private readonly DistanceCalculator distance;

        // Use the Haversine distance by default
        public RouteOptimiser() : this(new DistanceCalculator(GeoExtensions.DistanceTo))
        {
        }

        // Or pass in any other method that matches the delegate (used by the unit tests)
        public RouteOptimiser(DistanceCalculator distance)
        {
            this.distance = distance;
        }

        // Method to optimise the visiting order. The stops must be passed in the order the user currently has them.
        public RouteResult<T> Optimise<T>(IList<T> stops) where T : IGeoLocated
        {
            RouteResult<T> result = new RouteResult<T>();

            // Split the stops into the ones we can route and the ones we cannot (LINQ)
            List<T> located = stops.Where(s => HasValidCoordinates(s)).ToList();
            result.Unlocated = stops.Where(s => !HasValidCoordinates(s)).ToList();

            result.OriginalDistanceKm = RouteLength(located);

            // Step 1: build a good first route with nearest neighbour
            List<T> route = NearestNeighbour(located);

            // Step 2: improve it with 2-opt
            route = TwoOpt(route);

            // Never return a route that is worse than what the user already had
            if (RouteLength(route) > result.OriginalDistanceKm)
            {
                route = located;
            }

            result.Route = route;
            result.OptimisedDistanceKm = RouteLength(route);
            return result;
        }

        // Reject missing, non-finite and out-of-range coordinates before routing.
        private static bool HasValidCoordinates(IGeoLocated stop)
        {
            return stop.Latitude.HasValue && stop.Longitude.HasValue
                && double.IsFinite(stop.Latitude.Value) && double.IsFinite(stop.Longitude.Value)
                && stop.Latitude.Value >= -90 && stop.Latitude.Value <= 90
                && stop.Longitude.Value >= -180 && stop.Longitude.Value <= 180;
        }

        // Method to calculate the total length of a route (start to finish, no return trip)
        public double RouteLength<T>(IList<T> route) where T : IGeoLocated
        {
            double total = 0.0;
            for (int i = 0; i < route.Count - 1; i++)
            {
                total += distance(route[i], route[i + 1]);
            }
            return total;
        }

        // Method to estimate the travel time of a route in minutes, leg by leg.
        // Overloaded: pass any Func<double, double>...
        public double TravelMinutes<T>(IList<T> route, Func<double, double> minutesForLeg) where T : IGeoLocated
        {
            double minutes = 0.0;
            for (int i = 0; i < route.Count - 1; i++)
            {
                minutes += minutesForLeg(distance(route[i], route[i + 1]));
            }
            return minutes;
        }

        // ...or a TravelMode object. Its MinutesFor method is passed on as the Func,
        // and the right override (walking, public transport or driving) is called at run time.
        public double TravelMinutes<T>(IList<T> route, TravelMode mode) where T : IGeoLocated
        {
            return TravelMinutes(route, mode.MinutesFor);
        }

        // Nearest neighbour: start at the first stop and always go to the
        // closest stop that has not been visited yet
        public List<T> NearestNeighbour<T>(IList<T> stops) where T : IGeoLocated
        {
            List<T> route = new List<T>();
            if (stops.Count == 0) return route;

            List<T> remaining = new List<T>(stops);
            T current = remaining[0];
            remaining.RemoveAt(0);
            route.Add(current);

            while (remaining.Count > 0)
            {
                // Find the closest remaining stop using LINQ
                T next = remaining.OrderBy(s => distance(current, s)).First();
                route.Add(next);
                remaining.Remove(next);
                current = next;
            }
            return route;
        }

        // 2-opt: try reversing every section of the route (except the first stop).
        // If the reversed route is shorter, keep it, and repeat until nothing improves.
        public List<T> TwoOpt<T>(IList<T> stops) where T : IGeoLocated
        {
            List<T> route = new List<T>(stops);
            int n = route.Count;
            bool improved = true;

            while (improved)
            {
                improved = false;
                for (int i = 1; i < n - 1; i++)
                {
                    for (int k = i + 1; k < n; k++)
                    {
                        // Distance change if the stops from i to k are visited in reverse:
                        // edge (i-1, i) becomes (i-1, k) and edge (k, k+1) becomes (i, k+1)
                        double before = distance(route[i - 1], route[i]);
                        double after = distance(route[i - 1], route[k]);
                        if (k < n - 1)
                        {
                            before += distance(route[k], route[k + 1]);
                            after += distance(route[i], route[k + 1]);
                        }

                        if (after < before - 1e-9)
                        {
                            route.Reverse(i, k - i + 1);
                            improved = true;
                        }
                    }
                }
            }
            return route;
        }
    }
}