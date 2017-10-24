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
            using (this.Client = new DocumentClient(
                new Uri(options.DocumentDBEndpoint),
                options.MasterKey,
                new ConnectionPolicy { ConnectionMode = ConnectionMode.Gateway, ConnectionProtocol = Protocol.Tcp }))
            {
                string intermediateDatabaseName = options.Database + "-copy";

                List<DocumentCollection> sourceCollections = this.Client.CreateDocumentCollectionQuery(UriFactory.CreateDatabaseUri(options.Database))
                    .AsEnumerable()
                    .ToList();

                await CloneDatabaseAsync(options.Database, intermediateDatabaseName, sourceCollections, false);

                Console.WriteLine($"Deleting database {options.Database}");
                await Client.DeleteDatabaseAsync(UriFactory.CreateDatabaseUri(options.Database));

                await CloneDatabaseAsync(intermediateDatabaseName, options.Database, sourceCollections, true);

                Console.WriteLine($"Deleting database {intermediateDatabaseName}");
                await Client.DeleteDatabaseAsync(UriFactory.CreateDatabaseUri(intermediateDatabaseName));

                Console.WriteLine($"Complete.");
            }
        }

        private async Task CloneDatabaseAsync(string sourceDatabaseName, string destinationDatabaseName, List<DocumentCollection> collectionInfos, bool enableIndexing = false)
        {
            Console.WriteLine($"Creating database {destinationDatabaseName}");
            await this.Client.CreateDatabaseIfNotExistsAsync(new Database { Id = destinationDatabaseName });

            foreach (DocumentCollection coll in collectionInfos)
            {
                DocumentCollection collectionDefinition = CloneCollectionConfigs(coll, enableIndexing);

                Console.WriteLine($"\tCreating collection {destinationDatabaseName}/{coll.Id}");

                await this.Client.CreateDocumentCollectionIfNotExistsAsync(
                    UriFactory.CreateDatabaseUri(destinationDatabaseName),
                    collectionDefinition,
                    new RequestOptions { OfferThroughput = 10000 });

                DisplayCounts(coll);

                Console.WriteLine($"\tCopying data...");

                int totalCount = 0;
                string continuation = null;

                do
                {
                    FeedResponse<dynamic> response = await this.Client.ReadDocumentFeedAsync(
                        coll.SelfLink,
                        new FeedOptions { MaxItemCount = -1, RequestContinuation = continuation });
                    continuation = response.ResponseContinuation;

                    List<Task> insertTasks = new List<Task>();
                    foreach (Document document in response)
                    {
                        insertTasks.Add(this.Client.UpsertDocumentAsync(
                            UriFactory.CreateDocumentCollectionUri(destinationDatabaseName, coll.Id),
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

        private void DisplayCounts(DocumentCollection coll)
        {
            int count = this.Client.CreateDocumentQuery(
                coll.SelfLink, 
                new FeedOptions { MaxDegreeOfParallelism = -1, MaxItemCount = -1 }).Count();

            Console.WriteLine($"Collection {coll.Id} has {count} docs");
        }

        private static DocumentCollection CloneCollectionConfigs(DocumentCollection coll, bool enableIndexing)
        {
            DocumentCollection collectionDefinition = new DocumentCollection();
            collectionDefinition.Id = coll.Id;

            PartitionKeyDefinition partitionKeyDefinition = coll.PartitionKey;
            if (partitionKeyDefinition.Paths.Count > 0)
            {
                collectionDefinition.PartitionKey = partitionKeyDefinition;
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

            [Option('d', "database", HelpText = "DocumentDB database ID", Required = true)]
            public string Database { get; set; }
        }
    }
}
