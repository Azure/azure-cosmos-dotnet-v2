using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Linq;

namespace Microsoft.Azure.Documents.MigrateDatabase
{
    class Program
    {
        static DocumentClient sourceClient;
        static DocumentClient targetClient;

        private static string sourceEndpointUrl;
        private static string sourceAuthorizationKey;
        private static string targetEndpointUrl;
        private static string targetAuthorizationKey;
        private static string sourceDatabaseName;
        private static string sourceCollectionName;
        private static string targetDatabaseName;
        private static string targetCollectionName;
        //private static Uri sourceCollectionUri;
        //private static Uri targetCollectionUri;
        private static Dictionary<string, string> checkpoints; 
        static void Main(string[] args)
        {   
            sourceEndpointUrl = ConfigurationManager.AppSettings["sourceendpoint"];
            sourceAuthorizationKey = ConfigurationManager.AppSettings["sourceauthKey"];

            targetEndpointUrl = ConfigurationManager.AppSettings["targetendpoint"];
            targetAuthorizationKey = ConfigurationManager.AppSettings["targetauthKey"];

            sourceDatabaseName = ConfigurationManager.AppSettings["sourcedatabase"];
            sourceCollectionName = ConfigurationManager.AppSettings["sourcecollection"];

            targetDatabaseName = ConfigurationManager.AppSettings["targetdatabase"];
            targetCollectionName = ConfigurationManager.AppSettings["targetcollection"];

            ConnectionPolicy connectionPolicy = new ConnectionPolicy
            {
                ConnectionMode = ConnectionMode.Direct,
                ConnectionProtocol = Protocol.Tcp,
                UserAgentSuffix = "changefeedmigration"
            };

            TextWriterTraceListener tr = new TextWriterTraceListener(System.IO.File.CreateText("Output.txt"));
            Debug.Listeners.Add(tr);

            //Debug.Unindent();

            //connectionPolicy.RetryOptions.MaxRetryAttemptsOnThrottledRequests = 0;

            sourceClient = new DocumentClient(new Uri(sourceEndpointUrl), sourceAuthorizationKey, connectionPolicy);

            targetClient = new DocumentClient(new Uri(targetEndpointUrl), targetAuthorizationKey, connectionPolicy);

            
            checkpoints = new Dictionary<string, string>();

            Run().Wait();

            Console.ReadLine();
        }

