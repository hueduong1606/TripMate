namespace TripMate.Core.Algorithms
{
    // Base class for the different ways of getting around a city
    // (Week 5: Inheritance, Abstract Classes and Method Overriding).
    // The route optimiser only knows about TravelMode, so a new mode (e.g. cycling)
    // can be added as a new subclass without changing any other code.
    public abstract class TravelMode
    {
        // Straight-line distance is shorter than the real streets, so every mode
        // multiplies it by this factor
        public const double DetourFactor = 1.3;

        // Each subclass must say what it is called and how fast it is
        public abstract string Name { get; }
        public abstract double SpeedKmh { get; }

        // Method to estimate the minutes needed for one leg of a route.
        // Virtual, so a subclass can add extra time (see PublicTransportMode).
        public virtual double MinutesFor(double km)
        {
            return km * DetourFactor / SpeedKmh * 60.0;
        }

        // Override the ToString() method so the ComboBox shows the name
        public override string ToString()
        {
            return Name;
        }

        // Method to create one object of every travel mode (shown in the ComboBox)
        public static List<TravelMode> GetAll()
        {
            return new List<TravelMode> { new WalkingMode(), new PublicTransportMode(), new DrivingMode() };
        }
    }

    public class WalkingMode : TravelMode
    {
        public override string Name { get { return "Walking"; } }
        public override double SpeedKmh { get { return 5.0; } }
    }

    public class PublicTransportMode : TravelMode
    {
        // Average wait at each stop or platform
        public const double WaitMinutes = 5.0;

        public override string Name { get { return "Public transport"; } }
        public override double SpeedKmh { get { return 20.0; } }

        // Override: travel time plus the wait for the next bus or train
        public override double MinutesFor(double km)
        {
            return base.MinutesFor(km) + WaitMinutes;
        }
    }

    public class DrivingMode : TravelMode
    {
        // Time to find a park at each stop
        public const double ParkingMinutes = 8.0;

        public override string Name { get { return "Driving"; } }
        public override double SpeedKmh { get { return 35.0; } }

        // Override: travel time plus parking
        public override double MinutesFor(double km)
        {
            return base.MinutesFor(km) + ParkingMinutes;
        }
    }
}