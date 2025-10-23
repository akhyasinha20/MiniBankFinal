
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using MiniBank.Models;
using System.Data.Entity;

namespace MiniBank.Controllers
{
    public class CustomerController : Controller
    {
        private MiniBankDBEntities4 db = new MiniBankDBEntities4();

        // helper DTO used for dashboard
        private class LoanSummary
        {
            public int LoanAccountId { get; set; }
            public decimal LoanAmount { get; set; }
            public decimal? EMI { get; set; }
            public decimal Outstanding { get; set; }
            public DateTime StartDate { get; set; }
            public int TenureMonths { get; set; }
            public int PaymentsMade { get; set; }
            public decimal TotalPaid { get; set; }
            public bool EMIPaidThisMonth { get; set; }
            public DateTime NextEMIDate { get; set; }
        }

        // GET: Customer Dashboard
        public ActionResult Dashboard()
        {
            var uname = Session["Username"];
            // Get customer details
            var customer = db.Customers.FirstOrDefault(c => c.CustName == (string)uname);
            if (customer == null) return RedirectToAction("Login", "Auth");
            var customerId = customer.CustomerId;

            // Fetch all accounts for this customer
            var accounts = db.Accounts.Where(a => a.CustomerId == customerId).ToList();
            var accountIds = accounts.Select(a => a.AccountId).ToList();

            // Total savings balance
            decimal totalBalance = 0m;
            if (accountIds.Any())
            {
                totalBalance = db.SavingsAccounts
                    .Where(s => accountIds.Contains(s.AccountId))
                    .Select(s => (decimal?)s.Balance)
                    .ToList()
                    .Where(v => v.HasValue)
                    .Sum(v => v.Value);
            }

            // Loan accounts and summaries
            var loanAccounts = db.LoanAccounts
                .Where(l => accountIds.Contains(l.AccountId))
                .ToList();

            var loanSummaries = new List<LoanSummary>();
            foreach (var l in loanAccounts)
            {
                var loanId = l.AccountId;
                var start = l.StartDate;
                var payments = db.LoanTransactions.Where(t => t.LoanAccountId == loanId).ToList();
                var paymentsMade = payments.Count;
                var totalPaid = payments.Sum(t => (decimal?)t.Amount) ?? 0m;

                var now = DateTime.Today;
                var paymentsThisMonthSum = payments
                    .Where(t => t.TransDate.HasValue
                                && t.TransDate.Value.Year == now.Year
                                && t.TransDate.Value.Month == now.Month)
                    .Sum(t => (decimal?)t.Amount) ?? 0m;

                var emiValue = l.EMI ?? 0m;
                var emiPaidThisMonth = emiValue > 0m ? paymentsThisMonthSum >= emiValue : paymentsThisMonthSum > 0m;

                DateTime nextDue;
                try
                {
                    nextDue = start.AddMonths(paymentsMade + 1);
                }
                catch
                {
                    nextDue = start.AddMonths(Math.Max(1, paymentsMade + 1));
                }

                loanSummaries.Add(new LoanSummary
                {
                    LoanAccountId = loanId,
                    LoanAmount = l.LoanAmount,
                    EMI = l.EMI,
                    Outstanding = l.OutstandingAmount ?? 0m,
                    StartDate = start,
                    TenureMonths = l.TenureMonths,
                    PaymentsMade = paymentsMade,
                    TotalPaid = totalPaid,
                    EMIPaidThisMonth = emiPaidThisMonth,
                    NextEMIDate = nextDue
                });
            }

            // Prepare ViewBag
            // Keep primary savings account fetch for backward compatibility
            var primarySavingsAccount = db.Accounts.FirstOrDefault(a => a.CustomerId == customerId && a.AccountType == "Savings");

            ViewBag.Customer = customer;
            ViewBag.Account = primarySavingsAccount;
            ViewBag.Accounts = accounts;
            ViewBag.SavingsMap = db.SavingsAccounts.Where(s => accountIds.Contains(s.AccountId)).ToDictionary(s => s.AccountId);
            ViewBag.LoanMap = db.LoanAccounts.Where(l => accountIds.Contains(l.AccountId)).ToDictionary(l => l.AccountId);
            ViewBag.TotalBalance = totalBalance;
            ViewBag.LoanSummaries = loanSummaries;
            ViewBag.IncomingEMI = loanSummaries.Where(s => s.EMI.HasValue).Select(s => s.EMI ?? 0m).Sum();
            ViewBag.ActiveLoanCount = loanSummaries.Count(s => s.Outstanding > 0m);

            return View();
        }

