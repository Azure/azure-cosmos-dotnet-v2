using System;
using System.Collections.Generic;
using Microsoft.Azure.Documents;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;


namespace MonitorTraffic
{
    public static class MonitorTraffic
    {
        [FunctionName("MonitorTraffic")]
        public static void Run([CosmosDBTrigger(
            databaseName: "IoT",
            collectionName: "IoT",
            ConnectionStringSetting = "DBConnection",
            LeaseCollectionName = "leases")]IReadOnlyList<Document> documents, TraceWriter log)
        {

            Microsoft.WindowsAzure.Storage.Queue.CloudQueueClient queueClient;
            Microsoft.WindowsAzure.Storage.Queue.CloudQueue queue;
            Microsoft.WindowsAzure.Storage.CloudStorageAccount storageAccount = Microsoft.WindowsAzure.Storage.CloudStorageAccount.Parse("DefaultEndpointsProtocol=https;AccountName=scmrstorage;AccountKey=FtW5Qlz/5rWiqX0MPlGO0X2anGs5t7ea/H/ZkdcIEHlTA9isEinpscnuuhw8GwKR+7+Eo2IDRG1jwdMoDsRTqg==;EndpointSuffix=core.windows.net");
            queueClient = storageAccount.CreateCloudQueueClient();
            queue = queueClient.GetQueueReference("trafficqueue");

            foreach (var doc in documents)
            {
                if (doc.GetPropertyValue<string>("iotid") == "AA")
                {
                    string m = String.Format("{{ \"lat\": {0}, \"long\": {1}, \"carId\": \"{2}\" }}",
                                        doc.GetPropertyValue<string>("lat"),
                                        doc.GetPropertyValue<string>("longitude"),
                                        doc.GetPropertyValue<string>("carid"));
                    Microsoft.WindowsAzure.Storage.Queue.CloudQueueMessage message = new Microsoft.WindowsAzure.Storage.Queue.CloudQueueMessage
                    (m);
                    queue.AddMessage(message);
                }
            }


        }
    }
}