        static async Task Run()
        {
            List<Uri> sourceCollectionUris = new List<Uri>();

            List<DocumentCollection> sourceCollections = sourceClient.CreateDocumentCollectionQuery(UriFactory.CreateDatabaseUri(sourceDatabaseName))
                .AsEnumerable()
                .ToList();

            await targetClient.CreateDatabaseIfNotExistsAsync(new Database {Id = sourceDatabaseName});

            foreach (DocumentCollection coll in sourceCollections)
            {
                sourceCollectionUris.Add(UriFactory.CreateDocumentCollectionUri(sourceDatabaseName, coll.Id));

                await targetClient.CreateDocumentCollectionIfNotExistsAsync(UriFactory.CreateDatabaseUri(sourceDatabaseName),
                    new DocumentCollection() {Id = coll.Id, PartitionKey = new PartitionKeyDefinition { Paths = { "/id" } } }, new RequestOptions { OfferThroughput = 10000});
            }

            //sourceCollectionUri = UriFactory.CreateDocumentCollectionUri(sourceDatabaseName, sourceCollectionName);
            //targetCollectionUri = UriFactory.CreateDocumentCollectionUri(targetDatabaseName, targetCollectionName);

            //ResourceResponse<DocumentCollection> coll = await client.ReadDocumentCollectionAsync(sourceCollectionUri, new RequestOptions { PopulateQuotaInfo = true });
            //Console.WriteLine(coll.CurrentResourceQuotaUsage);

            long overallElapsedTime = 0;
            long toplevelDocumentCount = 0;

            foreach (Uri sourceCollectionUri in sourceCollectionUris)
            //Uri sourceCollectionUri = sourceCollectionUris[7];
            {
                //var query =
                //    sourceClient.CreateDocumentQuery<long>(sourceCollectionUri, "select value count(1) from c",
                //        new FeedOptions {EnableCrossPartitionQuery = true}).AsEnumerable();

                //List<long> list = query.ToList();

                //Console.WriteLine("Source document count for {0} is {1}", sourceCollectionUri, list[0]);
                //Debug.WriteLine("Source document count for {0} is {1}", sourceCollectionUri, list[0]);

                //long originalDocumentCount = list[0];

                Console.WriteLine("Starting copy for collection {0}", sourceCollectionUri);

                Stopwatch sw = new Stopwatch();
                sw.Start();


                string pkRangesResponseContinuation = null;
                List<PartitionKeyRange> partitionKeyRanges = new List<PartitionKeyRange>();
                FeedResponse<PartitionKeyRange> pkRangesResponse;

                pkRangesResponse = await sourceClient.ReadPartitionKeyRangeFeedAsync(
                    sourceCollectionUri);

                partitionKeyRanges.AddRange(pkRangesResponse);
                pkRangesResponseContinuation = pkRangesResponse.ResponseContinuation;

                while (!String.IsNullOrEmpty(pkRangesResponseContinuation))
                {
                    pkRangesResponse = await sourceClient.ReadPartitionKeyRangeFeedAsync(
                        sourceCollectionUri,
                        new FeedOptions
                        {RequestContinuation = pkRangesResponseContinuation});

                    partitionKeyRanges.AddRange(pkRangesResponse);

                    pkRangesResponseContinuation = pkRangesResponse.ResponseContinuation;
                }


                int waitCount = 0;
                long overallCount = 0;
                double overallCharge = 0;

                while (true)
                {
                    long totalCountInThisPass = 0;
                    double totalChargeInThisPass = 0;

                    foreach (PartitionKeyRange pkRange in partitionKeyRanges)
                        //PartitionKeyRange pkRange = partitionKeyRanges[0];
                    {
                        Console.WriteLine("Processing partition key range {0}", pkRange.Id);
                        Debug.WriteLine("Processing partition key range {0}", pkRange.Id);

                        string continuation = null;
                        checkpoints.TryGetValue(pkRange.Id, out continuation);

                        Tuple<long, double> result = await ProcessPartitionKeyRangeId(sourceCollectionUri, pkRange, continuation);

                        Console.WriteLine("Count of documents processed in RangeId {0} is {1}", pkRange.Id, result.Item1);
                        Debug.WriteLine("Count of documents processed in RangeId {0} is {1}", pkRange.Id, result.Item1);

                        totalCountInThisPass += result.Item1;
                        totalChargeInThisPass += result.Item2;
                    }

                    Console.WriteLine("Overall count of documents is {0}", totalCountInThisPass);
                    Debug.WriteLine("Overall count of documents is {0}", totalCountInThisPass);
                    //Console.WriteLine("Average Request Charge per document is {0}", totalChargeInThisPass / totalCountInThisPass);

                    overallCount += totalCountInThisPass;
                    overallCharge += totalChargeInThisPass;

                    if (totalCountInThisPass == 0)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(0));
                        waitCount++;
                        if (waitCount >= 1)
                        {
                            break; //break from while(true) loop
                        }
                    }
                    else
                    {
                        waitCount = 0;
                    }
                }


                Console.WriteLine("No more changes coming to source collection");
                Debug.WriteLine("No more changes coming to source collection");

                Console.WriteLine("Overall document count : {0}", overallCount);
                Debug.WriteLine("Overall document count : {0}", overallCount);

                //if (overallCount == originalDocumentCount)
                //{
                //    Console.WriteLine("Documents count match");
                //    Debug.WriteLine("Documents count match");
                //}
                //else
                //{
                //    Console.WriteLine("Documents count do not match");
                //    Debug.WriteLine("Documents count do not match");
                //}

                Console.WriteLine("Total time for migration(sec) : " + sw.ElapsedMilliseconds/1000);
                Debug.WriteLine("Total time for migration(sec) : " + sw.ElapsedMilliseconds / 1000);

                overallElapsedTime += sw.ElapsedMilliseconds/1000;
                toplevelDocumentCount += overallCount;
                //Console.WriteLine("Overall request charge : {0}", overallCharge);
            }

            Console.WriteLine("Overall time for migration(sec) : " + overallElapsedTime);
            Debug.WriteLine("Overall time for migration(sec) : " + overallElapsedTime);

