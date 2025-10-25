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
using MiniBank.Filters;

namespace MiniBank.Controllers
{
    public class EmployeeController : Controller
    {  
        // GET: Employee
        private MiniBankDBNewEntities db = new MiniBankDBNewEntities();
        [SessionAuthorize]
        public ActionResult Dashboard() => View();

        [SessionAuthorize]
        [HttpGet]
        public ActionResult OpenAccount() => View();

        [HttpPost]
        public ActionResult OpenAccount(string pan, string name, DateTime? dob, decimal? minBalance, string email)
        {
            //Check if PAN exists
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

            var nameRegex = new Regex(@"^[A-Z][a-zA-Z]*(?:[ '-][A-Z][a-zA-Z]*)*$"); // only alphabets
            if (string.IsNullOrWhiteSpace(name) || !nameRegex.IsMatch(name.Trim()))
            {
                ViewBag.Error = "Customer name must contain only alphabets ";
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

          
            var username = cust.CustName;
            var password = "Password1234"; // meets at least one upper, lower, digit

           
           

            var user = new UserRegister
            {
                Username = username,
                PasswordHash = password, 
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

        

        private int GetIntFromByte(byte b) => b;

        [SessionAuthorize]
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

       

        [SessionAuthorize]
        [HttpGet]
        public ActionResult OpenFixedDeposit() => View();

      
       [HttpPost]
       [ValidateAntiForgeryToken]
       public ActionResult OpenFixedDeposit(int customerId, decimal principalAmount, int tenureYears, DateTime? startDate, bool? isSenior)
        {
            // Validate customer
            var cust = db.Customers.Find(customerId);
            if (cust == null)
            {
                ViewBag.Error = "Customer not found.";
                return View();
            }

            // Validate principal and tenure
            if (principalAmount < 10000m)
            {
                ViewBag.Error = "Minimum deposit for Fixed Deposit is ₹10,000.";
                return View();
            }

            if (tenureYears <= 0)
            {
                ViewBag.Error = "Tenure (in years) must be greater than zero.";
                return View();
            }

            // Start date: use provided or today; validate not future
            var start = startDate.HasValue ? startDate.Value.Date : DateTime.Today;
            if (start > DateTime.Today)
            {
                ViewBag.Error = "Start date cannot be in the future.";
                return View();
            }

            // Determine senior status from DOB; 
            var age = (cust.DOB.HasValue)
                ? (DateTime.Today.Year - cust.DOB.Value.Year - (DateTime.Today.DayOfYear < cust.DOB.Value.DayOfYear ? 1 : 0))
                : 0;
            var isSeniorFromDob = age >= 60;
            var finalIsSenior = isSeniorFromDob || (isSenior ?? false);

            // ROI tiers:
            // <=1 year => 6%
            // >1 && <=2 years => 7%
            // >2 years => 8%
            decimal roi;
            if (tenureYears <= 1) roi = 6.0m;
            else if (tenureYears <= 2) roi = 7.0m;
            else roi = 8.0m;

            if (finalIsSenior) roi += 0.5m;

            // Compound interest 
            var r = (double)roi / 100.0;
            var n = (double)tenureYears;
            double maturityDouble;
            try
            {
                maturityDouble = (double)principalAmount * Math.Pow(1.0 + r, n);
            }
            catch
            {
                ViewBag.Error = "Failed to compute maturity amount with given inputs.";
                return View();
            }

            var maturityAmount = Math.Round((decimal)maturityDouble, 2);

            // Create Account (Fixed Deposit)
            var acc = new Account
            {
                AccountType = "FixedDeposit",
                CustomerId = customerId,
                CreatedAt = DateTime.Now
            };
            db.Accounts.Add(acc);
            db.SaveChanges();

          
            var fd = new FixedDepositAccount
            {
                AccountId = acc.AccountId,
                CustomerId = customerId,
                StartDate = start,
                EndDate = start.AddYears(tenureYears),
                FD_ROI = roi,
                PrincipalAmount = principalAmount
            };
            db.FixedDepositAccounts.Add(fd);
            db.SaveChanges();

            
            var accountNumber = $"FD{acc.AccountId:D8}";
            ViewBag.Message = $"Fixed Deposit opened. Account#: {accountNumber} (ID: {acc.AccountId}). ROI: {roi:F2}%. Maturity Amount: ₹{maturityAmount:F2} (on {fd.EndDate:yyyy-MM-dd}).";

            ViewBag.FDAccountId = acc.AccountId;
            ViewBag.AccountNumber = accountNumber;
            ViewBag.Principal = principalAmount;
            ViewBag.TenureYears = tenureYears;
            ViewBag.ROI = roi;
            ViewBag.MaturityAmount = maturityAmount;
            ViewBag.MaturityDate = fd.EndDate.ToString("yyyy-MM-dd");

            return View();
        }

        [SessionAuthorize]
        [HttpGet]
        public ActionResult LoanAccount() => View();

    
       [HttpPost]
       public ActionResult LoanAccount(int customerId, decimal loanAmount, int tenure, decimal monthlyTakeHome)
        {
            var cust = db.Customers.Find(customerId);
            if (cust == null)
            {
                ViewBag.Error = "Customer not found.";
                return View();
            }

            // Minimum loan amount
            if (loanAmount < 10000m)
            {
                ViewBag.Error = "Minimum Loan amount is ₹10,000.";
                return View();
            }

            // Senior check (age >= 60)
            var age = (cust.DOB.HasValue) ? (DateTime.Today.Year - cust.DOB.Value.Year - (DateTime.Today.DayOfYear < cust.DOB.Value.DayOfYear ? 1 : 0)) : 0;
            var isSenior = age >= 60;

            // Senior citizens cannot be sanctioned loan > 100,000
            if (isSenior && loanAmount > 100000m)
            {
                ViewBag.Error = "Senior citizens cannot be sanctioned a loan greater than ₹1,00,000.";
                return View();
            }

            // Determine ROI
            decimal roi;
            if (isSenior) roi = 9.5m;
            else if (loanAmount <= 500000m) roi = 10.0m;       // up to 5 lakhs
            else if (loanAmount <= 1000000m) roi = 9.5m;      // 5 - 10 lakhs
            else roi = 9.0m;                                  // above 10 lakhs

            if (tenure <= 0)
            {
                ViewBag.Error = "Tenure must be greater than zero months.";
                return View();
            }

            // EMI calculation (standard formula, monthly compounding)
            var monthlyRate = (double)roi / 1200.0;
            double emiDouble;
            if (monthlyRate <= 0.0)
                emiDouble = (double)loanAmount / tenure;
            else
                emiDouble = (double)loanAmount * monthlyRate / (1.0 - Math.Pow(1.0 + monthlyRate, -tenure));

            var emi = Math.Round((decimal)emiDouble, 2);

            // Enforce EMI <= 60% of monthly take-home
            var maxAllowedEmi = Math.Round(monthlyTakeHome * 0.60m, 2);
            if (emi > maxAllowedEmi)
            {
                ViewBag.Error = $"Calculated EMI ₹{emi:F2} exceeds 60% of customer's monthly take-home (max allowed ₹{maxAllowedEmi:F2}).";
                return View();
            }

            // Create account & loan
            var acc = new Account { AccountType = "Loan", CustomerId = customerId, CreatedAt = DateTime.Now };
            db.Accounts.Add(acc);
            db.SaveChanges();

            var loan = new LoanAccount
            {
                AccountId = acc.AccountId,
                LoanAmount = loanAmount,
                StartDate = DateTime.Now,
                TenureMonths = tenure,
                AnnualROI = roi,
                EMI = emi,
                OutstandingAmount = loanAmount,
                IsClosed = false
            };

            db.LoanAccounts.Add(loan);
            db.SaveChanges();

            var accountNumber = $"LN{acc.AccountId:D8}";
            ViewBag.AccountNumber = accountNumber;
            ViewBag.EMI = emi;
            ViewBag.Message = $"Loan Account created. Account#: {accountNumber} (ID: {acc.AccountId}), EMI: ₹{emi:F2}, ROI: {roi:F2}%";

            return View();
        }

        

        [SessionAuthorize]
        [HttpGet]
        public ActionResult CloseAccount(int? customerId)
        {
            
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

                var accountIds = accounts.Select(a => a.AccountId).ToList();
                var savingsMap = db.SavingsAccounts.Where(s => accountIds.Contains(s.AccountId)).ToDictionary(s => s.AccountId);
                var loanMap = db.LoanAccounts.Where(l => accountIds.Contains(l.AccountId)).ToDictionary(l => l.AccountId);

                ViewBag.Customer = cust;
                ViewBag.Accounts = accounts;
                ViewBag.SavingsMap = savingsMap;
                ViewBag.LoanMap = loanMap;

                return View();
            }

            
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

        [SessionAuthorize]
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
