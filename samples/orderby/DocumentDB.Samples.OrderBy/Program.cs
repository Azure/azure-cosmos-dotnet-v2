namespace DocumentDB.Samples.OrderBy
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using DocumentDB.Samples.Twitter;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Documents.Linq;
    using Microsoft.Azure.Documents.Partitioning;
    using Newtonsoft;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// This sample demonstrates ORDER BY using Azure DocumentDB.
    /// </summary>
    public class Program
    {
        private static readonly string EndpointUrl = ConfigurationManager.AppSettings["EndPointUrl"];
        private static readonly string AuthorizationKey = ConfigurationManager.AppSettings["AuthorizationKey"];
        private static readonly string DatabaseId = ConfigurationManager.AppSettings["DatabaseId"];
        private static readonly ConnectionPolicy ConnectionPolicy = new ConnectionPolicy { UserAgentSuffix = " samples-net-orderby/1", ConnectionMode = ConnectionMode.Direct, ConnectionProtocol = Protocol.Tcp };
        private static readonly string DataDirectory = @"..\..\Data";
        private static readonly string FakeDataDirectory = @"..\..\FakeData";

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
            try
            {
                using (var client = new DocumentClient(new Uri(EndpointUrl), AuthorizationKey, ConnectionPolicy))
                {
                    var program = new Program(client);
                    program.RunAsync().Wait();
                    Console.WriteLine("Samples completed successfully.");
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
                Console.WriteLine("End of samples, press any key to exit.");
                Console.ReadKey();
            }
        }

        /// <summary>
        /// Run samples for Database create, read, update and delete.
        /// </summary>
        /// <returns>a Task object.</returns>
        private async Task RunAsync()
        {
            Database database = await DocumentClientHelper.GetNewDatabaseAsync(this.client, DatabaseId);

            DocumentCollection collection = await this.CreateCollectionForOrderBy(database);

            if (bool.Parse(ConfigurationManager.AppSettings["ShouldDownload"]))
            {
                DownloadDataFromTwitter();
                await this.ImportData(collection, DataDirectory);
            }
            else
            {
                await this.ImportData(collection, FakeDataDirectory);
            }

            this.RunOrderByQuery(collection);

            this.RunOrderByQueryNestedProperty(collection);

            this.RunOrderByQueryWithFilters(collection);

            await this.RunOrderByQueryAsyncWithPaging(collection);
        }

        private void RunOrderByQuery(DocumentCollection collection)
        {
            Console.WriteLine("Fetching status messages ordered by the number of retweets.");

            // Query as LINQ lambdas
            var orderByQuery = this.client.CreateDocumentQuery<Status>(collection.SelfLink).OrderByDescending(s => s.RetweetCount);
            foreach (Status status in orderByQuery)
            {
                Console.WriteLine("Id: {0}, Text: {1}, Retweets: {2}", status.StatusId, status.Text, status.RetweetCount);
            }

            // Query as LINQ query
            orderByQuery = 
                from s in this.client.CreateDocumentQuery<Status>(collection.SelfLink)
                orderby s.RetweetCount descending
                select s;

            foreach (Status status in orderByQuery)
            {
                Console.WriteLine("Id: {0}, Text: {1}, Retweets: {2}", status.StatusId, status.Text, status.RetweetCount);
            }
            
            // Query as SQL string
            var orderBySqlQuery = this.client.CreateDocumentQuery<Status>(collection.SelfLink, @"SELECT * FROM status ORDER BY status.retweet_count DESC");

            foreach (Status status in orderBySqlQuery)
            {
                Console.WriteLine("Id: {0}, Text: {1}, Retweets: {2}", status.StatusId, status.Text, status.RetweetCount);
            }

            Console.WriteLine();
        }

        private void RunOrderByQueryNestedProperty(DocumentCollection collection)
        {
            Console.WriteLine("Fetching status messages ordered by the popularity of the user.");

            foreach (Status status in this.client.CreateDocumentQuery<Status>(
                collection.SelfLink,
                @"SELECT * FROM status ORDER BY status.user.followers_count DESC"))
            {
                Console.WriteLine("Id: {0}, Text: {1}, User: {2}, Followers: {3}", status.StatusId, status.Text, status.User.ScreenName, status.User.FollowersCount);
            }

            Console.WriteLine();
        }

        private void RunOrderByQueryWithFilters(DocumentCollection collection)
        {
            Console.WriteLine("Fetching tweets retweeted or favorited over 10 times ordered by date.");
            foreach (Status status in this.client.CreateDocumentQuery<Status>(
                collection.SelfLink,
                @"SELECT * FROM status WHERE status.retweet_count > 10 AND status.favorite_count > 10 ORDER BY status.created_at ASC"))
            {
                Console.WriteLine("Id: {0}, Text: {1}, Retweets: {2}, Favourites: {3}", status.StatusId, status.Text, status.RetweetCount, status.FavoriteCount);
            }

            Console.WriteLine();
        }

        private async Task RunOrderByQueryAsyncWithPaging(DocumentCollection collection)
        {
            Console.WriteLine("Fetching one page of 100 status messages by created date");
            var query = this.client.CreateDocumentQuery<Status>(
                collection.SelfLink,
                @"SELECT * FROM status ORDER BY status.created_at ASC",
                new FeedOptions { MaxItemCount = 100 }).AsDocumentQuery();

            foreach (Status status in await query.ExecuteNextAsync<Status>())
            {
                Console.WriteLine("Id: {0}, Text: {1}, CreatedAt: {2}", status.StatusId, status.Text, status.CreatedAt);
            }

            Console.WriteLine();
        }

        private void DownloadDataFromTwitter()
        {
            TwitterClient twitterClient = new TwitterClient(
                ConfigurationManager.AppSettings["TwitterConsumerKey"],
                ConfigurationManager.AppSettings["TwitterConsumerSecret"],
                ConfigurationManager.AppSettings["TwitterAccessToken"],
                ConfigurationManager.AppSettings["TwitterAccessTokenSecret"]);

            long? sinceStatusId = null;
            foreach (string filePath in Directory.GetFiles(DataDirectory, "*.json"))
            {
                string fileName = Path.GetFileNameWithoutExtension(filePath);
                long statusId = long.Parse(fileName);

                if (sinceStatusId == null || sinceStatusId < statusId)
                {
                    sinceStatusId = statusId;
                }
            }

            foreach (Status statusUpdate in twitterClient.GetStatuses("DocumentDB", sinceStatusId))
            {
                string filePath = Path.Combine(DataDirectory, string.Format("{0}.json", statusUpdate.StatusId));
                string json = JsonConvert.SerializeObject(statusUpdate);
                if (File.Exists(filePath))
                {
                    File.WriteAllText(filePath, json);
                }
            }
        }

        private async Task ImportData(DocumentCollection collection, string sourceDirectory)
        {
            Console.WriteLine("Importing data ...");
            await DocumentClientHelper.RunBulkImport(this.client, collection, sourceDirectory);
            Console.WriteLine();
        }

        private async Task<DocumentCollection> CreateCollectionForOrderBy(Database database)
        {
            IndexingPolicy orderByPolicy = new IndexingPolicy();
            orderByPolicy.IncludedPaths.Add(new IndexingPath { Path = "/", IndexType = IndexType.Range, NumericPrecision = -1 });

            DocumentCollection collection = await DocumentClientHelper.CreateNewCollection(
                this.client,
                database,
                "tweetsCollection",
                new DocumentCollectionInfo { IndexingPolicy = orderByPolicy, OfferType = "S3" });

            return collection;
        }
    }
}