using System.Windows;
using TripMate.Core.Algorithms;
using TripMate.Core.Data;
using TripMate.Core.Models;
using TripMate.Windows;

/*
 Recording settle-up payments: opens SettleUpWindow with the fewest-transfers plan,
 then saves each transfer the group has paid as a Settlement payment.
*/

namespace TripMate
{
    public partial class MainWindow
    {
        // Open the Settle Up window and save the payments the user ticks
        private void recordPaymentsButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentTrip == null) return;

            // Work out the plan
            Dictionary<string, decimal> balances = DebtSettler.CalculateBalances(currentTrip.Members, currentTrip.Expenses);
            List<Transfer> transfers = debtSettler.Settle(balances);
            if (transfers.Count == 0)
            {
                MessageBox.Show("Everyone is already square!", "TripMate");
                return;
            }

            SettleUpWindow window = new SettleUpWindow(transfers, DebtSettler.CountNaiveTransfers(currentTrip.Expenses), currentTrip.HomeCurrency);
            window.Owner = this;
            if (window.ShowDialog() != true) return;

            // Save each paid transfer as a settlement payment
            using (var context = new Context())
            {
                foreach (Transfer t in window.PaidTransfers)
                {
                    Member from = currentTrip.Members.First(m => m.Name == t.From);
                    Member to = currentTrip.Members.First(m => m.Name == t.To);
                    Expense payment = DebtSettler.CreateSettlementPayment(currentTrip.TripId, from, to, t.Amount, currentTrip.HomeCurrency);

                    // Only the ids are needed; don't let EF try to insert the members again
                    payment.PaidBy = null;
                    foreach (ExpenseShare share in payment.Shares) share.Member = null;

                    context.Expenses.Add(payment);
                }
                context.SaveChanges();
            }

            ClearSettlement();
            ReloadCurrentTrip();
            SetStatus(window.PaidTransfers.Count + " payment(s) recorded");
        }
    }
}
