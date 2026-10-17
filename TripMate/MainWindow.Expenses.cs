using System.Windows;
using Microsoft.EntityFrameworkCore;
using TripMate.Core.Algorithms;
using TripMate.Core.Data;
using TripMate.Core.Models;
using TripMate.Core.Services;

/*
 Expenses tab: log who paid for what and who shares it, then "Settle Up"
 works out who pays whom with the fewest transfers (see DebtSettler).
*/

namespace TripMate
{
    public partial class MainWindow
    {
        private readonly DebtSettler debtSettler = new DebtSettler();
        private readonly ICurrencyService currencyService = new CurrencyService();

        // Expense categories, used by the ComboBox and the pie chart
        private static readonly string[] categories = { "Accommodation", "Food", "Transport", "Activities", "Shopping", "Other" };

        // Called from the MainWindow constructor
        partial void SetupExpensesTab()
        {
            foreach (string category in categories) categoryComboBox.Items.Add(category);
            categoryComboBox.SelectedIndex = 1;
            expenseDatePicker.SelectedDate = DateTime.Today;

            refreshTripViews += ShowExpenses;
            tripSelected += ClearSettlement;
        }

        // Clear the settle-up results from the previous trip
        private void ClearSettlement()
        {
            transfersListBox.ItemsSource = null;
            settleSummaryText.Text = "";
        }

        // Method to show the expenses of the current trip in the data grid
        private void ShowExpenses()
        {
            if (currentTrip == null) return;

            // Expense changes invalidate the previously calculated transfer plan.
            ClearSettlement();

            // Who can pay and who can share an expense
            List<Member> members = currentTrip.Members.OrderBy(x => x.Name).ToList();
            paidByComboBox.ItemsSource = members;
            splitListBox.ItemsSource = members;
            splitListBox.SelectAll();

            // Newest expenses first
            expensesDataGrid.ItemsSource = currentTrip.Expenses.OrderByDescending(x => x.Date).ThenByDescending(x => x.ExpenseId).ToList();
            // Settle-up payments are listed too, but they are not spending
            List<Expense> spending = currentTrip.SpendingExpenses;
            expenseTotalText.Text = "Total spent: " + Money(spending.Sum(x => x.Amount)) + " across " + spending.Count + " expenses";

            // Currencies the expense can be entered in
            List<string> currencies = new List<string> { currentTrip.HomeCurrency };
            if (currentTrip.LocalCurrency != currentTrip.HomeCurrency) currencies.Add(currentTrip.LocalCurrency);
            currencies.AddRange(CurrencyService.SupportedCurrencies.Where(c => !currencies.Contains(c)));
            expenseCurrencyComboBox.ItemsSource = currencies;
            expenseCurrencyComboBox.SelectedIndex = currentTrip.LocalCurrency != currentTrip.HomeCurrency ? 1 : 0;

            ShowBalances();
        }

        // Method to show what each person is owed (+) or owes (-)
        private void ShowBalances()
        {
            if (currentTrip == null) return;

            Dictionary<string, decimal> balances = DebtSettler.CalculateBalances(currentTrip.Members, currentTrip.Expenses);
            balancesListBox.ItemsSource = balances
                .OrderByDescending(b => b.Value)
                .Select(b => b.Key + ": " + (b.Value > 0 ? "is owed " + Money(b.Value) : b.Value < 0 ? "owes " + Money(-b.Value) : "square"))
                .ToList();
        }

        // Tick every traveller in the "split between" list
        private void selectAllButton_Click(object sender, RoutedEventArgs e)
        {
            splitListBox.SelectAll();
        }

        // Validate the form, convert the amount if needed and save the expense
        private async void addExpenseButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentTrip == null) return;

            // Validate the input
            string description = expenseDescriptionTextBox.Text.Trim();
            if (description == "")
            {
                MessageBox.Show("Please enter a description.", "TripMate");
                return;
            }
            if (!decimal.TryParse(expenseAmountTextBox.Text, out decimal originalAmount) || originalAmount <= 0)
            {
                MessageBox.Show("Please enter an amount greater than 0.", "TripMate");
                return;
            }
            Member? payer = paidByComboBox.SelectedItem as Member;
            if (payer == null)
            {
                MessageBox.Show("Please choose who paid.", "TripMate");
                return;
            }
            List<Member> sharedBy = splitListBox.SelectedItems.Cast<Member>().OrderBy(m => m.Name).ToList();
            if (sharedBy.Count == 0)
            {
                MessageBox.Show("Please choose who the expense is split between.", "TripMate");
                return;
            }

            string currency = expenseCurrencyComboBox.SelectedItem as string ?? currentTrip.HomeCurrency;
            decimal amount = originalAmount;

