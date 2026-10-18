# TripMate

TripMate is a Windows desktop application for planning group trips and sharing travel costs. It uses WPF on .NET 9, Entity Framework Core with SQLite, OxyPlot, and ML.NET.

## Features

- **Group:** create trips, manage members, and load a sample Tokyo trip.
- **Itinerary:** add and edit daily activities, find locations, reorder stops, and optimise routes for different travel modes.
- **Expenses:** record shared spending, convert foreign-currency amounts, view balances, calculate a transfer plan, and record repayments.
- **Explore:** find nearby attractions, view weather forecasts, and convert currencies.
- **Insights:** view spending charts and estimate trip costs with a model trained on bundled sample data.

## Requirements

- Windows is required to run the WPF application.
- Visual Studio with support for .NET 9 and the **.NET desktop development** workload. For the assignment, use the required Visual Studio 2022 lab environment with .NET 9 support.
- The .NET 9 SDK. A newer SDK can build the solution, but running the application and tests also requires compatible .NET 9 runtimes, including the Windows Desktop Runtime for WPF.
- Internet access for the initial NuGet package restore and live API features. No API keys are required by the current services.
- Write permission to the application output directory, where the SQLite database is created.

The application targets `net9.0-windows`; the core library and unit tests target `net9.0`. A successful build on another operating system does not demonstrate that the WPF application runs there.

## Open and run in Visual Studio

1. Clone or extract the repository to a writable local folder.
2. Open `TripMate.sln` in Visual Studio.
3. Allow NuGet packages to restore. If necessary, right-click the solution and select **Restore NuGet Packages**.
4. Right-click the **TripMate** project and select **Set as Startup Project**.
5. Select **Build > Build Solution**, then press **F5** to run, or **Ctrl+F5** to run without debugging.
6. Click **Add Sample Trip** to load example members, activities, and expenses, or create a new trip.

Each click on **Add Sample Trip** creates another sample trip. Select the trip to work with before opening its feature tabs.

To build and run from a terminal on Windows with the required SDK and runtimes installed:

```powershell
dotnet restore TripMate.sln
dotnet build TripMate.sln --no-restore
dotnet run --project TripMate/TripMate.csproj --no-build
```

## Data and persistence

TripMate creates `TripMate.db` next to the running application on first launch. For the default Debug build this is normally:

```text
TripMate/bin/Debug/net9.0-windows/TripMate.db
```

Trips, members, activities, expenses, shares, and repayments are stored locally. They remain available after restarting the same build. A different build or publish directory may use a different database. Keep the database when preserving trip data; database files are not part of the source repository.

The cost model reads `Data/trip_costs_sample.csv` relative to the running application. The project copies this file into the output directory during the build. Keep the `Data` folder with the application when moving its output. The sample trip is generated in code and is separate from the model training CSV.

## Network features and limitations

- Place lookup uses Open-Meteo geocoding and OpenStreetMap Nominatim. Weather forecasts use Open-Meteo. Nearby attractions use Wikipedia geosearch.
- Currency conversion uses Frankfurter exchange rates. The supported codes are listed in `CurrencyService.SupportedCurrencies`; some country currencies, such as VND, do not have live conversion support in this application.
- Rates already downloaded are cached in memory for the current session. This is not a persistent offline rate store. Same-currency expenses do not need an exchange rate.
- With no connection, local trip data, itinerary changes, existing spending charts, and settlement calculations remain available. Live searches and uncached currency conversion can fail; the application displays an error. Foreign-currency expenses must be entered in the trip's home currency when conversion is unavailable.
- Expense inputs must be positive amounts with at most two decimal places. Converted expense amounts and shares use whole cents, including for currencies commonly displayed without fractional units.
- Route optimisation uses geographic distances and travel-mode estimates, rather than live road routing or transport timetables. The exact settlement algorithm supports up to 16 members with nonzero balances; larger groups use a greedy plan that is not guaranteed to minimise transfers.
- The Insights model trains locally when first needed, using synthetic sample data. Estimates are rough guidance in AUD, including flights from Sydney, rather than quotes or bookings. Destinations must be in the training data. The form accepts 1–30 days and 1–20 travellers. Unsupported trip destinations are cleared instead of silently reusing a previous city.

## Run the existing automated tests

The solution includes NUnit tests in **TripMate.UnitTests**. These instructions describe the project's existing tests.

In Visual Studio, select **Test > Run All Tests**, then inspect **Test Explorer**. From the repository root, the equivalent command is:

```powershell
dotnet test TripMate.UnitTests/TripMate.UnitTests.csproj
```

The tests cover core behaviour including debt settlement, route optimisation, coordinate validity, local persistence, and cost prediction. They do not cover every WPF interaction or live external service. Test success alone does not establish that the full application has been verified in the required lab environment.

## Solution structure

- `TripMate`: WPF windows and feature tabs, split across partial `MainWindow` files.
- `TripMate.Core`: models, SQLite data access, service interfaces and implementations, route and settlement algorithms, and the cost predictor.
- `TripMate.UnitTests`: NUnit tests for the core project.

## Troubleshooting

- **Missing SDK or unsupported target framework:** install the .NET 9 SDK and use a compatible Visual Studio installation with the desktop workload.
- **A runtime is missing:** install the .NET 9 runtime required by the error; the WPF application needs the Windows Desktop Runtime.
- **Packages cannot restore:** check the internet connection and NuGet source availability, then restore again.
- **Database cannot be opened or written:** run from a writable folder and check access to the application output directory.
- **Cost model cannot train:** confirm that `Data/trip_costs_sample.csv` is present beside the built application under `Data`, then rebuild if it is missing.
- **Live lookup or conversion fails:** check connectivity and currency support. Local trip data remains in SQLite.
