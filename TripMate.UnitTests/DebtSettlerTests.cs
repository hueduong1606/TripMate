using TripMate.Core.Algorithms;
using TripMate.Core.Models;

namespace TripMate.UnitTests
{
    [TestFixture]
    public class DebtSettlerTests
    {
        private DebtSettler _settler = null!;

        // Runs before every test
        [SetUp]
        public void Setup()
        {
            // Arrange: a new settler for every test
            _settler = new DebtSettler();
        }

        // Helper: apply the transfers to the balances and check everyone ends up square
        private static void AssertEveryoneSquare(Dictionary<string, decimal> balances, List<Transfer> transfers)
        {
            Dictionary<string, decimal> after = new Dictionary<string, decimal>(balances);
            foreach (Transfer t in transfers)
            {
                Assert.That(t.Amount, Is.GreaterThan(0m), "Transfers should always be positive.");
                after[t.From] += t.Amount;
                after[t.To] -= t.Amount;
            }
            foreach (KeyValuePair<string, decimal> b in after)
            {
                Assert.That(b.Value, Is.EqualTo(0m), b.Key + " is not square after settling up.");
            }
        }

        // Splitting equally works to the cent, and the shares add up exactly
        [TestCase(100, 3, "33.34,33.33,33.33")]
        [TestCase(10, 4, "2.50,2.50,2.50,2.50")]
        [TestCase(0.05, 2, "0.03,0.02")]
        [Test]
        public void Test_SplitEqually(decimal amount, int people, string expected)
        {
            // Act
            decimal[] shares = DebtSettler.SplitEqually(amount, people);

            // Assert: shares match and add up exactly to the amount
            Assert.That(string.Join(",", shares.Select(s => s.ToString("0.00"))), Is.EqualTo(expected));
            Assert.That(shares.Sum(), Is.EqualTo(amount));
        }

        // Balances are worked out from who paid and who shared each expense
        [Test]
        public void Test_CalculateBalances()
        {
            // Arrange: Ann pays $90 dinner for 3, Ben pays $30 taxi for Ann and Ben
            Member ann = new Member { MemberId = 1, Name = "Ann" };
            Member ben = new Member { MemberId = 2, Name = "Ben" };
            Member cat = new Member { MemberId = 3, Name = "Cat" };

            Expense dinner = new Expense { Amount = 90, PaidBy = ann, PaidById = 1 };
            dinner.Shares.Add(new ExpenseShare { Member = ann, MemberId = 1, ShareAmount = 30 });
            dinner.Shares.Add(new ExpenseShare { Member = ben, MemberId = 2, ShareAmount = 30 });
            dinner.Shares.Add(new ExpenseShare { Member = cat, MemberId = 3, ShareAmount = 30 });

            Expense taxi = new Expense { Amount = 30, PaidBy = ben, PaidById = 2 };
            taxi.Shares.Add(new ExpenseShare { Member = ann, MemberId = 1, ShareAmount = 15 });
            taxi.Shares.Add(new ExpenseShare { Member = ben, MemberId = 2, ShareAmount = 15 });

            // Act
            Dictionary<string, decimal> balances = DebtSettler.CalculateBalances(new[] { ann, ben, cat }, new[] { dinner, taxi });

            // Assert
            Assert.That(balances["Ann"], Is.EqualTo(45m));
            Assert.That(balances["Ben"], Is.EqualTo(-15m));
            Assert.That(balances["Cat"], Is.EqualTo(-30m));
            Assert.That(DebtSettler.CountNaiveTransfers(new[] { dinner, taxi }), Is.EqualTo(3));
        }