        // GET: ViewTransactions - shows customer's transactions
        public ActionResult ViewTransactions()
        {
            var uname = Session["Username"];
            var customer = db.Customers.FirstOrDefault(c => c.CustName == (string)uname);
            if (customer == null) return RedirectToAction("Login", "Auth");
            var accountIds = db.Accounts.Where(a => a.CustomerId == customer.CustomerId).Select(a => a.AccountId).ToList();

            var savingsTx = db.SavingsTransactions
                .Where(t => accountIds.Contains(t.AccountId))
                .OrderByDescending(t => t.TransactionDate)
                .ToList();

            var loanTx = db.LoanTransactions
                .Where(t => accountIds.Contains(t.LoanAccountId))
                .OrderByDescending(t => t.TransDate)
                .ToList();

            ViewBag.SavingsTransactions = savingsTx;
            ViewBag.LoanTransactions = loanTx;
            ViewBag.Customer = customer;

            return View();
        }

        // GET: Transfer
        [HttpGet]
        public ActionResult Transfer()
        {
            return View();
        }

        // POST: Transfer
        [HttpPost]
        public ActionResult Transfer(int fromId, int toId, decimal amount)
        {
            ViewBag.Error = null;
            ViewBag.Message = null;

            if (amount <= 0)
            {
                ViewBag.Error = "⚠️ Please enter a valid transfer amount.";
                return View();
            }

            if (fromId == toId)
            {
                ViewBag.Error = "⚠️ Source and destination accounts must be different.";
                return View();
            }

            var from = db.SavingsAccounts.Find(fromId);
            var to = db.SavingsAccounts.Find(toId);

            if (from == null || to == null)
            {
                ViewBag.Error = "❌ One or both accounts not found.";
                return View();
            }

            if (from.Balance - amount < from.MinBalance)
            {
                ViewBag.Error = "❌ Insufficient balance. Minimum balance must be maintained.";
                return View();
            }

            using (var tx = db.Database.BeginTransaction())
            {
                try
                {
                    from.Balance -= amount;
                    var debitTxn = new SavingsTransaction
                    {
                        AccountId = fromId,
                        TransactionType = "Transfer",
                        Amount = amount,
                        BalanceAfter = from.Balance,
                        TransactionDate = DateTime.Now
                    };
                    db.SavingsTransactions.Add(debitTxn);

                    to.Balance += amount;
                    var creditTxn = new SavingsTransaction
                    {
                        AccountId = toId,
                        TransactionType = "Transfer",
                        Amount = amount,
                        BalanceAfter = to.Balance,
                        TransactionDate = DateTime.Now
                    };
                    db.SavingsTransactions.Add(creditTxn);

                    db.SaveChanges();
                    tx.Commit();

                    ViewBag.Message = $"✅ Transfer of ₹{amount} from Account #{fromId} to Account #{toId} completed successfully.";
                }
                catch
                {
                    try { tx.Rollback(); } catch { }
                    ViewBag.Error = "❌ Transfer failed. Please try again later.";
                }
            }

            return View();
        }

