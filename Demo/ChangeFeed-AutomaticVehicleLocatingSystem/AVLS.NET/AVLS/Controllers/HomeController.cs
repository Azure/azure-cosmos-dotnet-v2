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
        CloudQueue trafficQueue;
        public HomeController()
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(CloudConfigurationManager.GetSetting("StorageConnectionString"));
            CloudQueueClient queueClient = storageAccount.CreateCloudQueueClient();
            trafficQueue = queueClient.GetQueueReference("trafficqueue");
        }

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

        /// <summary>
        /// This method is callee directly from html page Avls.cshtml
        /// Gets the message from trafficQueue. Each message keeps the lat and long of the spot where accident happened.
        /// </summary>
        /// <param name="consistency"></param>
        /// <returns></returns>
        /// 
        public string GetNumbers(string consistency)
        {
            CloudQueueMessage retrievedMessage = trafficQueue.GetMessage();
            if (retrievedMessage != null)
            {
                trafficQueue.DeleteMessage(retrievedMessage);
                return retrievedMessage.AsString;
            }
            else
                return null;  
        }
    }
}