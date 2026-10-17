using System.Windows;
using TripMate.Core.Algorithms;

namespace TripMate.Windows
{
    // One row of the settle-up list: a transfer plus a "paid" tick box
    public class TransferRow
    {
        public TransferRow(Transfer transfer, string currency)
        {
            Transfer = transfer;
            AmountText = transfer.Amount.ToString("0.00") + " " + currency;
        }

        public Transfer Transfer { get; private set; }
        public string AmountText { get; private set; }
        public bool IsSelected { get; set; }
    }

    // Dialog window that shows the settle-up plan and lets the group record payments.
    // MainWindow reads PaidTransfers and saves them to the database.
    public partial class SettleUpWindow : Window
    {
        private readonly List<TransferRow> rows;
        private readonly string currency;

        public SettleUpWindow(List<Transfer> transfers, int naiveTransfers, string currency)
        {
            InitializeComponent();
            this.currency = currency;
            PaidTransfers = new List<Transfer>();

            // Build one row per transfer (LINQ Select with a lambda expression)
            rows = transfers.Select(t => new TransferRow(t, currency)).ToList();
            transfersListView.ItemsSource = rows;

            decimal total = transfers.Sum(t => t.Amount);
            summaryText.Text = transfers.Count + " transfer" + (transfers.Count == 1 ? "" : "s") + " (" + total.ToString("0.00") + " " + currency +
                               " in total) settle everyone up." +
                               (naiveTransfers > transfers.Count ? " Paying each person back separately would take " + naiveTransfers + "." : "");
        }

        // The transfers the user ticked as paid
        public List<Transfer> PaidTransfers { get; private set; }

        // Copy the plan as text, e.g. to paste into the group chat
        private void copyButton_Click(object sender, RoutedEventArgs e)
        {
            string text = "TripMate settle-up plan:\n" + string.Join("\n", rows.Select(r => r.Transfer.From + " pays " + r.Transfer.To + " " + r.AmountText));
            try
            {
                Clipboard.SetText(text);
                MessageBox.Show("Plan copied. Paste it into your group chat.", "TripMate");
            }
            catch (Exception ex)
            {
                // The clipboard can be locked by another app
                MessageBox.Show("Could not copy the plan: " + ex.Message, "TripMate");
            }
        }

        // Keep the ticked transfers and close the window
        private void recordButton_Click(object sender, RoutedEventArgs e)
        {
            PaidTransfers = rows.Where(r => r.IsSelected).Select(r => r.Transfer).ToList();
            if (PaidTransfers.Count == 0)
            {
                MessageBox.Show("Tick at least one transfer that has been paid.", "TripMate");
                return;
            }
            DialogResult = true;
        }
    }
}
