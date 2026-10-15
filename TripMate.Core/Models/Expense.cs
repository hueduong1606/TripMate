using System.ComponentModel.DataAnnotations.Schema;

namespace TripMate.Core.Models
{
    // Something one member paid for on behalf of some (or all) of the group
    public class Expense
    {
        public Expense()
        {
            Shares = new List<ExpenseShare>();
            Date = DateTime.Today;
        }

        public int ExpenseId { get; set; }
        public string Description { get; set; } = "";
        public string Category { get; set; } = "Other";
        public DateTime Date { get; set; }

        // Amount in the trip's home currency (what the group settles in)
        public decimal Amount { get; set; }

        // What was actually paid, before currency conversion
        public decimal OriginalAmount { get; set; }
        public string OriginalCurrency { get; set; } = "AUD";

        // Who paid
        public int PaidById { get; set; }
        public Member? PaidBy { get; set; }

        public int TripId { get; set; }

        // Who the expense is split between, and how much each person owes
        public IList<ExpenseShare> Shares { get; set; }

        // Read-only properties used by the DataGrid
        [NotMapped]
        public string PaidByName
        {
            get { return PaidBy == null ? "" : PaidBy.Name; }
        }

        [NotMapped]
        public string SplitBetween
        {
            get { return string.Join(", ", Shares.Where(s => s.Member != null).Select(s => s.Member!.Name)); }
        }

        [NotMapped]
        public string OriginalText
        {
            get { return OriginalAmount.ToString("0.00") + " " + OriginalCurrency; }
        }
    }

    // One member's share of an expense
    public class ExpenseShare
    {
        public int ExpenseShareId { get; set; }
        public decimal ShareAmount { get; set; }

        public int ExpenseId { get; set; }
        public Expense? Expense { get; set; }

        public int MemberId { get; set; }
        public Member? Member { get; set; }
    }
}