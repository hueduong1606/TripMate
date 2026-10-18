using System.Windows;
using TripMate.Core.Services;

/*
 Currency converter on the Explore tab, using live European Central Bank rates (Frankfurter API).
*/

namespace TripMate
{
    public partial class MainWindow
    {
        // Called from the MainWindow constructor
        partial void SetupCurrencyConverter()
        {
            foreach (string currency in CurrencyService.SupportedCurrencies)
            {
                fromCurrencyComboBox.Items.Add(currency);
                toCurrencyComboBox.Items.Add(currency);
            }
            tripSelected += SetDefaultCurrencies;
        }

        // Default the converter to the trip's home currency -> local currency.
        // Falls back to AUD when a currency has no live rates (e.g. VND).
        private void SetDefaultCurrencies()
        {
            if (currentTrip == null) return;

            string from = CurrencyService.IsSupported(currentTrip.HomeCurrency) ? currentTrip.HomeCurrency.ToUpper() : "AUD";
            bool localSupported = CurrencyService.IsSupported(currentTrip.LocalCurrency);
            string to = localSupported ? currentTrip.LocalCurrency.ToUpper() : from;

            fromCurrencyComboBox.SelectedItem = from;
            toCurrencyComboBox.SelectedItem = to;
            convertResultText.Text = "";
            rateInfoText.Text = localSupported ? "" : "Live rates are not available for " + currentTrip.LocalCurrency + ".";
        }

        // Validate the amount and convert it with the currency service
        private async void convertButton_Click(object sender, RoutedEventArgs e)
        {
            if (!decimal.TryParse(convertAmountTextBox.Text, out decimal amount) || amount < 0)
            {
                MessageBox.Show("Please enter a valid amount.", "TripMate");
                return;
            }
            string from = fromCurrencyComboBox.Text;
            string to = toCurrencyComboBox.Text;

            try
            {
                convertButton.IsEnabled = false;
                decimal rate = await currencyService.GetRateAsync(from, to);
                // The currencies changed while waiting (e.g. another trip was chosen), so this result is out of date
                if (fromCurrencyComboBox.Text != from || toCurrencyComboBox.Text != to) return;
                convertResultText.Text = amount.ToString("0.00") + " " + from + " = " + (amount * rate).ToString("0.00") + " " + to;
                rateInfoText.Text = "1 " + from + " = " + rate.ToString("0.####") + " " + to +
                                    (currencyService.LastRateDate.HasValue ? "  (ECB rate from " + currencyService.LastRateDate.Value.ToString("dd MMM yyyy") + ")" : "");
            }
            catch (Exception ex)
            {
                convertResultText.Text = "";
                rateInfoText.Text = "Conversion failed: " + ex.Message;
            }
            finally
            {
                convertButton.IsEnabled = true;
            }
        }

        // Swap the "from" and "to" currencies
        private void swapCurrencyButton_Click(object sender, RoutedEventArgs e)
        {
            string from = fromCurrencyComboBox.Text;
            fromCurrencyComboBox.Text = toCurrencyComboBox.Text;
            toCurrencyComboBox.Text = from;
        }
    }
}
