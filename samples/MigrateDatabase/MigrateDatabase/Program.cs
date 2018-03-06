using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommandLine;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents;

namespace MigrateDatabase
{
    class Program
    {
        private DocumentClient Client { get; set; }
        private CommandLineOptions Options { get; set; }

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
                p.RunAsync(options).Wait();
            }
            finally
            {

            }
        }

        private async Task RunAsync(CommandLineOptions options)
        {
            InitializeDocumentClient(options);

            Database database = this.Client.CreateDatabaseQuery()
                .Where(d => d.Id == options.SourceDatabase)
                .AsEnumerable().FirstOrDefault();

            string intermediateDatabaseName = options.SourceDatabase + "-copy";
            Database intermediateDatabase = this.Client.CreateDatabaseQuery()
                .Where(d => d.Id == intermediateDatabaseName)
                .AsEnumerable().FirstOrDefault();

            List<DocumentCollection> sourceCollections;

            if (intermediateDatabase != null)
            {
                Console.WriteLine($"Found intermediate database {intermediateDatabase}");
                sourceCollections = this.Client.CreateDocumentCollectionQuery(UriFactory.CreateDatabaseUri(intermediateDatabaseName))
                    .AsEnumerable()
                    .ToList();
            }
            else
            {
                string sourceDatabaseName = options.SourceDatabase;
                sourceCollections = this.Client.CreateDocumentCollectionQuery(UriFactory.CreateDatabaseUri(sourceDatabaseName))
                    .AsEnumerable()
                    .ToList();

                await CloneDatabaseAsync(sourceDatabaseName, intermediateDatabaseName, sourceCollections, false);

                Console.WriteLine($"Deleting database {sourceDatabaseName}");
                await Client.DeleteDatabaseAsync(UriFactory.CreateDatabaseUri(sourceDatabaseName));
            }

            InitializeDocumentClient(options);

            string destinationDatabaseName = (options.DestinationDatabase != null) ? options.DestinationDatabase : options.SourceDatabase;
            await CloneDatabaseAsync(intermediateDatabaseName, destinationDatabaseName, sourceCollections, true);

            Console.WriteLine($"Deleting database {intermediateDatabaseName}");
            await Client.DeleteDatabaseAsync(UriFactory.CreateDatabaseUri(intermediateDatabaseName));

            Console.WriteLine($"Complete.");
        }

        private void InitializeDocumentClient(CommandLineOptions options)
        {
            this.Client = new DocumentClient(
                             new Uri(options.DocumentDBEndpoint),
                             options.MasterKey,
                             new ConnectionPolicy
                             {
                                 ConnectionMode = ConnectionMode.Direct,
                                 ConnectionProtocol = Protocol.Tcp,
                                 MaxConnectionLimit = 2000,
                                 RetryOptions = new RetryOptions
                                 {
                                     MaxRetryAttemptsOnThrottledRequests = 1000,
                                     MaxRetryWaitTimeInSeconds = 1000
                                 }
                             });
        }

        private async Task SetMaxThroughputAsync(DocumentCollection collection)
        {
            FeedResponse<PartitionKeyRange> pkRanges = await this.Client.ReadPartitionKeyRangeFeedAsync(collection.SelfLink);
            int maxThroughput = pkRanges.Count * 10000;

            Offer offer = this.Client.CreateOfferQuery().Where(o => o.ResourceLink == collection.SelfLink).AsEnumerable().FirstOrDefault();

            OfferV2 newOffer = new OfferV2(offer, maxThroughput);

            await this.Client.ReplaceOfferAsync(newOffer);
        }

        private async Task CloneDatabaseAsync(string sourceDatabaseName, string destinationDatabaseName, List<DocumentCollection> collectionInfos, bool enableIndexing = true)
        {
            Console.WriteLine($"Creating database {destinationDatabaseName}");
            await this.Client.CreateDatabaseIfNotExistsAsync(new Database { Id = destinationDatabaseName });

            foreach (DocumentCollection coll in collectionInfos)
            {
                DocumentCollection collectionDefinition = CloneCollectionConfigs(coll, enableIndexing);

                Console.WriteLine($"\tCreating collection {destinationDatabaseName}/{coll.Id}");

                DocumentCollection newColl = await CreateCollectionWithRetry(destinationDatabaseName, collectionDefinition);

                DisplayCounts(sourceDatabaseName, coll.Id);

                Console.WriteLine($"\tCopying data...");

                int totalCount = 0;
                string continuation = null;

                do
                {
                    FeedResponse<dynamic> response = await this.Client.ReadDocumentFeedAsync(
                        UriFactory.CreateDocumentCollectionUri(sourceDatabaseName, coll.Id),
                        new FeedOptions {
                            MaxItemCount = -1,
                            RequestContinuation = continuation,
                            MaxDegreeOfParallelism = -1
                        });
                    continuation = response.ResponseContinuation;

                    List<Task> insertTasks = new List<Task>();
                    foreach (Document document in response)
                    {
                        insertTasks.Add(this.Client.UpsertDocumentAsync(
                            newColl.SelfLink,
                            document));
                        totalCount++;
                    }

                    await Task.WhenAll(insertTasks);
                    Console.WriteLine($"\tCopied {totalCount} documents...");
                }
                while (continuation != null);

                Console.WriteLine($"\tCopied {totalCount} documents.");
            }
        }

        private async Task<DocumentCollection> CreateCollectionWithRetry(string databaseName, DocumentCollection collectionDefinition)
        {
            DocumentCollection newColl = await this.Client.CreateDocumentCollectionIfNotExistsAsync(
                UriFactory.CreateDatabaseUri(databaseName),
                collectionDefinition,
                new RequestOptions { OfferThroughput = 10000 });

            return newColl;
        }

        private void DisplayCounts(string databaseName, string collectionName)
        {
            int count = this.Client.CreateDocumentQuery(
                UriFactory.CreateDocumentCollectionUri(databaseName, collectionName), 
                new FeedOptions {
                    MaxDegreeOfParallelism = -1,
                    MaxItemCount = -1,
                    EnableCrossPartitionQuery = true }).Count();

            Console.WriteLine($"\tCollection {databaseName + "." + collectionName} has {count} docs");
        }

        private static DocumentCollection CloneCollectionConfigs(DocumentCollection coll, bool enableIndexing)
        {
            DocumentCollection collectionDefinition = new DocumentCollection();
            collectionDefinition.Id = coll.Id;
            if (coll.PartitionKey.Paths.Count > 0)
            {
                foreach (string path in coll.PartitionKey.Paths)
                {
                    collectionDefinition.PartitionKey.Paths.Add(path);
                }
            }

            if (enableIndexing)
            {
                collectionDefinition.IndexingPolicy = coll.IndexingPolicy;
            }
            else
            {
                IndexingPolicy noIndexing = new IndexingPolicy();
                noIndexing.IndexingMode = IndexingMode.None;
                noIndexing.Automatic = false;

                collectionDefinition.IndexingPolicy = noIndexing;
            }

            collectionDefinition.DefaultTimeToLive = coll.DefaultTimeToLive;
            return collectionDefinition;
        }

        class CommandLineOptions
        {
            [Option('a', "account", HelpText = "DocumentDB account endpoint, e.g. https://docdb.documents.azure.com", Required = true)]
            public string DocumentDBEndpoint { get; set; }

            [Option('e', "masterKey", HelpText = "DocumentDB master key", Required = true)]
            public string MasterKey { get; set; }

            [Option('s', "sourceDatabase", HelpText = "source DocumentDB database ID", Required = true)]
            public string SourceDatabase { get; set; }

            [Option('d', "destDatabase", HelpText = "dest DocumentDB database ID", Required = false)]
            public string DestinationDatabase { get; set; }
        }
    }
}
