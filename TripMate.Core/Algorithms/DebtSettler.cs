using System.Numerics;
using TripMate.Core.Models;

/*
 Settling up the shared expenses with the fewest bank transfers.

 Each member's balance = what they paid - what they owe.
 Positive balance = the group owes them money, negative = they owe the group.

*/

namespace TripMate.Core.Algorithms
{
    // One bank transfer in the settle-up plan
    public class Transfer
    {
        public string From { get; set; } = "";
        public string To { get; set; } = "";
        public decimal Amount { get; set; }

        // Override the ToString() method to show "Ann pays Ben 12.50"
        public override string ToString()
        {
            return From + " pays " + To + " $" + Amount.ToString("0.00");
        }
    }

    public class DebtSettler
    {
        // Method to split an amount equally, in whole cents.
        // Any leftover cents go to the first people, so the shares always add up exactly.
        public static decimal[] SplitEqually(decimal amount, int people)
        {
            if (people <= 0) throw new ArgumentException("An expense must be split between at least one person.");

            long totalCents = (long)Math.Round(amount * 100, MidpointRounding.AwayFromZero);
            long baseCents = totalCents / people;
            long leftover = totalCents - baseCents * people;

            decimal[] shares = new decimal[people];
            for (int i = 0; i < people; i++)
            {
                long cents = baseCents + (i < leftover ? 1 : 0);
                shares[i] = cents / 100m;
            }
            return shares;
        }

        // Method to work out each member's balance from the trip's expenses
        public static Dictionary<string, decimal> CalculateBalances(IEnumerable<Member> members, IEnumerable<Expense> expenses)
        {
            Dictionary<string, decimal> balances = new Dictionary<string, decimal>();
            foreach (Member m in members)
            {
                balances[m.Name] = 0m;
            }

            foreach (Expense e in expenses)
            {
                // The payer gets back the full amount...
                if (e.PaidBy != null) balances[e.PaidBy.Name] += e.Amount;

                // ...and everyone who shared it owes their share
                foreach (ExpenseShare s in e.Shares)
                {
                    if (s.Member != null) balances[s.Member.Name] -= s.ShareAmount;
                }
            }
            return balances;
        }

        // Method to count how many transfers it would take if everyone paid back
        // each payer separately (the "naive" way), to compare with the optimised plan
        public static int CountNaiveTransfers(IEnumerable<Expense> expenses)
        {
            var pairs = from e in expenses
                        from s in e.Shares
                        where s.Member != null && e.PaidBy != null && s.Member.Name != e.PaidBy.Name
                        select new { From = s.Member!.Name, To = e.PaidBy!.Name };
            return pairs.Distinct().Count();
        }

        // Method to produce the settle-up plan (greedy rule for now)
        public List<Transfer> Settle(IDictionary<string, decimal> balances)
        {
            return GreedySettle(balances);
        }

        // Greedy settle-up on its own (used for comparison and for very large groups)
        public List<Transfer> GreedySettle(IDictionary<string, decimal> balances)
        {
            List<string> names = balances.Keys.ToList();
            List<long> cents = balances.Values.Select(v => (long)Math.Round(v * 100, MidpointRounding.AwayFromZero)).ToList();
            return GreedySettle(names, cents);
        }

        // Greedy rule: the person who owes the most pays the person who is owed the most,
        // as much as possible, until everyone is square
        private List<Transfer> GreedySettle(List<string> names, List<long> cents)
        {
            List<Transfer> transfers = new List<Transfer>();
            long[] remaining = cents.ToArray();

            while (true)
            {
                int creditor = 0, debtor = 0;
                for (int i = 1; i < remaining.Length; i++)
                {
                    if (remaining[i] > remaining[creditor]) creditor = i;
                    if (remaining[i] < remaining[debtor]) debtor = i;
                }
                if (remaining.Length == 0 || remaining[creditor] <= 0) break;

                long amount = Math.Min(remaining[creditor], -remaining[debtor]);
                transfers.Add(new Transfer { From = names[debtor], To = names[creditor], Amount = amount / 100m });
                remaining[creditor] -= amount;
                remaining[debtor] += amount;
            }
            return transfers;
        }
    }
}
