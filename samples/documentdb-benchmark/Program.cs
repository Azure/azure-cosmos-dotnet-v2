namespace DocumentDBBenchmark
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Concurrent;
    using System.Configuration;
    using System.Diagnostics;
    using System.Net;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Newtonsoft.Json;

    /// <summary>
    /// This sample demonstrates how to achieve high performance writes using DocumentDB.
    /// </summary>
    public sealed class Program
    {
        private static readonly string DatabaseName = ConfigurationManager.AppSettings["DatabaseName"];
        private static readonly string DataCollectionName = ConfigurationManager.AppSettings["CollectionName"];
        private static readonly int CollectionThroughput = int.Parse(ConfigurationManager.AppSettings["CollectionThroughput"]);

        private static readonly ConnectionPolicy ConnectionPolicyGateway = new ConnectionPolicy 
        { 
            ConnectionMode = ConnectionMode.Gateway, 
            ConnectionProtocol = Protocol.Tcp, 
            RequestTimeout = new TimeSpan(1, 0, 0), 
            MaxConnectionLimit = 10000, 
            RetryOptions = new RetryOptions 
            { 
                MaxRetryAttemptsOnThrottledRequests = 10,
                MaxRetryWaitTimeInSeconds = 60
            } 
        };

        private static readonly ConnectionPolicy ConnectionPolicyDirect = new ConnectionPolicy
        {
            ConnectionMode = ConnectionMode.Direct,
            ConnectionProtocol = Protocol.Tcp,
            RequestTimeout = new TimeSpan(1, 0, 0),
            MaxConnectionLimit = 10000,
            RetryOptions = new RetryOptions
            {
                MaxRetryAttemptsOnThrottledRequests = 10,
                MaxRetryWaitTimeInSeconds = 60
            }
        };

        private enum TaskType
        {
            Read,
            Write
        };

        private static readonly string InstanceId = Dns.GetHostEntry("LocalHost").HostName + Process.GetCurrentProcess().Id;
        private const int MinThreadPoolSize = 100;

        private int pendingTaskCount;
        private long documentsProcessed;
        private ConcurrentDictionary<int, double> requestUnitsConsumed = new ConcurrentDictionary<int, double>();
        private ConcurrentDictionary<string, int> pkRangeToDocCountMapping = new ConcurrentDictionary<string, int>();

        private DocumentClient client;

        /// <summary>
        /// Initializes a new instance of the <see cref="Program"/> class.
        /// </summary>
        /// <param name="client">The DocumentDB client instance.</param>
        private Program(DocumentClient client)
        {
            this.client = client;
        }

        /// <summary>
        /// Main method for the sample.
        /// </summary>
        /// <param name="args">command line arguments.</param>
        public static void Main(string[] args)
        {
            // ThreadPool.SetMinThreads(MinThreadPoolSize, MinThreadPoolSize);

            string endpoint = ConfigurationManager.AppSettings["EndPointUrl"];
            string authKey = ConfigurationManager.AppSettings["AuthorizationKey"];

            Console.WriteLine("Summary:");
            Console.WriteLine("--------------------------------------------------------------------- ");
            Console.WriteLine("Endpoint: {0}", endpoint);
            Console.WriteLine("Collection : {0}.{1} at {2} request units per second", DatabaseName, DataCollectionName, ConfigurationManager.AppSettings["CollectionThroughput"]);
            Console.WriteLine("Document Template*: {0}", ConfigurationManager.AppSettings["DocumentTemplateFile"]);
            Console.WriteLine("Degree of parallelism*: {0}", ConfigurationManager.AppSettings["DegreeOfParallelism"]);
            Console.WriteLine("--------------------------------------------------------------------- ");
            Console.WriteLine();

            Console.WriteLine("DocumentDBBenchmark starting...");

            try
            {
                using (var client = new DocumentClient(
                    new Uri(endpoint),
                    authKey,
                    ConnectionPolicyDirect))
                {
                    var program = new Program(client);
                    program.RunAsync().Wait();
                    Console.WriteLine("DocumentDBBenchmark completed successfully.");
                }
            }

#if !DEBUG
            catch (Exception e)
            {
                // If the Exception is a DocumentClientException, the "StatusCode" value might help identity 
                // the source of the problem. 
                Console.WriteLine("Samples failed with exception:{0}", e);
            }
#endif

            finally
            {
                Console.WriteLine("Press any key to exit...");
                Console.ReadLine();
            }
        }

        /// <summary>
        /// Run samples for Order By queries.
        /// </summary>
        /// <returns>a Task object.</returns>
        private async Task RunAsync()
        {

            DocumentCollection dataCollection = GetCollectionIfExists(DatabaseName, DataCollectionName);
            int currentCollectionThroughput = 0;

            if (bool.Parse(ConfigurationManager.AppSettings["ShouldCleanupOnStart"]) || dataCollection == null)
            {
                Database database = GetDatabaseIfExists(DatabaseName);
                if (database != null)
                {
                    await client.DeleteDatabaseAsync(database.SelfLink);
                }

                Console.WriteLine("Creating database {0}", DatabaseName);
                database = await client.CreateDatabaseAsync(new Database { Id = DatabaseName });

                Console.WriteLine("Creating collection {0} with {1} RU/s", DataCollectionName, CollectionThroughput);
                dataCollection = await this.CreatePartitionedCollectionAsync(DatabaseName, DataCollectionName);

                currentCollectionThroughput = CollectionThroughput;
            }
            else
            {
                OfferV2 offer = (OfferV2)client.CreateOfferQuery().Where(o => o.ResourceLink == dataCollection.SelfLink).AsEnumerable().FirstOrDefault();
                currentCollectionThroughput = offer.Content.OfferThroughput;

                Console.WriteLine("Found collection {0} with {1} RU/s", DataCollectionName, currentCollectionThroughput);
            }

            int taskCount;
            int degreeOfParallelism = int.Parse(ConfigurationManager.AppSettings["DegreeOfParallelism"]);

            if (degreeOfParallelism == -1)
            {
                // set TaskCount = 10 for each 10k RUs, minimum 1, maximum 250
                taskCount = Math.Max(currentCollectionThroughput / 1000, 1);
                taskCount = Math.Min(taskCount, 250);
            }
            else
            {
                taskCount = degreeOfParallelism;
            }

            Console.WriteLine("Starting Inserts with {0} tasks", taskCount);
            string sampleDocument = File.ReadAllText(ConfigurationManager.AppSettings["DocumentTemplateFile"]);

            pendingTaskCount = taskCount;
            await client.OpenAsync();
            var writeTasks = new List<Task>();
            writeTasks.Add(this.LogOutputStats(TaskType.Write));

            long numberOfDocumentsToInsert = long.Parse(ConfigurationManager.AppSettings["NumberOfDocumentsToInsert"]) / taskCount;
            for (var i = 0; i < taskCount; i++)
            {
                writeTasks.Add(this.InsertDocument(i, dataCollection, sampleDocument, numberOfDocumentsToInsert));
            }

            await Task.WhenAll(writeTasks);

            requestUnitsConsumed.Clear();
            documentsProcessed = 0;

            string[] pkRangesAsArr = await GetPartitionRanges();
            pendingTaskCount = pkRangesAsArr.Count();

            var readTasks = new List<Task>();
            readTasks.Add(this.LogOutputStats(TaskType.Read));

            foreach (var pkRange in pkRangesAsArr)
            {
                readTasks.Add(this.ReadDocument(pkRange));
            }

            await Task.WhenAll(readTasks);

            if (bool.Parse(ConfigurationManager.AppSettings["ShouldCleanupOnFinish"]))
            {
                Console.WriteLine("Deleting Database {0}", DatabaseName);
                await client.DeleteDatabaseAsync(UriFactory.CreateDatabaseUri(DatabaseName));
            }
        }   

        private async Task InsertDocument(int taskId, DocumentCollection collection, string sampleJson, long numberOfDocumentsToInsert)
        {
            requestUnitsConsumed[taskId] = 0;
            string partitionKeyProperty = collection.PartitionKey.Paths[0].Replace("/", "");
            Dictionary<string, object> newDictionary = JsonConvert.DeserializeObject<Dictionary<string, object>>(sampleJson);

            for (var i = 0; i < numberOfDocumentsToInsert; i++)
            {
                string partitionKey = Guid.NewGuid().ToString();
                newDictionary["id"] = Guid.NewGuid().ToString();
                newDictionary[partitionKeyProperty] = partitionKey;

                try
                {
                    ResourceResponse<Document> response = await client.CreateDocumentAsync(
                            UriFactory.CreateDocumentCollectionUri(DatabaseName, DataCollectionName),
                            newDictionary,
                            new RequestOptions() { });

                    string partition = response.SessionToken.Split(':')[0];
                    requestUnitsConsumed[taskId] += response.RequestCharge;
                    Interlocked.Increment(ref this.documentsProcessed);
                }
                catch (Exception e)
                {
                    if (e is DocumentClientException)
                    {
                        DocumentClientException de = (DocumentClientException)e;
                        if (de.StatusCode != HttpStatusCode.Forbidden)
                        {
                            Trace.TraceError("Failed to write {0}. Exception was {1}", JsonConvert.SerializeObject(newDictionary), e);
                        }
                        else
                        {
                            Interlocked.Increment(ref this.documentsProcessed);
                        }
                    }
                }
            }

            Interlocked.Decrement(ref this.pendingTaskCount);
        }

        private async Task ReadDocument(string pkRange)
        {
            pkRangeToDocCountMapping[pkRange] = 0;
            Dictionary<string, string> pkRangeToContTokenMapping = new Dictionary<string, string>();
            Uri documentsLink = UriFactory.CreateDocumentCollectionUri(DatabaseName, DataCollectionName);
            string documentsLinkAsString = string.Concat(documentsLink.ToString(), "/docs/");
            int taskId = Int32.Parse(pkRange);
            requestUnitsConsumed[taskId] = 0;

            pkRangeToContTokenMapping[pkRange] = null;
            int continuation = 1;
            do
            {
                try
                {
                    var response = await client.ReadDocumentFeedAsync(
                    documentsLinkAsString,
                    new FeedOptions
                    {
                        MaxItemCount = 20000,
                        MaxBufferedItemCount = 20000,
                        PartitionKeyRangeId = pkRange,
                        RequestContinuation = pkRangeToContTokenMapping[pkRange]
                    });

                    requestUnitsConsumed[taskId] += response.RequestCharge;
                    Interlocked.Add(ref this.documentsProcessed, response.Count);
                    pkRangeToDocCountMapping[pkRange] += response.Count;
                    pkRangeToContTokenMapping[pkRange] = response.ResponseContinuation;
                    continuation++;
                }
                catch (Exception e)
                {
                    if (e is DocumentClientException)
                    {
                        DocumentClientException de = (DocumentClientException)e;
                        if (de.StatusCode != HttpStatusCode.Forbidden)
                        {
                            Trace.TraceError("Failed to readfeed {0}. Exception was {1}", documentsLink.ToString(), e);
                        }
                        else
                        {
                            Interlocked.Increment(ref this.documentsProcessed);
                        }
                    }
                }

            }
            while (!string.IsNullOrEmpty(pkRangeToContTokenMapping[pkRange]));
            Interlocked.Decrement(ref this.pendingTaskCount);
        }

        private async Task LogOutputStats(TaskType taskType)
        {
            long lastCount = 0;
            double lastRequestUnits = 0;
            double lastSeconds = 0;
            double requestUnits = 0;
            double ruPerSecond = 0;
            double ruPerMonth = 0;

            Stopwatch watch = new Stopwatch();
            watch.Start();

            while (this.pendingTaskCount > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
                double seconds = watch.Elapsed.TotalSeconds;

                requestUnits = 0;
                foreach (int taskId in requestUnitsConsumed.Keys)
                {
                    requestUnits += requestUnitsConsumed[taskId];
                }

                long currentCount = this.documentsProcessed;
                ruPerSecond = (requestUnits / seconds);
                ruPerMonth = ruPerSecond * 86400 * 30;

                Console.WriteLine("Processed {0} docs @ {1} {2}/s, {3} RU/s ({4} Billion max monthly 1KB reads)",
                    currentCount,
                    Math.Round(this.documentsProcessed / seconds),
                    taskType == TaskType.Write ? "writes" : "reads",
                    Math.Round(ruPerSecond),
                    Math.Round(ruPerMonth / (1000 * 1000 * 1000)));

                lastCount = documentsProcessed;
                lastSeconds = seconds;
                lastRequestUnits = requestUnits;
            }

            double totalSeconds = watch.Elapsed.TotalSeconds;
            ruPerSecond = (requestUnits / totalSeconds);
            ruPerMonth = ruPerSecond * 86400 * 30;

            Console.WriteLine();
            Console.WriteLine("Summary:");
            Console.WriteLine("--------------------------------------------------------------------- ");
            Console.WriteLine("Processed {0} docs @ {1} {2}/s, {3} RU/s ({4} Billion max monthly 1KB reads)",
            lastCount,
                Math.Round(this.documentsProcessed / watch.Elapsed.TotalSeconds),
                taskType == TaskType.Write ? "writes" : "reads",
                Math.Round(ruPerSecond),
                Math.Round(ruPerMonth / (1000 * 1000 * 1000)));
            Console.WriteLine("--------------------------------------------------------------------- ");
        }

        /// <summary>
        /// Create a partitioned collection.
        /// </summary>
        /// <returns>The created collection.</returns>
        private async Task<DocumentCollection> CreatePartitionedCollectionAsync(string databaseName, string collectionName)
        {
            DocumentCollection existingCollection = GetCollectionIfExists(databaseName, collectionName);

            DocumentCollection collection = new DocumentCollection();
            collection.Id = collectionName;
            collection.PartitionKey.Paths.Add(ConfigurationManager.AppSettings["CollectionPartitionKey"]);
            //collection.IndexingPolicy.IncludedPaths.Add(new IncludedPath { Path = ConfigurationManager.AppSettings["CollectionPartitionKey"] });
           // collection.IndexingPolicy.ExcludedPaths.Add(new ExcludedPath { Path = "/*" });

            // Show user cost of running this test
            double estimatedCostPerMonth = 0.06 * CollectionThroughput;
            double estimatedCostPerHour = estimatedCostPerMonth / (24 * 30);
            Console.WriteLine("The collection will cost an estimated ${0} per hour (${1} per month)", Math.Round(estimatedCostPerHour, 2), Math.Round(estimatedCostPerMonth, 2));
            Console.WriteLine("Press enter to continue ...");
            Console.ReadLine();

            return await client.CreateDocumentCollectionAsync(
                    UriFactory.CreateDatabaseUri(databaseName), 
                    collection, 
                    new RequestOptions { OfferThroughput = CollectionThroughput });
        }

        /// <summary>
        /// Get the database if it exists, null if it doesn't
        /// </summary>
        /// <returns>The requested database</returns>
        private Database GetDatabaseIfExists(string databaseName)
        {
            return client.CreateDatabaseQuery().Where(d => d.Id == databaseName).AsEnumerable().FirstOrDefault();
        }

        /// <summary>
        /// Get the collection if it exists, null if it doesn't
        /// </summary>
        /// <returns>The requested collection</returns>
        private DocumentCollection GetCollectionIfExists(string databaseName, string collectionName)
        {
            if (GetDatabaseIfExists(databaseName) == null)
            {
                return null;
            }

            return client.CreateDocumentCollectionQuery(UriFactory.CreateDatabaseUri(databaseName))
                .Where(c => c.Id == collectionName).AsEnumerable().FirstOrDefault();
        }

        /// <summary>
        /// Get partition key ranges as a string array
        /// </summary>
        /// <returns></returns>
        private async Task<string[]> GetPartitionRanges()
        {
            List<PartitionKeyRange> partitionKeyRanges = new List<PartitionKeyRange>();

            Uri collectionUri = UriFactory.CreateDocumentCollectionUri(DatabaseName, DataCollectionName);

            string pkRangesResponseContinuation = null;
            do
            {
                FeedResponse<PartitionKeyRange> pkRanges = await client.ReadPartitionKeyRangeFeedAsync(collectionUri, new FeedOptions { RequestContinuation = pkRangesResponseContinuation });
                partitionKeyRanges.AddRange(pkRanges);
            }
            while (pkRangesResponseContinuation != null);

            string[] pkRangesAsArr = partitionKeyRanges.Select(pk => pk.Id).ToArray();
            return pkRangesAsArr;
        }
    }
}
