using Microsoft.AspNetCore.Mvc;

namespace DufflDigital.Web.Controllers
{
    public class DashboardController : Controller
    {
        // This action renders the empty landing page after login
        public IActionResult Index()
        {
            // Security: Check if Session exists
            var userName = HttpContext.Session.GetString("UserName");

            if (string.IsNullOrEmpty(userName))
            {
                // Redirect back to login if session is empty
                return RedirectToAction("Login", "Account");
            }

            // Pass the name to the view to show a welcome message
            ViewBag.AdminName = userName;
            return View();
        }
    }
}