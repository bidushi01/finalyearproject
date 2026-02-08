using Microsoft.AspNetCore.Mvc;

namespace finalyearproject.UI.Controllers
{
    public class ServiceController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