            Console.WriteLine("Overall documents transferred : " + toplevelDocumentCount);
            Debug.WriteLine("Overall documents transferred : " + toplevelDocumentCount);




            Debug.Flush();
        }

        static async Task<Tuple<long, double>> ProcessPartitionKeyRangeId(Uri sourceCollectionUri, PartitionKeyRange pkRange, string continuation)
        {
            long totalCount = 0;
            double totalRequestCharge = 0;

            List<Task> tasks = new List<Task>();

            ChangeFeedOptions options = new ChangeFeedOptions
            {
                PartitionKeyRangeId = pkRange.Id,
                StartFromBeginning = true,
                RequestContinuation = continuation,
                //MaxItemCount = -1
                MaxItemCount = 1000
            };

            using (var query = sourceClient.CreateDocumentChangeFeedQuery(sourceCollectionUri, options))
            {
                do
                {
                    var readChangesResponse = await query.ExecuteNextAsync<Document>();

                    totalCount += readChangesResponse.Count;
                    totalRequestCharge += readChangesResponse.RequestCharge;

                    Console.WriteLine("Count of documents in this page : {0}", readChangesResponse.Count);
                    Debug.WriteLine("Count of documents in this page : {0}", readChangesResponse.Count);
                    //Console.WriteLine("Request charge for these documents : {0}", readChangesResponse.RequestCharge);

                    if (readChangesResponse.Count > 0)
                    {
                        foreach (Document changedDocument in readChangesResponse)
                        {
                            tasks.Add(targetClient.UpsertDocumentAsync(sourceCollectionUri, changedDocument));
                        }

                        await Task.WhenAll(tasks);

                        checkpoints[pkRange.Id] = readChangesResponse.ResponseContinuation;
                    }
                } while (query.HasMoreResults);
            }

            return Tuple.Create(totalCount, totalRequestCharge);
        }

    //    static async Task ReadFeed()
    //    {
    //        long totalCount = 0;
    //        double totalRequestCharge = 0;

    //        string tempDatabaseName = sourceDatabaseName + "_final";

    //        //List<Uri> tempCollectionUris = new List<Uri>();
    //        List<String> tempCollectionIds = new List<String>();

    //        List<DocumentCollection> tempCollections = targetClient.CreateDocumentCollectionQuery(UriFactory.CreateDatabaseUri(sourceDatabaseName))
    //            .AsEnumerable()
    //            .ToList();

    //        await targetClient.CreateDatabaseIfNotExistsAsync(new Database { Id = tempDatabaseName });

    //        foreach (DocumentCollection coll in tempCollections)
    //        {
    //            tempCollectionIds.Add(coll.Id);

    //            await targetClient.CreateDocumentCollectionIfNotExistsAsync(UriFactory.CreateDatabaseUri(tempDatabaseName),
    //                new DocumentCollection() { Id = coll.Id, PartitionKey = new PartitionKeyDefinition { Paths = { "/id" } } }, new RequestOptions { OfferThroughput = 10000 });
    //        }

    //        foreach (Uri collectionUri in tempCollectionUris)
    //        {
    //            List<Task> tasks = new List<Task>();

    //            FeedResponse<dynamic> feedResponse = null;
                
    //            do
    //            {
    //                feedResponse = await sourceClient.ReadDocumentFeedAsync(collectionUri, new FeedOptions { MaxItemCount = -1 });

    //                totalCount += feedResponse.Count;
    //                totalRequestCharge += feedResponse.RequestCharge;

    //                Console.WriteLine("Count of documents in this page : {0}", feedResponse.Count);
    //                Debug.WriteLine("Count of documents in this page : {0}", feedResponse.Count);
    //                //Console.WriteLine("Request charge for these documents : {0}", readChangesResponse.RequestCharge);

    //                if (feedResponse.Count > 0)
    //                {
    //                    foreach (Document changedDocument in feedResponse)
    //                    {
    //                        tasks.Add(targetClient.UpsertDocumentAsync(collectionUri, changedDocument));
    //                    }

    //                    await Task.WhenAll(tasks);
    //                }
    //            } while (String.IsNullOrEmpty(feedResponse.ResponseContinuation));

    //    }
    //}
}
