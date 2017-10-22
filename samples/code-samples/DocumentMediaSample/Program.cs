namespace Microsoft.Samples.Documents.Media
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents.Client;
    using System.Configuration;
    using Microsoft.Azure.Documents;
    using System.Net;
    using System.IO;
    using Microsoft.Azure.Documents.Linq;

    class Program
    {
        private static DocumentClient client;
        private static string mediaDatabaseName = "mediaDB";
        private static string mediaCollectionName = "mediaCollection";

        static void Main(string[] args)
        {
            Program.RunScenarioAsync().Wait();   
        }

        private async static Task RunScenarioAsync()
        {
            Console.WriteLine("Clearing target directory ...");

            DirectoryInfo targetDirectory = new DirectoryInfo("target");

            foreach (FileInfo file in targetDirectory.EnumerateFiles())
            {
                file.Delete();
            }

            await Program.InitializeAsync();

            await Program.UploadFilesAsync();

            await Program.DownloadFilesAsync();

            await Program.DeleteAllMediaAsync();            
        }

        private static async Task InitializeAsync()
        {
            Console.WriteLine("Creating DocumentClient ...");

            ConnectionPolicy connectionPolicy = new ConnectionPolicy { ConnectionMode = ConnectionMode.Direct, ConnectionProtocol = Protocol.Tcp };
            connectionPolicy.RetryOptions.MaxRetryAttemptsOnThrottledRequests = 100;
            connectionPolicy.RetryOptions.MaxRetryWaitTimeInSeconds = 60;

            Program.client = new DocumentClient(
                new Uri(ConfigurationManager.AppSettings["endpoint"]),
                ConfigurationManager.AppSettings["accountKey"],
                connectionPolicy);

            await client.OpenAsync();

            Console.WriteLine("Creating MediaDatabase ...");

            try
            {
                await client.CreateDatabaseAsync(new Database { Id = mediaDatabaseName });
            }
            catch (DocumentClientException exception)
            {
                if (exception.StatusCode == HttpStatusCode.Conflict)
                {
                    Console.WriteLine("Media Database already exists");
                }
            }

            DocumentCollection collection = null;
            try
            {
                collection = await client.ReadDocumentCollectionAsync(UriFactory.CreateDocumentCollectionUri(
                    Program.mediaDatabaseName, Program.mediaCollectionName));

                Console.WriteLine("Media collection exists");
            }
            catch (DocumentClientException exception)
            {
                if (exception.StatusCode == HttpStatusCode.NotFound)
                {
                    Console.WriteLine("Media collection doesnt exists");
                }
            }

            if (collection == null)
            {
                Console.WriteLine("Creating MediaCollection ...");

                await client.CreateDocumentCollectionAsync(UriFactory.CreateDatabaseUri(mediaDatabaseName),
                        new DocumentCollection
                        {
                            Id = mediaCollectionName,
                            PartitionKey = new PartitionKeyDefinition { Paths = { "/mediaId" } }
                        },
                        new RequestOptions
                        {
                            OfferThroughput = 250000
                        });
            }
            else
            {
                Console.WriteLine("Collection already exisits, cleaning contents ...");

                await client.ClearAllMediaAsync(mediaDatabaseName, mediaCollectionName);
            }
        }

        private async static Task UploadFilesAsync()
        {
            DirectoryInfo sourceDirectory = new DirectoryInfo("source");

            long totalSize = 0;

            Console.WriteLine("Uploading {0} files to cloud ...", sourceDirectory.GetFiles().Length);

            IList<Task> uploadTasks = new List<Task>();

            long startTickCount = Environment.TickCount;

            foreach (FileInfo file in sourceDirectory.EnumerateFiles())
            {
                uploadTasks.Add(Program.UploadMediaAsync(
                    file.FullName, Program.mediaDatabaseName, Program.mediaCollectionName));

                totalSize += file.Length;
            }

            await Task.WhenAll(uploadTasks);

            long endTickCount = Environment.TickCount;

            Console.WriteLine("Uploaded {0} bytes in {1} msec", totalSize, endTickCount - startTickCount);
        }

        private async static Task DownloadFilesAsync()
        {
            DirectoryInfo sourceDirectory = new DirectoryInfo("source");

            IList<Task> downloadTasks = new List<Task>();

            long startTickCount = Environment.TickCount;

            foreach (FileInfo file in sourceDirectory.EnumerateFiles())
            {
                downloadTasks.Add(Program.DownloadMediaAsync(
                    file.Name, Program.mediaDatabaseName, Program.mediaCollectionName, "target"));
            }

            await Task.WhenAll(downloadTasks);

            long endTickCount = Environment.TickCount;

            long totalSize = 0;
            foreach (FileInfo file in new DirectoryInfo("target").EnumerateFiles())
            {                
                totalSize += file.Length;
            }

            Console.WriteLine("Downloaded {0} bytes in {1} msec", totalSize, endTickCount - startTickCount);
        }

        private async static Task DeleteAllMediaAsync()
        {
            IList<Task> deleteTasks = new List<Task>();

            DirectoryInfo sourceDirectory = new DirectoryInfo("source");

            long startTickCount = Environment.TickCount;

            foreach (FileInfo file in sourceDirectory.EnumerateFiles())
            {
                deleteTasks.Add(Program.DeleteMediaAsync(
                    file.Name, Program.mediaDatabaseName, Program.mediaCollectionName));
            }

            await Task.WhenAll(deleteTasks);

            long endTickCount = Environment.TickCount;

            Console.WriteLine("Deleted in {0} msec", endTickCount - startTickCount);            
        }

        private async static Task UploadMediaAsync(string sourceFileName, string targetDB, string targetCollection)
        {
            using(FileStream stream = new FileStream(sourceFileName, FileMode.Open))
            {
                await Program.client.UploadMediaAsync(
                    targetDB, targetCollection, Path.GetFileName(sourceFileName), stream);
            }
        }

        private async static Task DeleteMediaAsync(string sourceFileName, string targetDB, string targetCollection)
        {
            await Program.client.DeleteMediaAsync(targetDB, targetCollection, sourceFileName);
        }

        private static async Task DownloadMediaAsync(string sourceFileName, string sourceDB, string sourceCollection, string targetDestination)
        {            
            Stream mediaStream = await Program.client.ReadMediaAsync(sourceDB, sourceCollection, sourceFileName);

            using (mediaStream)
            {
                using (FileStream stream = new FileStream(Path.Combine(targetDestination, Path.GetFileName(sourceFileName)), FileMode.CreateNew))
                {
                    await mediaStream.CopyToAsync(stream);
                }
            }
        }
    }
}
