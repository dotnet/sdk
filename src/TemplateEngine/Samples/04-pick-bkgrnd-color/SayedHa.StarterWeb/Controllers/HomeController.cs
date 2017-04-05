using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace SayedHa.StarterWeb.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult About()
        {
            ViewData["Message"] = "Your application description page.";

            return View();
        }

#if (EnableContactPage)
        public IActionResult Contact()
        {
            ViewData["Message"] = "Your contact page.";

            return View();
        }

#endif
        public IActionResult Error()
        {
            return View();
        }
    }
}
