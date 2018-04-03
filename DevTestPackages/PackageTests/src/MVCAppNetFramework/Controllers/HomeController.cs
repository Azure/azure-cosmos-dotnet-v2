using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using SmokeTestLib.NetStandard;

namespace MVCAppNetFramework.Controllers
{
    public class HomeController : Controller
    {
        public async Task<ActionResult> Index()
        {
            var binDir = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "bin");

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

        public ActionResult About()
        {
            ViewBag.Message = "Your application description page.";

            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Your contact page.";

            return View();
        }
    }
}