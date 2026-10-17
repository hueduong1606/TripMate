namespace TripMate.Core.Services
{
    // Interface for exchange rates and currency conversion
    public interface ICurrencyService
    {
        // Date of the last rate downloaded (rates are published once a day)
        DateTime? LastRateDate { get; }

        // 1 unit of "from" = rate units of "to"
        Task<decimal> GetRateAsync(string from, string to);

        // Convert an amount, rounded to cents
        Task<decimal> ConvertAsync(decimal amount, string from, string to);
    }
}
