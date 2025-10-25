using MiniBank.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using static System.Web.Razor.Parser.SyntaxConstants;
using MiniBank.Filters;
namespace MiniBank.Controllers
{using MiniBank.Filters;
    public class ManagerController : Controller
    {
        
        private MiniBankDBNewEntities db = new MiniBankDBNewEntities();
        [SessionAuthorize]
        // GET: Manager
        public ActionResult Dashboard()
        {
            ViewBag.TotalEmployees = db.UserRegisters.Count(u => u.Role == "Employee");
            ViewBag.TotalCustomers = db.Customers.Count();
            ViewBag.TotalLoanAccounts = db.LoanAccounts.Count();
            ViewBag.TotalFixedAccounts = db.FixedDepositAccounts.Count();
            
            return View();
        }

        
        [SessionAuthorize]
        public ActionResult AddEmployee()
        {
            var emps = db.UserRegisters.Where(u => u.Role == "Employee").ToList();
            return View(emps);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Approve(int id)
        {
            var usr = db.UserRegisters.Find(id);
            if (usr == null)
            {
                TempData["Error"] = "User not found.";
                return RedirectToAction("AddEmployee");
            }

            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    if (!usr.ReferenceId.HasValue)
                    {
                        var employee = new Employee
                        {
                            EmployeeName = usr.Username,
                            DeptId = "DEPT01",
                            Email = usr.Email,
                            IsActive = true,
                            CreatedAt = DateTime.Now
                        };

                        db.Employees.Add(employee);
                        db.SaveChanges(); 

                        usr.ReferenceId = employee.EmpId;
                    }
                    else
                    {
                        var existingEmp = db.Employees.Find(usr.ReferenceId.Value);
                        if (existingEmp != null)
                        {
                            existingEmp.IsActive = true;
                        }
                    }
                    usr.IsActive = true;
                    db.SaveChanges();

                    transaction.Commit();

                    TempData["Success"] = "Employee approved and added successfully.";
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    TempData["Error"] = "Failed to approve employee: " + ex.Message;
                }
            }

            return RedirectToAction("AddEmployee");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Remove(int id)
        {
            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    var usr = db.UserRegisters.Find(id);
                    if (usr != null)
                    {
                        if (usr.ReferenceId.HasValue)
                        {
                            var emp = db.Employees.Find(usr.ReferenceId.Value);
                            if (emp != null)
                            {
                                db.Employees.Remove(emp);
                            }
                        }

                        db.UserRegisters.Remove(usr);
                        db.SaveChanges();
                    }

                    transaction.Commit();

                    TempData["Success"] = "Employee removed.";
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    TempData["Error"] = "Failed to remove employee: " + ex.Message;
                }
            }

