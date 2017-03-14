using CommandLine;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PartitionStats
{
    class Program
    {
        private DocumentClient Client { get; set; }
        private CommandLineOptions Options { get; set; }

        private Uri DocumentCollectionUri { get; set; }

        static void Main(string[] args)
        {
            try
            {
                var options = new CommandLineOptions();
                if (!CommandLine.Parser.Default.ParseArgumentsStrict(args, options))
                {
                    Console.WriteLine("Invalid arguments");
                    return;
                }

                Program p = new Program { Options = options };
                p.Analyze(options).Wait();
            }
            //catch (Exception e)
            //{
            //    Console.WriteLine("Failed with {0}", e);
            //}
            finally
            {

            }
        }

        private async Task Analyze(CommandLineOptions options)
        {
            using (this.Client = new DocumentClient(
                new Uri(options.DocumentDBEndpoint), 
                options.MasterKey, 
                new ConnectionPolicy { ConnectionMode = ConnectionMode.Gateway, ConnectionProtocol = Protocol.Tcp }))
            {
                DocumentCollectionUri = UriFactory.CreateDocumentCollectionUri(options.Database, options.Collection);

                ResourceResponse<DocumentCollection> collection = await Client.ReadDocumentCollectionAsync(DocumentCollectionUri, new RequestOptions { PopulateQuotaInfo = true });
                List<PartitionKeyRange> partitionKeyRanges = await GetPartitionKeyRanges();

                PrintSummaryStats(collection, partitionKeyRanges);

                if (partitionKeyRanges.Count > 1)
                {
                    await PrintPerPartitionStats(collection, partitionKeyRanges);
                }
            }
        }

        private async Task<List<PartitionKeyRange>> GetPartitionKeyRanges()
        {
            string pkRangesResponseContinuation = null;
            List<PartitionKeyRange> partitionKeyRanges = new List<PartitionKeyRange>();

            do
            {
                FeedResponse<PartitionKeyRange> pkRangesResponse = await Client.ReadPartitionKeyRangeFeedAsync(
                    DocumentCollectionUri,
                    new FeedOptions { RequestContinuation = pkRangesResponseContinuation });

                partitionKeyRanges.AddRange(pkRangesResponse);
                pkRangesResponseContinuation = pkRangesResponse.ResponseContinuation;
            }
            while (pkRangesResponseContinuation != null);

            return partitionKeyRanges;
        }


        private static void PrintSummaryStats(ResourceResponse<DocumentCollection> collection, List<PartitionKeyRange> partitionKeyRanges)
        {
            Console.WriteLine("Summary: ");
            Console.WriteLine("\tpartitions: {0}", partitionKeyRanges.Count);

            string[] keyValuePairs = collection.CurrentResourceQuotaUsage.Split(';');

            foreach (string kvp in keyValuePairs)
            {
                string metricName = kvp.Split('=')[0];
                string metricValue = kvp.Split('=')[1];

                switch (metricName)
                {
                    case "collectionSize":
                        break;
                    case "documentsSize":
                        Console.WriteLine("\t{0}: {1} GB", metricName, Math.Round(int.Parse(metricValue) / (1024 * 1024.0), 3));
                        break;
                    case "documentsCount":
                        Console.WriteLine("\t{0}: {1:n0}", metricName, int.Parse(metricValue));
                        break;
                    case "storedProcedures":
                    case "triggers":
                    case "functions":
                        break;
                    default:
                        Console.WriteLine("\t{0}: {1}", metricName, metricValue);
                        break;
                }
            }

            Console.WriteLine();
        }

        private async Task PrintPerPartitionStats(ResourceResponse<DocumentCollection> collection, List<PartitionKeyRange> partitionKeyRanges)
        {
            Console.WriteLine("Per partition stats: ");

            foreach (PartitionKeyRange pkRange in partitionKeyRanges)
            {
                await PrintPartitionStatsByPartitionKeyRange(collection, pkRange);
            }
        }

        private async Task PrintPartitionStatsByPartitionKeyRange(ResourceResponse<DocumentCollection> collection, PartitionKeyRange pkRange)
        {
            ResourceResponse<Document> perPartitionResponse = await GetPartitionUsageStats(collection, pkRange);
            if (perPartitionResponse == null)
            {
                Console.WriteLine("\tPartition.{0} documentsSize: 0 GB", pkRange.Id);
                return;
            }

            string[] perPartitionKeyValuePairs = perPartitionResponse.CurrentResourceQuotaUsage.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string kvp in perPartitionKeyValuePairs)
            {
                string metricName = kvp.Split('=')[0];
                string metricValue = kvp.Split('=')[1];

                switch (metricName)
                {
                    case "documentsSize":
                        Console.WriteLine("\tPartition.{0} {1}: {2} GB", pkRange.Id, metricName, Math.Round(int.Parse(metricValue) / (1024 * 1024.0), 3));
                        break;
                    default:
                        break;
                }
            }

            if (Options.PartitionId == pkRange.Id)
            {
                await PrintTopPartitionKeysFromSampleData(collection, pkRange);
            }
        }

        private async Task PrintTopPartitionKeysFromSampleData(ResourceResponse<DocumentCollection> collection, PartitionKeyRange pkRange)
        {
            Dictionary<string, int> partitionKeyStats = new Dictionary<string, int>();

            int numDocumentsRead = 0;
            string partitionKeyProperty = GetPartitionKeyPropertyName(collection.Resource);

            while (numDocumentsRead < Options.SampleCount)
            {
                FeedResponse<Document> sampleResults = await Client.CreateDocumentChangeFeedQuery(
                    DocumentCollectionUri,
                    new ChangeFeedOptions { StartFromBeginning = true, PartitionKeyRangeId = pkRange.Id, MaxItemCount = -1 })
                    .ExecuteNextAsync<Document>();

                if (sampleResults.Count == 0)
                {
                    break;
                }

                foreach (Document doc in sampleResults)
                {
                    string pkValue = doc.GetPropertyValue<string>(partitionKeyProperty) ?? "[undefined]";
                    if (partitionKeyStats.ContainsKey(pkValue))
                    {
                        partitionKeyStats[pkValue]++;
                    }
                    else
                    {
                        partitionKeyStats[pkValue] = 1;
                    }
                }

                numDocumentsRead += sampleResults.Count;
            }

            foreach (KeyValuePair<string, int> partitionKey in partitionKeyStats.OrderByDescending(kvp => kvp.Value))
            {
                if (partitionKey.Value >= 1)
                {
                    Console.WriteLine("Key: {0}, Count: {1}", partitionKey.Key, partitionKey.Value);
                }
            }
        }

        private static string GetPartitionKeyPropertyName(DocumentCollection collection)
        {
            return collection.PartitionKey.Paths.First().Replace("/", "");
        }

        private async Task<ResourceResponse<Document>> GetPartitionUsageStats(DocumentCollection collection, PartitionKeyRange pkRange)
        {
            Document sampleDocument = GetRandomDocumentFromPartition(DocumentCollectionUri, pkRange);
            if (sampleDocument == null)
            {
                return null;
            }

            //TODO: support partition key definitions for nested properties, numeric partition keys
            object partitionKeyValue = sampleDocument.GetPropertyValue<string>(GetPartitionKeyPropertyName(collection));
            if (partitionKeyValue == null)
            {
                partitionKeyValue = Undefined.Value;
            }

            ResourceResponse<Document> perPartitionResponse = await Client.ReadDocumentAsync(
                UriFactory.CreateDocumentUri(Options.Database, Options.Collection, sampleDocument.Id),
                new RequestOptions { PartitionKey = new PartitionKey(partitionKeyValue) });

            return perPartitionResponse;
        }

        private Document GetRandomDocumentFromPartition(Uri documentCollectionUri, PartitionKeyRange pkRange)
        {
            FeedResponse<Document> response = Client.CreateDocumentChangeFeedQuery(documentCollectionUri,
                new ChangeFeedOptions { StartFromBeginning = true, PartitionKeyRangeId = pkRange.Id, MaxItemCount = 1 }).ExecuteNextAsync<Document>().Result;

            Document sampleDocument = response.AsEnumerable().FirstOrDefault();

            return sampleDocument;
        }

        class CommandLineOptions
        {
            [Option('a', "account", HelpText = "DocumentDB account endpoint, e.g. https://docdb.documents.azure.com", Required= true)]
            public string DocumentDBEndpoint { get; set; }

            [Option('e', "masterKey", HelpText = "DocumentDB master key", Required = true)]
            public string MasterKey { get; set; }

            [Option('d', "database", HelpText = "DocumentDB database ID", Required = true)]
            public string Database { get; set; }

            [Option('c', "collection", HelpText = "DocumentDB collection ID", Required = true)]
            public string Collection { get; set; }

            [Option('p', "partition", HelpText = "DocumentDB partition ID", Required = false)]
            public string PartitionId { get; set; }

            [Option('s', "sampleCount", HelpText = "Maximum number of samples per partition", Required = false)]
            public int SampleCount { get; set; }

        }

    }
}
