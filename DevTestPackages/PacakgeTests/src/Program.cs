using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
#if UseNetStandard
using SmokeTestLib.NetStandard;
#elif UseNetFramework
using SmokeTestLib;
#endif

namespace ConsoleAppNetFrameworkRefNetFramework
{
    class Program
    {        
        static void Main(string[] args)
        {
            var binDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            var nativeDll = new List<string>() {
                "Microsoft.Azure.Documents.ServiceInterop.dll",                
                "DocumentDB.Spatial.Sql.dll",
            };
            
            foreach(var dll in nativeDll)
            {
                var color = Console.ForegroundColor;
                var dllPath = Path.Combine(binDir, dll);
                if (File.Exists(dllPath))
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"Exists - {dllPath}");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Missing - {dllPath}");
                }
                Console.ForegroundColor = color;
            }

#if UseBoth
            var test = new SmokeTestLib.SmokeTest();
            test.RunDemoAsync().Wait();

            var test2 = new SmokeTestLib.NetStandard.SmokeTest();
            test.RunDemoAsync().Wait();
#else
            var test = new SmokeTest();
            test.RunDemoAsync().Wait();
#endif

            Console.WriteLine("Press any key to exit.");
            Console.ReadLine();
        }
    }
}
