using System.Numerics;
using TripMate.Core.Models;

/*
 Settling up the shared expenses with the fewest bank transfers.

 Each member's balance = what they paid - what they owe.
 Positive balance = the group owes them money, negative = they owe the group.

 Any group of k people whose balances add up to zero can be settled with k - 1
 transfers. So the fewest transfers = (people with a balance) - (the largest number
 of separate zero-sum groups we can split them into). We find those groups with
 dynamic programming over every subset of people, then settle each group with the
 greedy "largest debtor pays largest creditor" rule.
 For very large groups (more than 16 people owing money) we fall back to the
 greedy rule on its own, which uses at most n - 1 transfers.
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
        // Largest number of people the exact method handles (2^16 subsets)
        public const int MaxExactPeople = 16;

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

        // Method to produce the settle-up plan with the fewest transfers
        public List<Transfer> Settle(IDictionary<string, decimal> balances)
        {
            // Work in whole cents to avoid rounding problems, and ignore anyone already square
            List<string> names = new List<string>();
            List<long> cents = new List<long>();
            foreach (KeyValuePair<string, decimal> b in balances)
            {
                long c = (long)Math.Round(b.Value * 100, MidpointRounding.AwayFromZero);
                if (c != 0)
                {
                    names.Add(b.Key);
                    cents.Add(c);
                }
            }

            if (cents.Sum() != 0)
            {
                throw new ArgumentException("Balances must add up to zero.");
            }

            // Split people into independent zero-sum groups
            List<List<int>> groups;
            if (cents.Count <= MaxExactPeople)
            {
                groups = FindZeroSumGroups(cents.ToArray());
            }
            else
            {
                groups = new List<List<int>> { Enumerable.Range(0, cents.Count).ToList() };
            }

            // Settle each group on its own
            List<Transfer> transfers = new List<Transfer>();
            foreach (List<int> group in groups)
            {
                transfers.AddRange(GreedySettle(group.Select(i => names[i]).ToList(), group.Select(i => cents[i]).ToList()));
            }
            return transfers;
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

        // Dynamic programming over subsets (bitmasks).
        // best[mask] = the largest number of zero-sum groups the people in "mask" can be split into,
        // when people are removed one at a time.
        private static List<List<int>> FindZeroSumGroups(long[] amounts)
        {
            int n = amounts.Length;
            List<List<int>> groups = new List<List<int>>();
            if (n == 0) return groups;

            int full = (1 << n) - 1;
            long[] sum = new long[full + 1];
            int[] best = new int[full + 1];

            for (int mask = 1; mask <= full; mask++)
            {
                // Sum of the subset = sum without its lowest person + that person
                int lowest = BitOperations.TrailingZeroCount(mask);
                sum[mask] = sum[mask & (mask - 1)] + amounts[lowest];

                int bestWithoutOne = 0;
                for (int i = 0; i < n; i++)
                {
                    if ((mask & (1 << i)) != 0)
                    {
                        bestWithoutOne = Math.Max(bestWithoutOne, best[mask ^ (1 << i)]);
                    }
                }
                best[mask] = bestWithoutOne + (sum[mask] == 0 ? 1 : 0);
            }

            // Walk back from the full set, removing one person at a time along an optimal path.
            // Every time the remaining set adds up to zero, the people removed since the
            // previous zero-sum set form one independent group.
            int current = full;
            int groupStart = full;
            while (current != 0)
            {
                int bonus = sum[current] == 0 ? 1 : 0;
                for (int i = 0; i < n; i++)
                {
                    int smaller = current ^ (1 << i);
                    if ((current & (1 << i)) != 0 && best[smaller] + bonus == best[current])
                    {
                        current = smaller;
                        break;
                    }
                }

                if (sum[current] == 0)
                {
                    groups.Add(MaskToList(groupStart ^ current, n));
                    groupStart = current;
                }
            }
            return groups;
        }

        // Method to turn a bitmask into the list of people it contains
        private static List<int> MaskToList(int mask, int n)
        {
            List<int> people = new List<int>();
            for (int i = 0; i < n; i++)
            {
                if ((mask & (1 << i)) != 0) people.Add(i);
            }
            return people;
        }
    }
}
