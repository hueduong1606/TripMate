namespace TripMate.Core.Models
{
    // The kinds of spending an expense can belong to.
    // Settle-up payments use Trip.SettlementCategory instead, so they are not listed here.
    public enum ExpenseCategory
    {
        Accommodation,
        Food,
        Transport,
        Activities,
        Shopping,
        Other
    }
}
