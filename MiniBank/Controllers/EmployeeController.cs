using MiniBank.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Mvc;
using static System.Web.Razor.Parser.SyntaxConstants;

namespace MiniBank.Controllers
{
    public class EmployeeController : Controller
    {
        // GET: Employee
        private MiniBankDBEntities4 db = new MiniBankDBEntities4();
        public ActionResult Dashboard() => View();

        [HttpGet]
        public ActionResult OpenAccount() => View();

        [HttpPost]
        public ActionResult OpenAccount(string pan, string name, DateTime? dob, decimal? minBalance, string email)
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

                ViewBag.PAN = pan;
                ViewBag.Step = 2; 
                return View();
            }

            var nameRegex = new Regex(@"^[A-Za-z]+$"); // only alphabets, no spaces
            if (string.IsNullOrWhiteSpace(name) || !nameRegex.IsMatch(name.Trim()))
            {
                ViewBag.Error = "Customer name must contain only alphabets (A-Z, a-z) with no spaces.";
                ViewBag.PAN = pan;
                ViewBag.Step = 2;
                ViewBag.Name = name;
                ViewBag.DOB = dob?.ToString("yyyy-MM-dd");
                ViewBag.MinBalance = minBalance;
                ViewBag.Email = email;
                return View();
            }

            if (dob.HasValue && dob.Value.Date > DateTime.Today)
            {
                ViewBag.Error = "Date of Birth cannot be a future date.";
                ViewBag.PAN = pan;
                ViewBag.Step = 2;
                ViewBag.Name = name;
                ViewBag.DOB = dob?.ToString("yyyy-MM-dd");
                ViewBag.MinBalance = minBalance;
                ViewBag.Email = email;
                return View();
            }

            if (minBalance.HasValue && minBalance.Value < 1000)
            {
                ViewBag.Error = "Minimum balance must be ₹1000 or more.";
                ViewBag.PAN = pan;
                ViewBag.Step = 2;
                ViewBag.Name = name;
                ViewBag.DOB = dob?.ToString("yyyy-MM-dd");
                ViewBag.MinBalance = minBalance;
                ViewBag.Email = email;
                return View();
            }


            var cust = new Customer
            {
                CustName = name.Trim(),
                PAN = pan,
                DOB = dob,
                Email = email,
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

            // Generate login credentials for the customer
            var username = cust.CustName;
            var password = "Password1234"; // meets at least one upper, lower, digit

            // ensure username uniqueness
           

            var user = new UserRegister
            {
                Username = username,
                PasswordHash = password, // existing system stores plaintext; keep consistent
                Email = email,
                Role = "Customer",
                ReferenceId = cust.CustomerId,
                IsActive = true,
                CreatedAt = DateTime.Now
            };

            db.UserRegisters.Add(user);
            db.SaveChanges();

            ViewBag.Message = $"Account successfully created! CustomerID: {cust.CustomerId}, AccountID: {acc.AccountId}. Login: {username} / {password}";
            return View();
        }

        // Helper: create a username from PAN (fallback to cust id appended if needed)
        private string GenerateUsernameFromPAN(string pan, int customerId)
        {
            if (string.IsNullOrWhiteSpace(pan)) return "cust" + customerId;
            // remove spaces and make uppercase
            var clean = new string(pan.Where(char.IsLetterOrDigit).ToArray());
            return clean;
        }

        private int GetIntFromByte(byte b) => b;

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

