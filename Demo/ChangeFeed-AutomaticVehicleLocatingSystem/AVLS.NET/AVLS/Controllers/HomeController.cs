using Microsoft.Azure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace AVLS.Controllers
{
    public class HomeController : Controller
    {
        public HomeController()
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(CloudConfigurationManager.GetSetting("StorageConnectionString"));
            CloudQueueClient queueClient = storageAccount.CreateCloudQueueClient();
            trafficQueue = queueClient.GetQueueReference("trafficqueue");
             
        }
        CloudQueue trafficQueue;
        public ActionResult Index()
        {
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

        public ActionResult Avls()
        {
            return View();
        }

        public string GetNumbers(string consistency)
        {
            CloudQueueMessage retrievedMessage = trafficQueue.GetMessage();
            if (retrievedMessage != null)
            {
                trafficQueue.DeleteMessage(retrievedMessage);
                return retrievedMessage.AsString;
            }
            else
                return null; // "{ \"lat\": 40.7145500183105, \"long\":  - 74.0071411132813, \"carId\": \"ABC 432\" }";
        }
    }
}