using Microsoft.AspNetCore.Authorization;
using CleanHub.Models;
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

        /// <summary>Подстрана за една услуга, на пр. /uslugi/visoki-stakla</summary>
        [Route("uslugi/{slug}")]
        public IActionResult Service(string slug)
        {
            var service = LandingServices.Find(slug);
            return service == null ? NotFound() : View(service);
        }
    }
}
