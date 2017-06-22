namespace ChangefeedMigrationSample
{
    using DocumentDB.ChangeFeedProcessor;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    // ------------------------------------------------------------------------------------------------
    // This sample demonstrates using change processor library to read changes from source collection 
    // to destination collection 
    // ------------------------------------------------------------------------------------------------

    public class DeviceReading
    {
        [JsonProperty("id")]
        public string Id;

        [JsonProperty("deviceId")]
        public string DeviceId;

        [JsonConverter(typeof(IsoDateTimeConverter))]
        [JsonProperty("readingTime")]
        public DateTime ReadingTime;

        [JsonProperty("metricType")]
        public string MetricType;

        [JsonProperty("unit")]
        public string Unit;

        [JsonProperty("metricValue")]
        public double MetricValue;
    }

    class Program
    {
        private const string EndPointUrl = "https://interntest.documents.azure.com:443/";
        private const string PrimaryKey = "mXBmwssUDqDNL03M0qkmMBYizwpeIrLqCyFNUOGQsGCEeLRRkWJDleEORnVNzfQ13dkiIyxfhgVM4QAQLzQQzg==";
        private const string DBName = "SmartHome";
        private const string MonitoredCollectionName = "Nest";
        private const string LeaseCollectionName = "Lease";
        private const string DestCollName = "Dest";
        private const string DestCollPartKey = "/id";

        static void Main(string[] args)
        {
            Console.WriteLine("ChangeFeed App");
            Program newApp = new ChangefeedMigrationSample.Program();
            // Thread 1 comment out for thread 2 
            newApp.RunChangeFeedProcessor(EndPointUrl, PrimaryKey);
            // Thread 2 comment out for thread 1 
            // UpdateDb(EndPointUrl, PrimaryKey); 
            Console.ReadKey();
        }

        async void RunChangeFeedProcessor(string uri, string secretKey)
        {
            // connect client 
            DocumentClient client = new DocumentClient(new Uri(uri), secretKey);
            await client.CreateDatabaseIfNotExistsAsync(new Database { Id = DBName });

            // create monitor collection if it does not exist
            await client.CreateDocumentCollectionIfNotExistsAsync(
                UriFactory.CreateDatabaseUri(DBName),
                new DocumentCollection { Id = MonitoredCollectionName },
                new RequestOptions { OfferThroughput = 2500 });

            // create lease collect if it does not exist
            await client.CreateDocumentCollectionIfNotExistsAsync(
            UriFactory.CreateDatabaseUri(DBName),
            new DocumentCollection { Id = LeaseCollectionName },
            new RequestOptions { OfferThroughput = 2500 });
            await StartChangeFeedHost(uri, secretKey);
        }

        async Task StartChangeFeedHost(string uri, string secretKey)
        {
            string hostName = Guid.NewGuid().ToString();

            // monitored collection info 
            DocumentCollectionInfo documentCollectionLocation = new DocumentCollectionInfo
            {
                Uri = new Uri(uri),
                MasterKey = secretKey,
                DatabaseName = DBName,
                CollectionName = MonitoredCollectionName
            };

            // lease collection info 
            DocumentCollectionInfo leaseCollectionLocation = new DocumentCollectionInfo
            {
                Uri = new Uri(uri),
                MasterKey = secretKey,
                DatabaseName = DBName,
                CollectionName = LeaseCollectionName
            };
            ChangeFeedEventHost host = new ChangeFeedEventHost(hostName, documentCollectionLocation, leaseCollectionLocation);
            await host.RegisterObserverAsync<DocumentFeedObserver>();
            Console.WriteLine("Main program: press Enter to stop...");
            Console.ReadLine();
            await host.UnregisterObserversAsync();
        }

        class DocumentFeedObserver : IChangeFeedObserver
        {
            private static int s_totalDocs = 0;

            public Task OpenAsync(ChangeFeedObserverContext context)
            {
                Console.WriteLine("Worker opened, {0}", context.PartitionKeyRangeId);
                return Task.CompletedTask;  // Requires targeting .NET 4.6+.
            }

            public Task CloseAsync(ChangeFeedObserverContext context, ChangeFeedObserverCloseReason reason)
            {
                Console.WriteLine("Worker closed, {0}", context.PartitionKeyRangeId);
                return Task.CompletedTask;
            }

            public Task ProcessChangesAsync(ChangeFeedObserverContext context, IReadOnlyList<Document> docs)
            {
                DocumentClient newClient = new DocumentClient(new Uri(EndPointUrl), PrimaryKey);

                newClient.CreateDatabaseIfNotExistsAsync(new Database { Id = DBName });

                // create dest collection if it does not exist
                DocumentCollection destCollection = new DocumentCollection();
                destCollection.Id = DestCollName;
                destCollection.PartitionKey.Paths.Add(DestCollPartKey);

                newClient.CreateDocumentCollectionIfNotExistsAsync(
                    UriFactory.CreateDatabaseUri(DBName),
                    destCollection,
                    new RequestOptions { OfferThroughput = 2500 });

                Console.WriteLine("Change feed: total {0} doc(s)", Interlocked.Add(ref s_totalDocs, docs.Count));
                foreach (Document doc in docs)
                {
                    Console.WriteLine(doc.ToString());
                    newClient.UpsertDocumentAsync(
                        UriFactory.CreateDocumentCollectionUri(DBName, DestCollName),
                        doc);
                }
                return Task.CompletedTask;
            }
        }

        private static async Task UpdateDb(string endpoint, string authKey)
        {
            // Use this function to update data in monitored collection in seperate thread
            // Returns all documents in the collection.
            Console.WriteLine("Connect client");
            DocumentClient client = new DocumentClient(new Uri(endpoint), authKey);
            Uri collectionUri = UriFactory.CreateDocumentCollectionUri(DBName, MonitoredCollectionName);

            Console.WriteLine("Connect database");
            try
            {
                Database database = await client.ReadDatabaseAsync(UriFactory.CreateDatabaseUri(DBName));
            }
            catch (Exception e)
            {
                Console.WriteLine("Connect database failed");
                Console.WriteLine(e.Message);
            }

            // Create new documents 
            System.Console.WriteLine("Create JSON document");
            for (int i = 0; i < 10; i++)
            {
                System.Console.WriteLine("Creating document XMS-0004 {0}", i);
                await client.UpsertDocumentAsync(
                UriFactory.CreateDocumentCollectionUri(DBName, MonitoredCollectionName),
                new DeviceReading
                {
                    Id = String.Join("XMS-005-FE24C_", i.ToString()),
                    DeviceId = "XMS-0005",
                    MetricType = "Temperature",
                    MetricValue = 80.00 + (float)i,
                    Unit = "Fahrenheit",
                    ReadingTime = DateTime.UtcNow
                });
                System.Threading.Thread.Sleep(5000);
            }
        }

    }
}
