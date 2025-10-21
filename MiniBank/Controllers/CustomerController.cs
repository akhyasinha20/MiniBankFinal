using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using MiniBank.Models;
namespace MiniBank.Controllers
{
    public class CustomerController : Controller
    {
        // GET: Customer
        private MiniBankDBEntities1 db = new MiniBankDBEntities1();
        public ActionResult Dashboard()
        {
            var uid = (int)Session["UserId"];
            var user = db.UserRegisters.Find(uid);
            if (user == null) return RedirectToAction("Login", "Auth");

            var cust = db.Customers.FirstOrDefault(c => c.CustomerId == user.ReferenceId);
            return View(cust);
        }

        public ActionResult ViewTransactions(int id)
        {
            var tx = db.SavingsTransactions.Where(t => t.AccountId == id).ToList();
            return View(tx);
        }

        [HttpGet]
        public ActionResult Transfer() => View();

        [HttpPost]
        public ActionResult Transfer(int fromId, int toId, decimal amount)
        {
            var from = db.SavingsAccounts.Find(fromId);
            var to = db.SavingsAccounts.Find(toId);
            if (from == null || to == null)
            {
                ViewBag.Error = "Invalid accounts.";
                return View();
            }

            if (from.Balance - amount < from.MinBalance)
            {
                ViewBag.Error = "Insufficient balance.";
                return View();
            }

            from.Balance -= amount;
            to.Balance += amount;

            db.SavingsTransactions.Add(new SavingsTransaction
            {
                AccountId = fromId,
                TransactionType = "Transfer",
                Amount = amount,
                BalanceAfter = from.Balance
            });
            db.SavingsTransactions.Add(new SavingsTransaction
            {
                AccountId = toId,
                TransactionType = "Deposit",
                Amount = amount,
                BalanceAfter = to.Balance
            });
            db.SaveChanges();

            ViewBag.Message = "Transfer successful.";
            return View();
        }

        [HttpGet]
        public ActionResult PayEMI() => View();

        [HttpPost]
        public ActionResult PayEMI(int loanId, decimal amount)
        {
            var loan = db.LoanAccounts.Find(loanId);
            if (loan == null)
            {
                ViewBag.Error = "Loan not found.";
                return View();
            }

            loan.OutstandingAmount -= amount;
            if (loan.OutstandingAmount <= 0) loan.IsClosed = true;

            db.LoanTransactions.Add(new LoanTransaction
            {
                LoanAccountId = loanId,
                Amount = amount,
                OutstandingAfter = (decimal)loan.OutstandingAmount
            });
            db.SaveChanges();

            ViewBag.Message = "EMI paid successfully.";
            return View();
        }
    }

}