using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Dapper;
using System.Data;
using DufflDigital.Web.Models;
using DufflDigital.Web.Services;

namespace DufflDigital.Web.Controllers
{
    public class AccountController : Controller
    {
        private readonly string _connString;
        private readonly IEmailService _emailService;

        public AccountController(IConfiguration config, IEmailService emailService)
        {
            _connString = config.GetConnectionString("DufflDb");
            _emailService = emailService;
        }

        [HttpGet]
        public IActionResult Login() => View();

        //[HttpPost]
        //public async Task<IActionResult> Login(string email, string password)
        //{
        //    // Trim inputs to avoid accidental spaces
        //    email = email?.Trim() ?? "";
        //    password = password?.Trim() ?? "";

        //    using (IDbConnection db = new SqlConnection(_connString))
        //    {
        //        string sql = @"SELECT * FROM tblDDAdminUsers 
        //                       WHERE LOWER(Email)    = LOWER(@Email) 
        //                       AND   LOWER(Password) = LOWER(@Password)
        //                       AND   Status = 1";

        //        var user = await db.QueryFirstOrDefaultAsync<AdminUser>(
        //            sql, new { Email = email, Password = password });

        //        if (user != null)
        //        {
        //            HttpContext.Session.SetString("UserName", user.FullName);
        //            HttpContext.Session.SetString("UserRole", user.Role ?? "Admin");
        //            return RedirectToAction("Index", "Dashboard");
        //        }
        //    }

        //    ViewBag.Error = "Invalid username or password.";
        //    return View();
        //}

        [HttpPost]
        public async Task<IActionResult> Login(string email, string password)
        {
            email = email?.Trim() ?? "";
            password = password?.Trim() ?? "";

            using (IDbConnection db = new SqlConnection(_connString))
            {
                string sql = @"SELECT * FROM tblDDAdminUsers 
                       WHERE LOWER(Email) = LOWER(@Email) 
                       AND   Password COLLATE SQL_Latin1_General_CP1_CS_AS = @Password
                       AND   Status = 1";

                var user = await db.QueryFirstOrDefaultAsync<AdminUser>(
                    sql, new { Email = email, Password = password });

                if (user != null)
                {
                    HttpContext.Session.SetString("UserName", user.FullName);
                    HttpContext.Session.SetString("UserRole", user.Role ?? "Admin");
                    return RedirectToAction("Index", "Dashboard");
                }
            }

            ViewBag.Error = "Invalid username or password.";
            return View();
        }






        [HttpGet]
        public IActionResult ForgotPassword() => View();

        [HttpPost]
        public async Task<IActionResult> ForgotPassword(string email)
        {
            email = email?.Trim() ?? "";

            if (string.IsNullOrEmpty(email))
            {
                ViewBag.Error = "Please enter your email.";
                return View();
            }

            using (IDbConnection db = new SqlConnection(_connString))
            {
                string otp = new Random().Next(100000, 999999).ToString();
                int result = 0;

                // Step 1: Save OTP to DB
                try
                {
                    result = await db.ExecuteScalarAsync<int>(
                        "spDD_SetPasswordOTP",
                        new { Email = email, OTP = otp },
                        commandType: CommandType.StoredProcedure
                    );
                }
                catch (Exception ex)
                {
                    ViewBag.Error = "Database error: " + ex.Message;
                    return View();
                }

                // Step 2: Email not found in DB
                if (result <= 0)
                {
                    ViewBag.Info = "If this email is registered, you will receive an OTP shortly.";
                    return View();
                }

                // Step 3: Send OTP email
                try
                {
                    await _emailService.SendOtpEmailAsync(email, otp);
                    TempData["ResetEmail"] = email;
                    return RedirectToAction("VerifyOtp");
                }
                catch (Exception ex)
                {
                    ViewBag.Error = "Email failed: " + ex.Message;
                    return View();
                }
            }
        }

        [HttpGet]
        public IActionResult VerifyOtp() => View();

        [HttpPost]
        public async Task<IActionResult> VerifyOtp(string otp, string newPassword, string confirmPassword)
        {
            // Trim inputs to avoid whitespace issues
            otp = otp?.Trim() ?? "";
            newPassword = newPassword?.Trim() ?? "";
            confirmPassword = confirmPassword?.Trim() ?? "";

            string email = TempData["ResetEmail"]?.ToString();
            if (string.IsNullOrEmpty(email))
                return RedirectToAction("ForgotPassword");

            if (newPassword != confirmPassword)
            {
                ViewBag.Error = "Passwords do not match.";
                TempData.Keep("ResetEmail");
                return View();
            }

            using (IDbConnection db = new SqlConnection(_connString))
            {
                var success = await db.ExecuteScalarAsync<int>(
                    "spDD_ResetPasswordWithOTP",
                    new { Email = email, OTP = otp, NewPassword = newPassword },
                    commandType: CommandType.StoredProcedure
                );

                if (success == 1)
                    return RedirectToAction("Login");
            }

            ViewBag.Error = "Invalid or Expired OTP.";
            TempData.Keep("ResetEmail");
            return View();
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }
    }
}