        // Recording the plan as paid makes everyone square, and payments are not counted as spending
        [Test]
        public void Test_SettlementPayments_MakeEveryoneSquare()
        {
            // Arrange: Ann paid $90 for three people
            Member ann = new Member { MemberId = 1, Name = "Ann" };
            Member ben = new Member { MemberId = 2, Name = "Ben" };
            Member cat = new Member { MemberId = 3, Name = "Cat" };
            Trip trip = new Trip();
            Expense dinner = new Expense { Amount = 90, PaidBy = ann, PaidById = 1, Category = "Food" };
            foreach (Member m in new[] { ann, ben, cat })
            {
                dinner.Shares.Add(new ExpenseShare { Member = m, MemberId = m.MemberId, ShareAmount = 30 });
            }
            trip.Expenses.Add(dinner);
            Member[] members = { ann, ben, cat };

            // Act: record every transfer in the plan as paid
            List<Transfer> plan = _settler.Settle(DebtSettler.CalculateBalances(members, trip.Expenses));
            foreach (Transfer t in plan)
            {
                Member from = members.First(m => m.Name == t.From);
                Member to = members.First(m => m.Name == t.To);
                trip.Expenses.Add(DebtSettler.CreateSettlementPayment(0, from, to, t.Amount, "AUD"));
            }

            // Assert: balances are all zero, and payments are not counted as spending
            Assert.That(DebtSettler.CalculateBalances(members, trip.Expenses).Values, Is.All.EqualTo(0m));
            Assert.That(trip.SpendingExpenses.Sum(e => e.Amount), Is.EqualTo(90m));
            Assert.That(DebtSettler.CountNaiveTransfers(trip.Expenses), Is.EqualTo(2));
        }

        // One person is owed by two others: two transfers
        [Test]
        public void Test_Settle_SimpleCase()
        {
            // Arrange
            Dictionary<string, decimal> balances = new Dictionary<string, decimal>
            {
                { "Ann", 45m }, { "Ben", -15m }, { "Cat", -30m }
            };

            // Act
            List<Transfer> transfers = _settler.Settle(balances);

            // Assert
            Assert.That(transfers.Count, Is.EqualTo(2), "Ben and Cat should each pay Ann once.");
            AssertEveryoneSquare(balances, transfers);
        }

        // The exact method finds 4 transfers where the greedy rule needs 5
        [Test]
        public void Test_Settle_BeatsGreedy()
        {
            // Arrange: the greedy rule needs 5 transfers here, but Ann/Ben can settle between themselves
            // and Fay can pay Cat, Dan and Eve directly: 4 transfers
            Dictionary<string, decimal> balances = new Dictionary<string, decimal>
            {
                { "Ann", -11m }, { "Ben", 11m }, { "Cat", 8m }, { "Dan", 8m }, { "Eve", 2m }, { "Fay", -18m }
            };

            // Act
            List<Transfer> exact = _settler.Settle(balances);
            List<Transfer> greedy = _settler.GreedySettle(balances);

            // Assert
            Assert.That(exact.Count, Is.EqualTo(4), "Fewest transfers should be 4.");
            Assert.That(greedy.Count, Is.EqualTo(5), "Greedy on its own uses 5 transfers.");
            AssertEveryoneSquare(balances, exact);
            AssertEveryoneSquare(balances, greedy);
        }

        // Nobody owes anything: no transfers
        [Test]
        public void Test_Settle_EveryoneSquare()
        {
            // Arrange
            Dictionary<string, decimal> balances = new Dictionary<string, decimal> { { "Ann", 0m }, { "Ben", 0m } };

            // Act and Assert
            Assert.That(_settler.Settle(balances), Is.Empty, "Nobody should need to pay.");
        }

        // Balances that don't add up to zero are rejected
        [Test]
        public void Test_Settle_BalancesMustAddToZero()
        {
            // Arrange
            Dictionary<string, decimal> balances = new Dictionary<string, decimal> { { "Ann", 10m }, { "Ben", -5m } };

            // Act and Assert
            Assert.Throws<ArgumentException>(() => _settler.Settle(balances));
        }

        // Random groups of 5 to 25 people always settle, with at most n - 1 transfers
        [TestCase(5)]
        [TestCase(10)]
        [TestCase(16)]
        [TestCase(25)]
        [Test]
        public void Test_Settle_RandomGroups(int people)
        {
            // Arrange: random balances that add up to zero (fixed seed, so the test is repeatable)
            Random random = new Random(people);
            Dictionary<string, decimal> balances = new Dictionary<string, decimal>();
            decimal total = 0m;
            for (int i = 0; i < people - 1; i++)
            {
                decimal value = random.Next(-20000, 20000) / 100m;
                balances["P" + i] = value;
                total += value;
            }
            balances["P" + (people - 1)] = -total;

            // Act
            List<Transfer> transfers = _settler.Settle(balances);

            // Assert
            Assert.That(transfers.Count, Is.LessThanOrEqualTo(people - 1), "Never more than n - 1 transfers.");
            Assert.That(transfers.Count, Is.LessThanOrEqualTo(_settler.GreedySettle(balances).Count), "Never worse than the greedy rule.");
            AssertEveryoneSquare(balances, transfers);
        }
    }
}