            return RedirectToAction("AddEmployee");
        }

       
        [SessionAuthorize]
        [HttpGet]
        public ActionResult OpenAccount() => View();

        [HttpPost]
        public ActionResult OpenAccount(string pan, string name, DateTime? dob, decimal? minBalance, string email)
        {
           
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

            // Basic server-side validations 
            var nameRegex = new System.Text.RegularExpressions.Regex(@"^[A-Z][a-zA-Z]*(?:[ '-][A-Z][a-zA-Z]*)*$");
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
                MinBalance = 1000m
            };
            db.SavingsAccounts.Add(savings);
            db.SaveChanges();

            // create login for the customer
            var username = cust.CustName;
            var password = "Password1234";

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


        [SessionAuthorize]
        [HttpGet]
        public ActionResult OpenFixedDeposit() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult OpenFixedDeposit(int customerId, decimal principalAmount, int tenureYears)
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

            // Determine customer's age and senior status
            var age = (cust.DOB.HasValue)
                ? (DateTime.Today.Year - cust.DOB.Value.Year - (DateTime.Today.DayOfYear < cust.DOB.Value.DayOfYear ? 1 : 0))
                : 0;
            var isSenior = age >= 60;

            // ROI tiers:
            // <=1 year => 6%
            // >1 && <=2 years => 7%
            // >2 years => 8%
            decimal roi;
            if (tenureYears <= 1) roi = 6.0m;
            else if (tenureYears <= 2) roi = 7.0m;
            else roi = 8.0m;

            if (isSenior) roi += 0.5m;

            // Compound interest (annual compounding): A = P * (1 + r)^n
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
                StartDate = DateTime.Now,
                EndDate = DateTime.Now.AddYears(tenureYears),
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

        
        // Open Loan Account
        [SessionAuthorize]
        [HttpGet]
        public ActionResult OpenLoanAccount()
        {
           
            ViewBag.Customers = db.Customers.ToList();
            return View();
        }
    
         

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult OpenLoanAccount(int customerId, decimal loanAmount, int tenureMonths, decimal monthlyTakeHome)
        {
            ViewBag.Customers = db.Customers.ToList();

            var customer = db.Customers.Find(customerId);
            if (customer == null)
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
            var age = (customer.DOB.HasValue) ? (DateTime.Today.Year - customer.DOB.Value.Year - (DateTime.Today.DayOfYear < customer.DOB.Value.DayOfYear ? 1 : 0)) : 0;
            var isSenior = age >= 60;

            if (isSenior && loanAmount > 100000m)
            {
                ViewBag.Error = "Senior citizens cannot be sanctioned a loan greater than ₹1,00,000.";
                return View();
            }

            // ROI determination per slabs (senior override to 9.5%)
            decimal roi;
            if (isSenior) roi = 9.5m;
            else if (loanAmount <= 500000m) roi = 10.0m;
            else if (loanAmount <= 1000000m) roi = 9.5m;
            else roi = 9.0m;

            if (tenureMonths <= 0)
            {
                ViewBag.Error = "Tenure must be greater than zero months.";
                return View();
            }

            // Calculate EMI (standard formula)
            var monthlyRate = (double)roi / 1200.0;
            double emiDouble;
            if (monthlyRate <= 0.0)
                emiDouble = (double)loanAmount / tenureMonths;
            else
                emiDouble = (double)loanAmount * monthlyRate / (1.0 - Math.Pow(1.0 + monthlyRate, -tenureMonths));

            var emi = Math.Round((decimal)emiDouble, 2);

            // Enforce EMI <= 60% of monthly take-home
            var maxAllowedEmi = Math.Round(monthlyTakeHome * 0.60m, 2);
            if (emi > maxAllowedEmi)
            {
                ViewBag.Error = $"Calculated EMI ₹{emi:F2} exceeds 60% of customer's monthly take-home (max allowed ₹{maxAllowedEmi:F2}).";
                return View();
            }

            // Create account & loan
            var acc = new Account { AccountType = "Loan", CustomerId = customer.CustomerId, CreatedAt = DateTime.Now };
            db.Accounts.Add(acc);
            db.SaveChanges();

            var loan = new LoanAccount
            {
                AccountId = acc.AccountId,
                LoanAmount = loanAmount,
                StartDate = DateTime.Now,
                TenureMonths = tenureMonths,
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
            ViewBag.Message = $"Loan account created. Account#: {accountNumber} (ID: {acc.AccountId}), EMI: ₹{emi:F2}, ROI: {roi:F2}%";

            return View();
        }
        
        // Deposit / Withdraw (savings) 
        
        [SessionAuthorize]
        [HttpGet]
        public ActionResult DepositWithdraw() => View();

        [HttpPost]
        public ActionResult DepositWithdraw(int accountId, string type, decimal amount)
        {
            ViewBag.Message = null;

            var account = db.SavingsAccounts.FirstOrDefault(a => a.AccountId == accountId);

            if (account == null)
            {
                ViewBag.Message = "❌ Account ID not found.";
                return View();
            }

            if (type == "Deposit")
            {
                if (amount < 100)
                {
                    ViewBag.Message = "⚠️ Minimum deposit should be ₹100.";
                    return View();
                }

                account.Balance += amount;

                var txn = new SavingsTransaction
                {
                    AccountId = accountId,
                    TransactionType = "Deposit",
                    Amount = amount,
                    BalanceAfter = account.Balance,
                    TransactionDate = DateTime.Now
                };
                db.SavingsTransactions.Add(txn);
                db.SaveChanges();

                ViewBag.Message = $"✅ ₹{amount} deposited successfully!";
            }
            else if (type == "Withdraw")
            {
                if (amount <= 0)
                {
                    ViewBag.Message = "⚠️ Invalid withdrawal amount.";
                    return View();
                }

                if (account.Balance - amount < account.MinBalance)
                {
                    ViewBag.Message = $"❌ Insufficient balance. Minimum balance of ₹{account.MinBalance} must be maintained.";
                    return View();
                }

                account.Balance -= amount;

                var txn = new SavingsTransaction
                {
                    AccountId = accountId,
                    TransactionType = "Withdraw",
                    Amount = amount,
                    BalanceAfter = account.Balance,
                    TransactionDate = DateTime.Now
                };
                db.SavingsTransactions.Add(txn);
                db.SaveChanges();

                ViewBag.Message = $"✅ ₹{amount} withdrawn successfully!";
            }
            else
            {
                ViewBag.Message = "⚠️ Unknown transaction type.";
            }

            return View();
        }

        
        // Transactions view
        

       
        [SessionAuthorize]
        [HttpGet]
        public ActionResult ManageCustomers(int? customerId)
        {
            // If a specific customerId supplied, show its accounts for management
            if (customerId.HasValue)
            {
                var cust = db.Customers.Find(customerId.Value);
                if (cust == null)
                {
                    TempData["Error"] = $"Customer with ID {customerId.Value} not found.";
                    return RedirectToAction("ManageCustomers");
                }

                var accounts = db.Accounts.Where(a => a.CustomerId == customerId.Value).ToList();
                var accountIds = accounts.Select(a => a.AccountId).ToList();

                var savingsMap = db.SavingsAccounts
                    .Where(s => accountIds.Contains(s.AccountId))
                    .ToDictionary(s => s.AccountId);

                var loanMap = db.LoanAccounts
                    .Where(l => accountIds.Contains(l.AccountId))
                    .ToDictionary(l => l.AccountId);

                ViewBag.Customer = cust;
                ViewBag.Accounts = accounts;
                ViewBag.SavingsMap = savingsMap;
                ViewBag.LoanMap = loanMap;

                return View();
            }

            // otherwise list all customers (default)
            var customers = db.Customers.OrderBy(c => c.CustomerId).ToList();
            return View(customers);
        }

        // Close selected accounts for a customer 
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CloseSelectedAccounts(int? customerId, int[] selectedSavingsIds, int[] selectedLoanIds)
        {
            if (!customerId.HasValue)
            {
                TempData["Error"] = "Customer ID missing.";
                return RedirectToAction("ManageCustomers");
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
                                return RedirectToAction("ManageCustomers", new { customerId = customerId.Value });
                            }

                            var txns = db.SavingsTransactions.Where(t => t.AccountId == accId).ToList();
                            if (txns.Any()) db.SavingsTransactions.RemoveRange(txns);

                            db.SavingsAccounts.Remove(savings);
                            db.Accounts.Remove(account);
                        }
                    }

                    // Close loan accounts selected
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
                                return RedirectToAction("ManageCustomers", new { customerId = customerId.Value });
                            }

                            loan.IsClosed = true;
                            db.Entry(loan).State = EntityState.Modified;
                        }
                    }

                    db.SaveChanges();
                    tx.Commit();

                    TempData["Success"] = "Selected account(s) processed successfully.";
                    return RedirectToAction("ManageCustomers", new { customerId = customerId.Value });
                }
                catch (Exception ex)
                {
                    tx.Rollback();
                    TempData["Error"] = "Failed to close selected accounts: " + ex.Message;
                    return RedirectToAction("ManageCustomers", new { customerId = customerId.Value });
                }
            }
        }

        // Remove customer when no accounts exist
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CloseCustomerConfirmed(int? customerId)
        {
            if (!customerId.HasValue)
            {
                TempData["Error"] = "Customer ID is required.";
                return RedirectToAction("ManageCustomers");
            }

            var cust = db.Customers.Find(customerId.Value);
            if (cust == null)
            {
                TempData["Error"] = $"Customer with ID {customerId.Value} not found.";
                return RedirectToAction("ManageCustomers");
            }

            var hasAccounts = db.Accounts.Any(a => a.CustomerId == customerId.Value);
            if (hasAccounts)
            {
                TempData["Error"] = "Customer still has linked account(s). Close all accounts before removing the customer.";
                return RedirectToAction("ManageCustomers", new { customerId = customerId.Value });
            }

            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
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
                    return RedirectToAction("ManageCustomers", new { customerId = customerId.Value });
                }
            }
        }

        
        // ViewTransactions 
        
        [SessionAuthorize]
        [HttpGet]
        public ActionResult ViewTransactions()
        {
           
            ViewBag.SavingsTransactions = null;
            ViewBag.LoanTransactions = null;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ViewTransactions(int? customerId, DateTime? fromDate, DateTime? toDate)
        {
            ViewBag.SavingsTransactions = null;
            ViewBag.LoanTransactions = null;
            ViewBag.Customer = null;
            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");

            // require at least customerId or both dates
            if (!customerId.HasValue && (!fromDate.HasValue || !toDate.HasValue))
            {
                ViewBag.Error = "Provide a Customer ID, or both From and To dates to search.";
                return View();
            }

            
            if ((fromDate.HasValue && !toDate.HasValue) || (!fromDate.HasValue && toDate.HasValue))
            {
                ViewBag.Error = "Provide both From and To dates when searching by date range.";
                return View();
            }

           
            if (fromDate.HasValue && toDate.HasValue)
            {
                var from = fromDate.Value.Date;
                var to = toDate.Value.Date;
                if (from > to)
                {
                    ViewBag.Error = "From Date cannot be later than To Date.";
                    return View();
                }
                if (from > DateTime.Today || to > DateTime.Today)
                {
                    ViewBag.Error = "Dates cannot be in the future.";
                    return View();
                }
            }

            
            IQueryable<SavingsTransaction> savingsQuery = db.SavingsTransactions;
            IQueryable<LoanTransaction> loanQuery = db.LoanTransactions;

           
            if (customerId.HasValue)
            {
                var accountIds = db.Accounts.Where(a => a.CustomerId == customerId.Value).Select(a => a.AccountId).ToList();
                if (!accountIds.Any())
                {
                    ViewBag.Error = $"No accounts found for Customer ID {customerId.Value}.";
                    return View();
                }

                savingsQuery = savingsQuery.Where(t => accountIds.Contains(t.AccountId));
                loanQuery = loanQuery.Where(t => accountIds.Contains(t.LoanAccountId));

                ViewBag.Customer = db.Customers.Find(customerId.Value);
            }

            // if date range supplied, apply filter
            if (fromDate.HasValue && toDate.HasValue)
            {
                var from = fromDate.Value.Date;
                var to = toDate.Value.Date;
                savingsQuery = savingsQuery.Where(t => DbFunctions.TruncateTime(t.TransactionDate) >= from && DbFunctions.TruncateTime(t.TransactionDate) <= to);
                loanQuery = loanQuery.Where(t => DbFunctions.TruncateTime(t.TransDate) >= from && DbFunctions.TruncateTime(t.TransDate) <= to);
            }

            var savingsTx = savingsQuery.OrderByDescending(t => t.TransactionDate).ToList();
            var loanTx = loanQuery.OrderByDescending(t => t.TransDate).ToList();

            ViewBag.SavingsTransactions = savingsTx;
            ViewBag.LoanTransactions = loanTx;

            return View();
        }

        
        // View transactions for a specific customer 
        
        public ActionResult ViewCustomerTransactions(int id) // id = customerId
        {
            var accountIds = db.Accounts.Where(a => a.CustomerId == id).Select(a => a.AccountId).ToList();

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
            ViewBag.Customer = db.Customers.Find(id);

            return View();
        }

        
        // Manage customers (list)
        
        [SessionAuthorize]
        public ActionResult ManageCustomers()
        {
            return View(db.Customers.ToList());
        }

        public ActionResult DeactivateCustomer(int id)
        {
            var cust = db.Customers.Find(id);
            if (cust != null)
            {
                var usr = db.UserRegisters.FirstOrDefault(u => u.ReferenceId == cust.CustomerId);
                if (usr != null) usr.IsActive = false;
                db.SaveChanges();
            }
            return RedirectToAction("ManageCustomers");
        }
    }
}