using System.Text.Json;

namespace TripMate.Core.Services
{
    public class CurrencyService : ICurrencyService
    {
        // Currencies published by the European Central Bank, which the Frankfurter API uses
        public static readonly string[] SupportedCurrencies =
        {
            "AUD", "BGN", "BRL", "CAD", "CHF", "CNY", "CZK", "DKK", "EUR", "GBP", "HKD",
            "HUF", "IDR", "ILS", "INR", "ISK", "JPY", "KRW", "MXN", "MYR", "NOK", "NZD",
            "PHP", "PLN", "RON", "SEK", "SGD", "THB", "TRY", "USD", "ZAR"
        };

        // Country code (from the geocoding search) -> local currency
        private static readonly Dictionary<string, string> countryCurrencies = new Dictionary<string, string>
        {
            { "AU", "AUD" }, { "NZ", "NZD" }, { "JP", "JPY" }, { "KR", "KRW" }, { "CN", "CNY" },
            { "HK", "HKD" }, { "SG", "SGD" }, { "MY", "MYR" }, { "TH", "THB" }, { "ID", "IDR" },
            { "PH", "PHP" }, { "IN", "INR" }, { "VN", "VND" }, { "FJ", "FJD" }, { "US", "USD" },
            { "CA", "CAD" }, { "MX", "MXN" }, { "BR", "BRL" }, { "GB", "GBP" }, { "CH", "CHF" },
            { "NO", "NOK" }, { "SE", "SEK" }, { "DK", "DKK" }, { "IS", "ISK" }, { "PL", "PLN" },
            { "CZ", "CZK" }, { "HU", "HUF" }, { "TR", "TRY" }, { "ZA", "ZAR" }, { "IL", "ILS" },
            { "FR", "EUR" }, { "DE", "EUR" }, { "IT", "EUR" }, { "ES", "EUR" }, { "PT", "EUR" },
            { "NL", "EUR" }, { "GR", "EUR" }, { "AT", "EUR" }, { "IE", "EUR" }, { "BE", "EUR" }
        };

        // Rates already downloaded this session, so we don't ask the API twice
        private readonly Dictionary<string, decimal> rateCache = new Dictionary<string, decimal>();

        public DateTime? LastRateDate { get; private set; }

        // Method to get the currency used in a country (AUD if unknown)
        public static string CurrencyForCountry(string countryCode)
        {
            return countryCurrencies.TryGetValue(countryCode.ToUpper(), out string? currency) ? currency : "AUD";
        }

        // Method to check that a currency code is one the API supports
        public static bool IsSupported(string currency)
        {
            return SupportedCurrencies.Contains(currency.ToUpper());
        }

        // Method to get the exchange rate: 1 unit of "from" = rate units of "to"
        public async Task<decimal> GetRateAsync(string from, string to)
        {
            from = from.ToUpper();
            to = to.ToUpper();
            if (from == to) return 1m;

            if (!IsSupported(from) || !IsSupported(to))
            {
                throw new NotSupportedException("Live rates are not available for " + (IsSupported(from) ? to : from) + ". Please enter the amount in a supported currency.");
            }

            string key = from + to;
            if (rateCache.ContainsKey(key)) return rateCache[key];

            // e.g. https://api.frankfurter.dev/v1/latest?base=AUD&symbols=JPY
            string url = "https://api.frankfurter.dev/v1/latest?base=" + from + "&symbols=" + to;
            using (JsonDocument doc = await ApiClient.GetJsonAsync(url))
            {
                decimal rate = doc.RootElement.GetProperty("rates").GetProperty(to).GetDecimal();
                LastRateDate = DateTime.Parse(doc.RootElement.GetProperty("date").GetString()!);
                rateCache[key] = rate;
                return rate;
            }
        }

        // Method to convert an amount between two currencies, rounded to cents
        public async Task<decimal> ConvertAsync(decimal amount, string from, string to)
        {
            decimal rate = await GetRateAsync(from, to);
            return Math.Round(amount * rate, 2);
        }
    }
}
