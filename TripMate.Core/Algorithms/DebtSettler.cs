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
    }
}
