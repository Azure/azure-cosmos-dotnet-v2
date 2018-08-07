#r "Microsoft.Azure.Documents.Client"
#r "Microsoft.WindowsAzure.Storage"

using Microsoft.Azure.Documents;
using System.Collections.Generic;
using System;
public static async Task Run(IReadOnlyList<Document> input, TraceWriter log)
{
    Microsoft.WindowsAzure.Storage.Queue.CloudQueueClient  queueClient;
    Microsoft.WindowsAzure.Storage.Queue.CloudQueue  queue;
    Microsoft.WindowsAzure.Storage.CloudStorageAccount storageAccount = Microsoft.WindowsAzure.Storage.CloudStorageAccount.Parse("DefaultEndpointsProtocol=https;AccountName=scmrstorage;AccountKey=K4DcPf7cGGtopTMtaRr3L8bENL/wuXjFjN5Yjdy2ixw5QkxL66DsdiWLka5BzVRWPM2dzCp8vEOwRoTE9elYLg==;EndpointSuffix=core.windows.net");
    queueClient = storageAccount.CreateCloudQueueClient();
    queue = queueClient.GetQueueReference("trafficqueue");

    foreach (var doc in input) {
        if (doc.GetPropertyValue <string>("iotid") == "AA") {
            string m = String.Format ("{{ \"lat\": {0}, \"long\": {1}, \"carId\": \"{2}\" }}", 
                                doc.GetPropertyValue <string>("lat"),
                                doc.GetPropertyValue <string>("longitude"),
                                doc.GetPropertyValue <string>("carid")  );
            Microsoft.WindowsAzure.Storage.Queue.CloudQueueMessage  message = new Microsoft.WindowsAzure.Storage.Queue.CloudQueueMessage 
            (m); 
            queue.AddMessage(message);
            }
        }
}