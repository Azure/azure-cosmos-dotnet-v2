using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SmokeTestLib.NetStandard;
using WebAppNetCore.Models;

namespace WebAppNetCore.Controllers
{
    public class HomeController : Controller
    {
        public async Task<IActionResult> Index()
        {
            var binDir = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory);

            var nativeDll = new List<string>() {
                "Microsoft.Azure.Documents.ServiceInterop.dll",
                "DocumentDB.Spatial.Sql.dll",
            };

            foreach (var dll in nativeDll)
            {
                var dllPath = Path.Combine(binDir, dll);
                if (!new FileInfo(dllPath).Exists)
                {
                    throw new Exception($"Missing - {dllPath}");
                }
            }

            var test = new SmokeTest();
            await test.RunDemoAsync();
            return View();            
        }

        public IActionResult About()
        {
            ViewData["Message"] = "Your application description page.";

            return View();
        }

        public IActionResult Contact()
        {
            ViewData["Message"] = "Your contact page.";

            return View();
        }

        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
