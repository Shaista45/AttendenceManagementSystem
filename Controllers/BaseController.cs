using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace AttendenceManagementSystem.Controllers
{
    [Authorize]
    public class BaseController : Controller
    {
        protected string GetCurrentUserId()
        {
            return User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        }

        protected bool IsUserInRole(string role)
        {
            return User.IsInRole(role);
        }

        protected void ShowMessage(string message, string type = "success")
        {
            TempData["Message"] = message;
            TempData["MessageType"] = type;
        }

        protected void ShowErrorMessage(string message)
        {
            TempData["ErrorMessage"] = message;
        }
    }
}