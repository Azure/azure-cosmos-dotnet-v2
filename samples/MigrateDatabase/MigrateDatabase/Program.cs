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
                string partitionKeyPath = coll.PartitionKey.Paths[0];

                IndexingPolicy noIndexing = new IndexingPolicy();
                noIndexing.IndexingMode = IndexingMode.None;
                noIndexing.ExcludedPaths.Add(new ExcludedPath { Path = "/*" });

                Console.WriteLine($"\tCreating collection {destinationDatabaseName}/{coll.Id}");
                await this.Client.CreateDocumentCollectionIfNotExistsAsync(
                    UriFactory.CreateDatabaseUri(destinationDatabaseName),
                    new DocumentCollection() {
                        Id = coll.Id,
                        IndexingPolicy = (enableIndexing)? coll.IndexingPolicy: noIndexing,
                        PartitionKey = new PartitionKeyDefinition { Paths = { partitionKeyPath } },
                        DefaultTimeToLive = coll.DefaultTimeToLive
                    }, 
                    new RequestOptions { OfferThroughput = 10000 });

                int totalCount = 0;
                FeedResponse<dynamic> response;

                do
                {
                    response = await this.Client.ReadDocumentFeedAsync(
                        coll.SelfLink, 
                        new FeedOptions { MaxItemCount = -1 });

                    List<Task> insertTasks = new List<Task>();
                    foreach (Document document in response)
                    {
                        insertTasks.Add(this.Client.UpsertDocumentAsync(
                            UriFactory.CreateDocumentCollectionUri(destinationDatabaseName, coll.Id), 
                            document));
                        totalCount++;
                    }

                    await Task.WhenAll(insertTasks);
                }
                while (response.ResponseContinuation != null);

                Console.WriteLine($"\tCopied {totalCount} documents.");
            }
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
