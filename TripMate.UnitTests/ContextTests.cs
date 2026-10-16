using Microsoft.EntityFrameworkCore;
using TripMate.Core.Algorithms;
using TripMate.Core.Data;
using TripMate.Core.Models;

namespace TripMate.UnitTests
{
    [TestFixture]
    public class ContextTests
    {
        // A trip with members, activities and expenses is saved to SQLite and read back
        [Test]
        public void Test_Context_SaveAndLoadTrip()
        {
            // Arrange: a temporary database file
            string dbPath = Path.Combine(Path.GetTempPath(), "TripMateTest_" + Guid.NewGuid() + ".db");
            try
            {
                using (var context = new Context(dbPath))
                {
                    context.Database.EnsureCreated();

                    Trip trip = new Trip { Name = "Uni break", Destination = "Tokyo" };
                    Member ann = new Member { Name = "Ann" };
                    Member ben = new Member { Name = "Ben" };
                    trip.Members.Add(ann);
                    trip.Members.Add(ben);
                    trip.Activities.Add(new Activity { Name = "Senso-ji", DayNumber = 1, Order = 1 });

                    Expense ramen = new Expense { Description = "Ramen", Amount = 40m, OriginalAmount = 40m, PaidBy = ann, Trip = trip };
                    ramen.Shares.Add(new ExpenseShare { Member = ann, ShareAmount = 20m });
                    ramen.Shares.Add(new ExpenseShare { Member = ben, ShareAmount = 20m });
                    trip.Expenses.Add(ramen);

                    context.Trips.Add(trip);
                    context.SaveChanges();
                }

                // Act: read it back with a new context
                using (var context = new Context(dbPath))
                {
                    Trip loaded = context.Trips
                        .Include(t => t.Members)
                        .Include(t => t.Activities)
                        .Include(t => t.Expenses).ThenInclude(e => e.Shares).ThenInclude(s => s.Member)
                        .Include(t => t.Expenses).ThenInclude(e => e.PaidBy)
                        .Single();

                    // Assert
                    Assert.That(loaded.Members.Count, Is.EqualTo(2), "Both members should be saved.");
                    Assert.That(loaded.Activities.Single().Name, Is.EqualTo("Senso-ji"), "The activity should be saved.");
                    Assert.That(loaded.Expenses.Single().PaidByName, Is.EqualTo("Ann"), "The payer should be loaded with the expense.");
                    Assert.That(loaded.Expenses.Single().SplitBetween, Is.EqualTo("Ann, Ben"), "The shares should be loaded with the expense.");

                    // Deleting the trip removes everything that belongs to it
                    context.Trips.Remove(loaded);
                    context.SaveChanges();
                    Assert.That(context.Expenses.Count(), Is.EqualTo(0), "Deleting a trip should delete its expenses.");
                    Assert.That(context.ExpenseShares.Count(), Is.EqualTo(0), "Deleting a trip should delete the expense shares.");
                    Assert.That(context.Members.Count(), Is.EqualTo(0), "Deleting a trip should delete its members.");
                }
            }
            finally
            {
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                File.Delete(dbPath);
            }
        }

        // The sample trip's numbers add up
        [Test]
        public void Test_SampleTrip_IsConsistent()
        {
            // Arrange
            Trip sample = DataSeeder.CreateSampleTrip();

            // Assert: every expense's shares add up to the expense amount
            foreach (Expense e in sample.Expenses)
            {
                Assert.That(e.Shares.Sum(s => s.ShareAmount), Is.EqualTo(e.Amount), e.Description);
            }

            // Act
            Dictionary<string, decimal> balances = DebtSettler.CalculateBalances(sample.Members, sample.Expenses);
            int transfers = new DebtSettler().Settle(balances).Count;

            // Assert: balances add up to zero and settle in fewer transfers than paying back one by one
            Assert.That(balances.Values.Sum(), Is.EqualTo(0m), "Balances should add up to zero.");
            Assert.That(transfers, Is.LessThan(DebtSettler.CountNaiveTransfers(sample.Expenses)), "The plan should need fewer transfers than paying back one by one.");
        }
    }
}