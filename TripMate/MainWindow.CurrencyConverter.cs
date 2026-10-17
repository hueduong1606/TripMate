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

        // Default the converter to the trip's home currency -> local currency
        private void SetDefaultCurrencies()
        {
            if (currentTrip == null) return;
            fromCurrencyComboBox.Text = currentTrip.HomeCurrency;
            toCurrencyComboBox.Text = currentTrip.LocalCurrency;
            convertResultText.Text = "";
            rateInfoText.Text = "";
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