        // GET: PayEMI
        [HttpGet]
        public ActionResult PayEMI()
        {
            var uname = Session["Username"];
            var customer = db.Customers.FirstOrDefault(c => c.CustName == (string)uname);
            if (customer == null) return RedirectToAction("Login", "Auth");

            var accountIds = db.Accounts.Where(a => a.CustomerId == customer.CustomerId).Select(a => a.AccountId).ToList();
            var loans = db.LoanAccounts
                .Where(l => accountIds.Contains(l.AccountId) && (l.OutstandingAmount ?? 0m) > 0m && (l.IsClosed == null || l.IsClosed == false))
                .ToList();

            ViewBag.LoanAccounts = loans;
            return View();
        }

        // POST: PayEMI
        [HttpPost]
        public ActionResult PayEMI(int loanId, decimal amount)
        {
            TempData["Error"] = null;
            TempData["Success"] = null;

            if (amount <= 0m)
            {
                TempData["Error"] = "Please enter a valid amount.";
                return RedirectToAction("PayEMI");
            }

            var loan = db.LoanAccounts.Find(loanId);
            if (loan == null)
            {
                TempData["Error"] = "Loan not found.";
                return RedirectToAction("PayEMI");
            }

            loan.OutstandingAmount = loan.OutstandingAmount ?? loan.LoanAmount;
            var emiValue = loan.EMI ?? 0m;
            var outstanding = loan.OutstandingAmount ?? 0m;

            var now = DateTime.Now;
            var paymentsThisMonthSum = db.LoanTransactions
                .Where(t => t.LoanAccountId == loanId && t.TransDate.HasValue
                            && t.TransDate.Value.Year == now.Year && t.TransDate.Value.Month == now.Month)
                .Select(t => (decimal?)t.Amount)
                .ToList()
                .Where(v => v.HasValue)
                .Sum(v => v.Value);

            if (emiValue > 0m && paymentsThisMonthSum >= emiValue)
            {
                TempData["Error"] = $"EMI for this month is already paid (₹{emiValue:F2}).";
                return RedirectToAction("PayEMI");
            }

            var requiredAmount = outstanding <= emiValue ? outstanding : emiValue;
            bool amountMatchesRequired = Math.Abs(amount - requiredAmount) <= 0.01m;

            if (!amountMatchesRequired)
            {
                TempData["Error"] = $"Please pay the installment amount: ₹{requiredAmount:F2} (final settlement if outstanding is less).";
                return RedirectToAction("PayEMI");
            }

            using (var tx = db.Database.BeginTransaction())
            {
                try
                {
                    loan.OutstandingAmount = Math.Max(0m, outstanding - amount);
                    if (loan.OutstandingAmount <= 0m) loan.IsClosed = true;

                    db.LoanTransactions.Add(new LoanTransaction
                    {
                        LoanAccountId = loanId,
                        Amount = amount,
                        OutstandingAfter = loan.OutstandingAmount ?? 0m,
                        TransDate = DateTime.Now
                    });

                    db.SaveChanges();
                    tx.Commit();
                    TempData["Success"] = $"EMI payment of ₹{amount:F2} recorded.";
                }
                catch
                {
                    try { tx.Rollback(); } catch { }
                    TempData["Error"] = "EMI payment failed. Please try again.";
                }
            }

            return RedirectToAction("Dashboard");
        }

        // GET: ResetPassword
        [HttpGet]
        public ActionResult ResetPassword()
        {
            return View();
        }

        // POST: ResetPassword
        [HttpPost]
        public ActionResult ResetPassword(string Username, string oldPassword, string newPassword)
        {
            var uname = Session["Username"];
            if (string.IsNullOrEmpty(oldPassword) || string.IsNullOrEmpty(newPassword))
            {
                ViewBag.Message = "Please fill all fields.";
                return View();
            }

            var user = db.UserRegisters.FirstOrDefault(u => u.Username == (string)uname && u.PasswordHash == oldPassword);
            if (user == null)
            {
                ViewBag.Message = "Invalid current password.";
                return View();
            }

            user.PasswordHash = newPassword;
            db.SaveChanges();

            ViewBag.Message = "✅ Password reset successful!";
            return View();
        }
    }
}