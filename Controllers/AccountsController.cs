using Microsoft.AspNetCore.Mvc;

namespace CleanHub.Controllers
{
    public class AccountsController : Controller
    {
        public IActionResult Login()
        {
            return Redirect("/Identity/Account/Login");
        }
    }
}
