using Microsoft.AspNetCore.Mvc;

namespace AttendenceManagementSystem.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            // Check if user is authenticated
            if (User?.Identity?.IsAuthenticated == true)
            {
                // Redirect to appropriate dashboard based on role
                if (User.IsInRole("Admin"))
                {
                    return RedirectToAction("Dashboard", "Admin");
                }
                else if (User.IsInRole("Teacher"))
                {
                    return RedirectToAction("Dashboard", "Teacher");
                }
                else if (User.IsInRole("Student"))
                {
                    return RedirectToAction("Dashboard", "Student");
                }
            }

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View();
        }
    }
}