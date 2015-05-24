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
    /// This sample demonstrates ORDER BY using Azure DocumentDB. By default, this sample project uses sample data under "FakeData", but you can download real JSON
    /// data from Twitter and run queries against it.
    /// </summary>
    /// <remarks>
    /// To run samples using real data from Twitter, get your keys from <a href="https://dev.twitter.com/"/> and set in App.config.
    /// </remarks>
    public sealed class Program
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
        /// Run samples for Order By queries.
        /// </summary>
        /// <returns>a Task object.</returns>
        private async Task RunAsync()
        {
            Database database = await DocumentClientHelper.GetNewDatabaseAsync(this.client, DatabaseId);

            // Create collection with the required indexing policies for Order By
            DocumentCollection collection = await this.CreateCollectionForOrderBy(database);

            // By default, we use a fake dataset that looks like Twitter's status updates to illustrate how 
            // you can query using Order By. If you include your own Twitter Developer credentials in 
            // App.config, you can download real tweets, then query against them.
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

            // Create a collection with indexing policy for just order by against specific paths.
            // With this configuration, 
            //  RunOrderByQuery will throw an exception since RetweetCount is not indexed for Order By.
            //  RunOrderByQueryNestedProperty will run successfully.
            //  RunOrderByQueryWithFilters will run successfully.
            //  RunOrderByQueryAsyncWithPaging will run successfully.
            // You'll also notice that the overall storage for "customIndexingCollection" is a little lower than for "collection".

            DocumentCollection customIndexingCollection = await this.CreateCollectionForOrderBySinglePath(database);
        }

        /// <summary>
        /// Runs a simple Order By query that returns status updates in order of the number of times they were
        /// retweeted. Shows all three forms of querying - LINQ lambdas, LINQ query and SQL query.
        /// </summary>
        /// <param name="collection">The collection to run queries against.</param>
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
            
            // Query as SQL string (you can also use SqlQuerySpec for parameterized SQL).
            var orderBySqlQuery = this.client.CreateDocumentQuery<Status>(collection.SelfLink, 
                @"SELECT * FROM status ORDER BY status.retweet_count DESC");

            foreach (Status status in orderBySqlQuery)
            {
                Console.WriteLine("Id: {0}, Text: {1}, Retweets: {2}", status.StatusId, status.Text, status.RetweetCount);
            }

            Console.WriteLine();
        }

        /// <summary>
        /// Runs an Order By query against a nested property in JSON - in this case we order the status updates in order of
        /// the user's follower count (User is a referenced/nested object within Status).
        /// </summary>
        /// <param name="collection">The collection to run queries against.</param>
        private void RunOrderByQueryNestedProperty(DocumentCollection collection)
        {
            Console.WriteLine("Fetching status messages ordered by the popularity of the user.");

            // Query as LINQ lambdas
            var orderByQuery = this.client.CreateDocumentQuery<Status>(collection.SelfLink).OrderByDescending(s => s.User.FollowersCount);
            foreach (Status status in orderByQuery)
            {
                Console.WriteLine("Id: {0}, Text: {1}, User: {2}, Followers: {3}", 
                    status.StatusId, status.Text, status.User.ScreenName, status.User.FollowersCount);
            }

            // Query as LINQ query
            orderByQuery =
                from s in this.client.CreateDocumentQuery<Status>(collection.SelfLink)
                orderby s.User.FollowersCount descending
                select s;

            foreach (Status status in orderByQuery)
            {
                Console.WriteLine("Id: {0}, Text: {1}, User: {2}, Followers: {3}", 
                    status.StatusId, status.Text, status.User.ScreenName, status.User.FollowersCount);
            }

            // Query as SQL string
            var orderBySqlQuery = this.client.CreateDocumentQuery<Status>(collection.SelfLink, 
               @"SELECT * 
                FROM status 
                ORDER BY status.user.followers_count DESC");

            foreach (Status status in orderBySqlQuery)
            {
                Console.WriteLine("Id: {0}, Text: {1}, User: {2}, Followers: {3}", 
                    status.StatusId, status.Text, status.User.ScreenName, status.User.FollowersCount);
            }

            Console.WriteLine();
        }

        /// <summary>
        /// Runs an Order By query along with filters for a more real-use case. Here the query returns status updates which have been retweeted and favorited over 10
        /// times, and orders them by the order of creation time. Note how CreatedAt is a DateTime property that's serialized/deserialized as an epoch timestamp value
        /// to take advantage of range indexes/order by.
        /// </summary>
        /// <param name="collection">The collection to run queries against.</param>
        private void RunOrderByQueryWithFilters(DocumentCollection collection)
        {
            Console.WriteLine("Fetching tweets retweeted or favorited over 10 times ordered by date.");

            // Query as LINQ lambdas
            var orderByQuery = this.client.CreateDocumentQuery<Status>(collection.SelfLink)
                .Where(s => s.RetweetCount > 10 && s.FavoriteCount > 10)
                .OrderBy(s => s.CreatedAt);

            foreach (Status status in orderByQuery)
            {
                Console.WriteLine("Id: {0}, Text: {1}, Retweets: {2}, Favourites: {3}", 
                    status.StatusId, status.Text, status.RetweetCount, status.FavoriteCount);
            }

            // Query as LINQ query
            orderByQuery =
                from s in this.client.CreateDocumentQuery<Status>(collection.SelfLink)
                where s.RetweetCount > 10 && s.FavoriteCount > 10
                orderby s.CreatedAt
                select s;

            foreach (Status status in orderByQuery)
            {
                Console.WriteLine("Id: {0}, Text: {1}, Retweets: {2}, Favourites: {3}", 
                    status.StatusId, status.Text, status.RetweetCount, status.FavoriteCount);
            }

            // Query as SQL string.
            var orderBySqlQuery = this.client.CreateDocumentQuery<Status>(collection.SelfLink, 
                   @"SELECT * 
                    FROM status 
                    WHERE status.retweet_count > 10 
                    AND status.favorite_count > 10 
                    ORDER BY status.created_at ASC");

            foreach (Status status in orderBySqlQuery)
            {
                Console.WriteLine("Id: {0}, Text: {1}, Retweets: {2}, Favourites: {3}", 
                    status.StatusId, status.Text, status.RetweetCount, status.FavoriteCount);
            }

            Console.WriteLine();
        }

        /// <summary>
        /// Runs an asynchronous Order By query with explicit paging. Here we retrieve only the top 100 results for the query 
        /// by using the IDocumentQuery interface.
        /// </summary>
        /// <param name="collection">The collection to run queries against.</param>
        /// <returns></returns>
        private async Task RunOrderByQueryAsyncWithPaging(DocumentCollection collection)
        {
            Console.WriteLine("Fetching one page of 100 status messages by created date");

            var query = this.client.CreateDocumentQuery<Status>(collection.SelfLink, new FeedOptions { MaxItemCount = 100 })
                .OrderBy(s => s.CreatedAt)
                .AsDocumentQuery();

            foreach (var status in await query.ExecuteNextAsync<Status>())
            {
                Console.WriteLine("Id: {0}, Text: {1}, CreatedAt: {2}", status.StatusId, status.Text, status.CreatedAt);
            }

            Console.WriteLine();
        }

        /// <summary>
        /// Download some JSON data using Twitter's JSON API. Here we download all tweets for the search term "DocumentDB". 
        /// </summary>
        /// <remarks>
        /// To run this, get your keys from <a href="https://dev.twitter.com/"/> and set in App.config.
        /// </remarks>
        private void DownloadDataFromTwitter()
        {
            TwitterClient twitterClient = new TwitterClient(
                ConfigurationManager.AppSettings["TwitterConsumerKey"],
                ConfigurationManager.AppSettings["TwitterConsumerSecret"],
                ConfigurationManager.AppSettings["TwitterAccessToken"],
                ConfigurationManager.AppSettings["TwitterAccessTokenSecret"]);

            long? sinceStatusId = null;

            if (bool.Parse(ConfigurationManager.AppSettings["ShouldGetOnlyNewStatuses"]))
            {
                foreach (string filePath in Directory.GetFiles(DataDirectory, "*.json"))
                {
                    string fileName = Path.GetFileNameWithoutExtension(filePath);
                    long statusId = long.Parse(fileName);

                    if (sinceStatusId == null || sinceStatusId < statusId)
                    {
                        sinceStatusId = statusId;
                    }
                }
            }

            foreach (Status statusUpdate in twitterClient.GetStatuses(ConfigurationManager.AppSettings["TwitterSearchText"], sinceStatusId))
            {
                string filePath = Path.Combine(DataDirectory, string.Format("{0}.json", statusUpdate.StatusId));
                string json = JsonConvert.SerializeObject(statusUpdate);
                if (File.Exists(filePath))
                {
                    File.WriteAllText(filePath, json);
                }
            }
        }

        /// <summary>
        /// Import data into a DocumentDB collection using a "Bulk Import" stored procedure.
        /// </summary>
        /// <param name="collection">The collection to run queries against.</param>
        /// <param name="sourceDirectory">The source directory to read files from.</param>
        /// <returns></returns>
        private async Task ImportData(DocumentCollection collection, string sourceDirectory)
        {
            Console.WriteLine("Importing data ...");
            await DocumentClientHelper.RunBulkImport(this.client, collection, sourceDirectory);
            Console.WriteLine();
        }

        /// <summary>
        /// Create a collection with the required indexing policies for Order By against any numeric property.
        /// </summary>
        /// <param name="database">The database to create the collection in.</param>
        /// <returns>The created collection.</returns>
        private async Task<DocumentCollection> CreateCollectionForOrderBy(Database database)
        {
            IndexingPolicy orderByPolicy = new IndexingPolicy();
            orderByPolicy.IncludedPaths.Add(new IndexingPath { Path = "/", IndexType = IndexType.Range, NumericPrecision = -1 });

            // Here we create as a S1.
            DocumentCollection collection = await DocumentClientHelper.CreateNewCollection(
                this.client,
                database,
                "tweetsCollection",
                new DocumentCollectionInfo { IndexingPolicy = orderByPolicy, OfferType = "S1" });

            return collection;
        }

        /// <summary>
        /// Create a collection with the required indexing policies for Order By against specific properties.
        /// </summary>
        /// <param name="database">The database to create the collection in.</param>
        /// <returns>The created collection.</returns>
        private async Task<DocumentCollection> CreateCollectionForOrderBySinglePath(Database database)
        {
            IndexingPolicy orderByPolicy = new IndexingPolicy();

            // Index the createdAt property for Order By
            orderByPolicy.IncludedPaths.Add(new IndexingPath { Path = "/\"createdAt\"/?", IndexType = IndexType.Range, NumericPrecision = -1 });
            
            // Index all numeric paths under "user" for Order By
            orderByPolicy.IncludedPaths.Add(new IndexingPath { Path = "/\"user\"/*", IndexType = IndexType.Range, NumericPrecision = -1 });

            // Use the default (Hash) for everything else.
            orderByPolicy.IncludedPaths.Add(new IndexingPath { Path = "/" });

            // Here we create as a S1.
            DocumentCollection collection = await DocumentClientHelper.CreateNewCollection(
                this.client,
                database,
                "tweetsCollectionSinglePath",
                new DocumentCollectionInfo { IndexingPolicy = orderByPolicy, OfferType = "S1" });

            return collection;
        }
    }
}