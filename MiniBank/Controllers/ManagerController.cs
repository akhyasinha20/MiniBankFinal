using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using MiniBank.Models;

namespace MiniBank.Controllers
{   
    public class ManagerController : Controller
    {   private MiniBankDBEntities1 db = new MiniBankDBEntities1();
        // GET: Manager
        public ActionResult Dashboard()
        {
            ViewBag.TotalEmployees = db.UserRegisters.Count(u => u.Role == "Employee");
            ViewBag.TotalCustomers = db.Customers.Count();
            return View();
        }

        public ActionResult AddEmployee()
        {
            var emps = db.UserRegisters.Where(u => u.Role == "Employee").ToList();
            return View(emps);
        }

        public ActionResult Approve(int id)
        {
            var emp = db.UserRegisters.Find(id);
            if (emp != null)
            {
                emp.IsActive = true;
                db.SaveChanges();
            }
            return RedirectToAction("AddEmployee");
        }

        public ActionResult Remove(int id)
        {
            var emp = db.UserRegisters.Find(id);
            if (emp != null)
            {
                db.UserRegisters.Remove(emp);
                db.SaveChanges();
            }
            return RedirectToAction("AddEmployee");
        }

        public ActionResult ViewTransactions()
        {
            var tx = db.SavingsTransactions.OrderByDescending(t => t.TransactionDate).ToList();
            return View(tx);
        }

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