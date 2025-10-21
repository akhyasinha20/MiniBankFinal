using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using MiniBank.Models;
namespace MiniBank.Controllers
{
    public class AuthController : Controller
    {
        // GET: Auth
        private MiniBankDBEntities1 db = new MiniBankDBEntities1();
        [HttpGet]
        public ActionResult Login() => View();

        [HttpPost]
        public ActionResult Login(string username, string password)
        {
            var user = db.UserRegisters
                .FirstOrDefault(u => u.Username == username && u.PasswordHash == password);

            if (user == null)
            {
                ViewBag.Error = "Invalid username or password.";
                return View();
            }

            if ((bool)!user.IsActive)
            {
                ViewBag.Error = "Account not active. Manager approval required.";
                return View();
            }

            Session["UserId"] = user.UserId;
            Session["Role"] = user.Role;
            Session["Username"] = user.Username;

            switch (user.Role)
            {
                case "Manager": return RedirectToAction("Dashboard", "Manager");
                case "Employee": return RedirectToAction("Dashboard", "Employee");
                case "Customer": return RedirectToAction("Dashboard", "Customer");
                default:
                    ViewBag.Error = "Unknown role.";
                    return View();
            }
        }

        [HttpGet]
        public ActionResult Register() => View();

        [HttpPost]
        public ActionResult Register(string username, string password, string email, string role)
        {
            if (db.UserRegisters.Any(u => u.Username == username))
            {
                ViewBag.Error = "Username already exists.";
                return View();
            }

            var user = new UserRegister
            {
                Username = username,
                PasswordHash = password,
                Email = email,
                Role = role,
                IsActive = (role == "Manager")
            };

            db.UserRegisters.Add(user);
            db.SaveChanges();

            ViewBag.Message = "Registration successful! Manager approval required for employees.";
            return View();
        }

        public ActionResult Logout()
        {
            Session.Clear();
            return RedirectToAction("Login");
        }
    }
    }