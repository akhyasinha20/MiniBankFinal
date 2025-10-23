using MiniBank.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Mvc;
namespace MiniBank.Controllers
{
    public class AuthController : Controller
    {
        // GET: Auth
        private MiniBankDBEntities4 db = new MiniBankDBEntities4();

        [HttpGet]
        public ActionResult Login()
        {
            // Show message carried from Register (PRG)
            if (TempData["Message"] != null)
            {
                ViewBag.Message = TempData["Message"];
            }
            return View();
        }

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

            // Set session so layout can show Logout
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
            var usernameRegex = new Regex(@"^[A-Za-z]+$"); // only alphabets
            var passwordRegex = new Regex(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).+$");
            if (string.IsNullOrWhiteSpace(username) || !usernameRegex.IsMatch(username))
            {
                ViewBag.Error = "Username must contain only alphabets (A-Z, a-z).";
                ViewBag.Message = null;
                ViewBag.Username = username;
                ViewBag.Email = email;
                return View();
            }

            if (string.IsNullOrWhiteSpace(password) || !passwordRegex.IsMatch(password))
            {
                ViewBag.Error = "Password must contain at least one uppercase letter, one lowercase letter and one number.";
                ViewBag.Message = null;
                ViewBag.Username = username;
                ViewBag.Email = email;
                return View();
            }

            if (db.UserRegisters.Any(u => u.Username == username))
            {
                ViewBag.Error = "Username already exists.";
                ViewBag.Message = null;
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

            // PRG: redirect to Login and carry a success message
            TempData["Message"] = "Registration successful! Manager approval required for employees.";
            return RedirectToAction("Login");
        }

        public ActionResult Logout()
        {
            Session.Clear();
            return RedirectToAction("Login");
        }
    }
}