            // Remember which trip this expense belongs to. The user could switch
            // or delete the trip while we wait for the exchange rate.
            int tripId = currentTrip.TripId;
            string homeCurrency = currentTrip.HomeCurrency;

            try
            {
                addExpenseButton.IsEnabled = false;

                // Convert to the currency the group settles up in
                if (currency != homeCurrency)
                {
                    try
                    {
                        SetStatus("Converting " + currency + " to " + homeCurrency + "...");
                        amount = await currencyService.ConvertAsync(originalAmount, currency, homeCurrency);
                    }
                    catch (Exception ex)
                    {
                        // Handle exceptions, e.g. no internet or a currency the API does not cover
                        MessageBox.Show("Could not convert the currency: " + ex.Message + "\nPlease enter the amount in " + homeCurrency + ".", "TripMate");
                        SetStatus("Expense not saved");
                        return;
                    }
                }

                // A different trip was selected (or the trip was deleted) while converting:
                // don't save this expense to the wrong trip
                if (currentTrip == null || currentTrip.TripId != tripId)
                {
                    MessageBox.Show("The selected trip changed before the expense was saved. Please add it again.", "TripMate");
                    SetStatus("Expense not saved");
                    return;
                }

                // Split equally, in whole cents
                decimal[] shares = DebtSettler.SplitEqually(amount, sharedBy.Count);

                Expense expense = new Expense
                {
                    Description = description,
                    Category = categoryComboBox.SelectedItem as string ?? "Other",
                    Date = expenseDatePicker.SelectedDate ?? DateTime.Today,
                    Amount = amount,
                    OriginalAmount = originalAmount,
                    OriginalCurrency = currency,
                    PaidById = payer.MemberId,
                    TripId = tripId
                };
                for (int i = 0; i < sharedBy.Count; i++)
                {
                    expense.Shares.Add(new ExpenseShare { MemberId = sharedBy[i].MemberId, ShareAmount = shares[i] });
                }

                using (var context = new Context())
                {
                    context.Expenses.Add(expense);
                    context.SaveChanges();
                }

                expenseDescriptionTextBox.Clear();
                expenseAmountTextBox.Clear();
                ReloadCurrentTrip();
                SetStatus(description + " added: " + Money(amount));
            }
            catch (Exception ex)
            {
                // e.g. the database file is locked
                MessageBox.Show("Could not save the expense: " + ex.Message, "TripMate");
                SetStatus("Expense not saved");
            }
            finally
            {
                // Always turn the button back on, whatever happened above
                addExpenseButton.IsEnabled = true;
            }
        }

        // Delete the selected expense and its shares
        private void deleteExpenseButton_Click(object sender, RoutedEventArgs e)
        {
            Expense? selected = expensesDataGrid.SelectedItem as Expense;
            if (selected == null) return;

            // Ask before deleting, because this cannot be undone
            MessageBoxResult answer = MessageBox.Show("Delete the expense \"" + selected.Description + "\"?",
                "TripMate", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes) return;

            using (var context = new Context())
            {
                Expense? toRemove = context.Expenses.Include(x => x.Shares).FirstOrDefault(x => x.ExpenseId == selected.ExpenseId);
                if (toRemove != null)
                {
                    context.Expenses.Remove(toRemove);
                    context.SaveChanges();
                }
            }
            transfersListBox.ItemsSource = null;
            settleSummaryText.Text = "";
            ReloadCurrentTrip();
            SetStatus(selected.Description + " deleted");
        }

        // Method to work out who pays whom, with the fewest transfers
        private void settleButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentTrip == null) return;
            if (currentTrip.Expenses.Count == 0)
            {
                settleSummaryText.Text = "No expenses yet, so nobody owes anything.";
                transfersListBox.ItemsSource = null;
                return;
            }

            Dictionary<string, decimal> balances = DebtSettler.CalculateBalances(currentTrip.Members, currentTrip.Expenses);
            List<Transfer> transfers = debtSettler.Settle(balances);
            transfersListBox.ItemsSource = transfers.Select(t => t.From + " -> " + t.To + ": " + Money(t.Amount)).ToList();

            if (transfers.Count == 0)
            {
                settleSummaryText.Text = "Everyone is already square!";
                return;
            }

            // Compare with paying everyone back expense by expense
            int naive = DebtSettler.CountNaiveTransfers(currentTrip.Expenses);
            settleSummaryText.Text = transfers.Count + " transfer" + (transfers.Count == 1 ? "" : "s") + " settle everyone up" +
                                     (naive > transfers.Count ? " (paying each person back separately would take " + naive + ")." : ".");
            SetStatus("Settle-up plan ready");
        }
    }
}