        // Updated: include monthlyTakeHome and enforce validations described
        [HttpPost]
        public ActionResult LoanAccount(int customerId, decimal loanAmount, int tenure, decimal monthlyTakeHome)
        {
            var cust = db.Customers.Find(customerId);
            if (cust == null)
            {
                ViewBag.Error = "Customer not found.";
                return View();
            }

            if (loanAmount < 10000)
            {
                ViewBag.Error = "Minimum loan ₹10,000.";
                return View();
            }

            var age = (cust.DOB.HasValue) ? (DateTime.Today.Year - cust.DOB.Value.Year - (DateTime.Today.DayOfYear < cust.DOB.Value.DayOfYear ? 1 : 0)) : 0;
            var isSenior = age >= 60;

            if (isSenior && loanAmount > 100000)
            {
                ViewBag.Error = "Senior citizen max loan ₹1 Lakh.";
                return View();
            }

            decimal roi;
            if (isSenior) roi = 9.5M;
            else if (loanAmount <= 500000M) roi = 10.0M;
            else if (loanAmount <= 1000000M) roi = 9.5M;
            else roi = 9.0M;

            if (tenure <= 0)
            {
                ViewBag.Error = "Tenure must be greater than zero months.";
                return View();
            }

            var monthlyRate = (double)roi / 1200.0;
            double emiDouble;
            if (monthlyRate <= 0.0)
                emiDouble = (double)loanAmount / tenure;
            else
                emiDouble = (double)loanAmount * monthlyRate / (1.0 - Math.Pow(1.0 + monthlyRate, -tenure));

            var emi = Math.Round((decimal)emiDouble, 2);

            var maxAllowedEmi = Math.Round(monthlyTakeHome * 0.60m, 2);
            if (emi > maxAllowedEmi)
            {
                ViewBag.Error = $"EMI ₹{emi:F2} exceeds 60% of customer's monthly take-home (max allowed ₹{maxAllowedEmi:F2}).";
                return View();
            }

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

        // Replace existing CloseAccount GET action with this version (keep other methods unchanged)
        [HttpGet]
public ActionResult CloseAccount(int? customerId)
        {
            // If customerId supplied, list all accounts linked to that customer
            if (customerId.HasValue)
            {
                var cust = db.Customers.Find(customerId.Value);
                if (cust == null)
                {
                    TempData["Error"] = $"Customer with ID {customerId.Value} not found.";
                    ViewBag.Accounts = null;
                    ViewBag.Customer = null;
                    return View();
                }

                // All accounts for this customer
                var accounts = db.Accounts.Where(a => a.CustomerId == customerId.Value).ToList();

                // Preload related account details
                var accountIds = accounts.Select(a => a.AccountId).ToList();
                var savingsMap = db.SavingsAccounts.Where(s => accountIds.Contains(s.AccountId)).ToDictionary(s => s.AccountId);
                var loanMap = db.LoanAccounts.Where(l => accountIds.Contains(l.AccountId)).ToDictionary(l => l.AccountId);

                ViewBag.Customer = cust;
                ViewBag.Accounts = accounts;
                ViewBag.SavingsMap = savingsMap;
                ViewBag.LoanMap = loanMap;

                return View();
            }

            // No customerId supplied -> render lookup form
            ViewBag.Accounts = null;
            ViewBag.Customer = null;
            return View();
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CloseSelectedAccounts(int? customerId, int[] selectedSavingsIds, int[] selectedLoanIds)
        {
            if (!customerId.HasValue)
            {
                TempData["Error"] = "Customer ID missing.";
                return RedirectToAction("CloseAccount");
            }

            using (var tx = db.Database.BeginTransaction())
            {
                try
                {
                    // Close savings accounts selected
                    if (selectedSavingsIds != null && selectedSavingsIds.Length > 0)
                    {
                        foreach (var accId in selectedSavingsIds)
                        {
                            var savings = db.SavingsAccounts.Find(accId);
                            var account = db.Accounts.Find(accId);
                            if (savings == null || account == null) continue;

                            if (savings.Balance > 0m)
                            {
                                tx.Rollback();
                                TempData["Error"] = $"Savings account {accId} must have zero balance before closing.";
                                return RedirectToAction("CloseAccount", new { customerId = customerId.Value });
                            }

                            var txns = db.SavingsTransactions.Where(t => t.AccountId == accId).ToList();
                            if (txns.Any()) db.SavingsTransactions.RemoveRange(txns);

                            db.SavingsAccounts.Remove(savings);
                            db.Accounts.Remove(account);
                        }
                    }

                    // Close loan accounts selected (mark closed if outstanding 0)
                    if (selectedLoanIds != null && selectedLoanIds.Length > 0)
                    {
                        foreach (var accId in selectedLoanIds)
                        {
                            var loan = db.LoanAccounts.Find(accId);
                            if (loan == null) continue;

                            if ((loan.OutstandingAmount ?? 0m) > 0m)
                            {
                                tx.Rollback();
                                TempData["Error"] = $"Loan account {accId} has outstanding amount. Set to zero before closing.";
                                return RedirectToAction("CloseAccount", new { customerId = customerId.Value });
                            }

                            loan.IsClosed = true;
                            db.Entry(loan).State = EntityState.Modified;
                        }
                    }

                    db.SaveChanges();
                    tx.Commit();

                    TempData["Success"] = "Selected account(s) processed successfully.";
                    return RedirectToAction("CloseAccount", new { customerId = customerId.Value });
                }
                catch (Exception ex)
                {
                    tx.Rollback();
                    TempData["Error"] = "Failed to close selected accounts: " + ex.Message;
                    return RedirectToAction("CloseAccount", new { customerId = customerId.Value });
                }
            }
        }

        

  [HttpPost]
  [ValidateAntiForgeryToken]
public ActionResult CloseCustomerConfirmed(int? customerId)
        {
            if (!customerId.HasValue)
            {
                TempData["Error"] = "Customer ID is required.";
                return RedirectToAction("CloseAccount");
            }

            var cust = db.Customers.Find(customerId.Value);
            if (cust == null)
            {
                TempData["Error"] = $"Customer with ID {customerId.Value} not found.";
                return RedirectToAction("CloseAccount");
            }

            // If any accounts still exist, prevent deleting the customer
            var hasAccounts = db.Accounts.Any(a => a.CustomerId == customerId.Value);
            if (hasAccounts)
            {
                TempData["Error"] = "Customer still has linked account(s). Close all accounts before removing the customer.";
                return RedirectToAction("CloseAccount", new { customerId = customerId.Value });
            }

            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    // Optionally remove the UserRegister entry for this customer (keeps DB clean).
                    var user = db.UserRegisters.FirstOrDefault(u => u.ReferenceId == customerId.Value && u.Role == "Customer");
                    if (user != null)
                    {
                        db.UserRegisters.Remove(user);
                    }

                    db.Customers.Remove(cust);
                    db.SaveChanges();

                    transaction.Commit();
                    TempData["Success"] = $"Customer {cust.CustName} (ID: {cust.CustomerId}) removed successfully.";
                    return RedirectToAction("ManageCustomers");
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    TempData["Error"] = "Failed to remove customer: " + ex.Message;
                    return RedirectToAction("CloseAccount", new { customerId = customerId.Value });
                }
            }
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
         
            ViewBag.SelectedType = type;
            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");

        
            if (!fromDate.HasValue || !toDate.HasValue)
            {
                ViewBag.Error = "Please select a valid date range.";
                return View();
            }

            if (fromDate.Value.Date > toDate.Value.Date)
            {
                ViewBag.Error = "From Date cannot be later than To Date.";
                return View();
            }

         
            if (fromDate.Value.Date > DateTime.Today || toDate.Value.Date > DateTime.Today)
            {
                ViewBag.Error = "Enter valid date range (dates cannot be in the future).";
                return View();
            }

            try
            {
                var from = fromDate.Value.Date;
                var to = toDate.Value.Date;

                if (type == "Savings")
                {
                    var savingsTransactions = db.SavingsTransactions
                        .Where(t => DbFunctions.TruncateTime(t.TransactionDate) >= (DateTime?)from && DbFunctions.TruncateTime(t.TransactionDate) <= (DateTime?)to)
                        .OrderByDescending(t => t.TransactionDate)
                        .ToList();

                    ViewBag.Transactions = savingsTransactions;
                    ViewBag.TransactionType = "Savings";
                }
                else if (type == "Loan")
                {
                    
                    var loanTransactions = db.LoanTransactions
                        .Where(t => DbFunctions.TruncateTime(t.TransDate) >= (DateTime?)from && DbFunctions.TruncateTime(t.TransDate) <= (DateTime?)to)
                        .OrderByDescending(t => t.TransDate)
                        .ToList();

                    ViewBag.Transactions = loanTransactions;
                    ViewBag.TransactionType = "Loan";
                }
                else
                {
                    ViewBag.Error = "Invalid transaction type selected.";
                    return View();
                }

             
                ViewBag.FromDate = from.ToString("yyyy-MM-dd");
                ViewBag.ToDate = to.ToString("yyyy-MM-dd");

                ViewBag.SelectedType = type;
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
