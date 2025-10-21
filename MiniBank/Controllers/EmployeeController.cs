using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using MiniBank.Models;
namespace MiniBank.Controllers
{
    public class EmployeeController : Controller
    {
        // GET: Employee
        private MiniBankDBEntities1 db = new MiniBankDBEntities1();
        public ActionResult Dashboard() => View();

        [HttpGet]
        public ActionResult OpenAccount() => View();

        [HttpPost]
        public ActionResult OpenAccount(string pan, string name, DateTime? dob, decimal? minBalance)
        {
            // Step 1: Check if PAN exists
            if (string.IsNullOrEmpty(name) || !dob.HasValue || !minBalance.HasValue)
            {
                var existing = db.Customers.FirstOrDefault(c => c.PAN == pan);
                if (existing != null)
                {
                    ViewBag.Error = "Account cannot be generated as the PAN card already exists.";
                    return View();
                }

                // If PAN does not exist, prompt for additional details
                ViewBag.PAN = pan;
                ViewBag.Step = 2; // Indicate step 2
                return View();
            }

            // Step 2: Create account with additional details
            var cust = new Customer
            {
                CustName = name,
                PAN = pan,
                DOB = dob,
                CreatedAt = DateTime.Now
            };
            db.Customers.Add(cust);
            db.SaveChanges();

            var acc = new Account
            {
                AccountType = "Savings",
                CustomerId = cust.CustomerId,
                CreatedAt = DateTime.Now
            };
            db.Accounts.Add(acc);
            db.SaveChanges();

            var savings = new SavingsAccount
            {
                AccountId = acc.AccountId,
                Balance = minBalance.Value,
                MinBalance = 1000 // Assuming ₹1000 as the minimum balance
            };
            db.SavingsAccounts.Add(savings);
            db.SaveChanges();

            ViewBag.Message = $"Account successfully created! CustomerID: {cust.CustomerId}, AccountID: {acc.AccountId}";
            return View();
        }

        [HttpGet]
        public ActionResult DepositWithdraw() => View();

        [HttpPost]
        public ActionResult DepositWithdraw(int accountId, string type, decimal amount)
        {
            var acc = db.SavingsAccounts.Find(accountId);
            if (acc == null)
            {
                ViewBag.Error = "Account not found.";
                return View();
            }

            if (type == "Deposit")
            {
                if (amount < 100)
                {
                    ViewBag.Error = "Deposit must be greater than 100.";
                    return View();
                }
                acc.Balance += amount;
            }
            else if (type == "Withdrawal")
            {
                if (acc.Balance - amount < acc.MinBalance)
                {
                    ViewBag.Error = "Minimum balance ₹1000 required.";
                    return View();
                }
                acc.Balance -= amount;
            }

            db.SavingsTransactions.Add(new SavingsTransaction
            {
                AccountId = acc.AccountId,
                TransactionType = type,
                Amount = amount,
                BalanceAfter = acc.Balance,
                TransactionDate = DateTime.Now

            });
            db.SaveChanges();

            ViewBag.Message = "Transaction successful.";
            return View();
        }

        [HttpGet]
        public ActionResult LoanAccount() => View();

        [HttpPost]
        public ActionResult LoanAccount(int customerId, decimal loanAmount, int tenure)
        {
            var cust = db.Customers.Find(customerId);
            if (cust == null)
            {
                ViewBag.Error = "Customer not found.";
                return View();
            }

            if (!db.Accounts.Any(a => a.CustomerId == customerId && a.AccountType == "Savings"))
            {
                ViewBag.Error = "Customer must have a Savings Account first.";
                return View();
            }

            if (loanAmount < 10000)
            {
                ViewBag.Error = "Minimum loan ₹10,000.";
                return View();
            }

            decimal roi = 10.5M;
            if ((DateTime.Now.Year - (cust.DOB?.Year ?? 0)) >= 60)
            {
                roi = 9.5M;
                if (loanAmount > 100000)
                {
                    ViewBag.Error = "Senior citizen max loan ₹1 Lakh.";
                    return View();
                }
            }

            decimal emi = (loanAmount * roi / 1200) / (1 - (decimal)Math.Pow(1 + (double)(roi / 1200), -tenure));

            var acc = new Account { AccountType = "Loan", CustomerId = customerId };
            db.Accounts.Add(acc);
            db.SaveChanges();

            db.LoanAccounts.Add(new LoanAccount
            {
                AccountId = acc.AccountId,
                LoanAmount = loanAmount,
                StartDate = DateTime.Now,
                TenureMonths = tenure,
                AnnualROI = roi,
                EMI = emi,
                OutstandingAmount = loanAmount
            });
            db.SaveChanges();

            ViewBag.Message = $"Loan Account created. ID: {acc.AccountId}, EMI: ₹{emi:F2}";
            return View();
        }

        [HttpGet]
        public ActionResult Report()
        {
            ViewBag.Step = 1;
            return View();
        }

        [HttpPost]
        public ActionResult Report(string type, DateTime? fromDate, DateTime? toDate)
        {
            if (!fromDate.HasValue || !toDate.HasValue)
            {
                ViewBag.Error = "Please select a valid date range.";
                return View();
            }

            if (fromDate > toDate)
            {
                ViewBag.Error = "From Date cannot be later than To Date.";
                return View();
            }

            try
            {
                if (type == "Savings")
                {
                    var savingsTransactions = db.SavingsTransactions
                        .Where(t => t.TransactionDate >= fromDate && t.TransactionDate <= toDate)
                        .OrderByDescending(t => t.TransactionDate)
                        .Select(t => new
                        {
                            t.TransactionId,
                            t.AccountId,
                            t.TransactionType,
                            t.Amount,
                            t.BalanceAfter,
                            TransactionDate = t.TransactionDate ?? DateTime.Now
                        })
                        .ToList();
                    ViewBag.Transactions = savingsTransactions;
                    ViewBag.TransactionType = "Savings";
                }
                else if (type == "Loan")
                {
                    var loanTransactions = db.LoanTransactions
                        .Where(t => t.TransDate >= fromDate && t.TransDate <= toDate)
                        .OrderByDescending(t => t.TransDate)
                        .Select(t => new
                        {
                            t.TransactionId,
                            AccountId = t.LoanAccountId,
                            TransactionType = "EMI Payment",
                            t.Amount,
                            BalanceAfter = t.OutstandingAfter,
                            TransactionDate = t.TransDate ?? DateTime.Now
                        })
                        .ToList();
                    ViewBag.Transactions = loanTransactions;
                    ViewBag.TransactionType = "Loan";
                }
                else
                {
                    ViewBag.Error = "Invalid transaction type selected.";
                    return View();
                }

                ViewBag.FromDate = fromDate.Value.ToString("yyyy-MM-dd");
                ViewBag.ToDate = toDate.Value.ToString("yyyy-MM-dd");
                ViewBag.SelectedType = type;
                Console.WriteLine(ViewBag.Transactions);
                return View();
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Error fetching transactions: " + ex.Message;
                return View();
            }
        }
        }
    }