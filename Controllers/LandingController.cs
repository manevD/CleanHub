using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CleanHub.Controllers
{
    /// <summary>
    /// Јавна почетна страна (без најава): што работи фирмата, пред/потоа, контакт.
    /// Горе десно „Најава“ → апликацијата. Најавен корисник гледа „Отвори апликација“.
    /// </summary>
    [AllowAnonymous]
    public class LandingController : Controller
    {
        [Route("")]
        [Route("Landing")]
        public IActionResult Index() => View();
    }
}
