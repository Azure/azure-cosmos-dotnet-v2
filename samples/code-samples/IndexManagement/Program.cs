namespace DocumentDB.Samples.IndexManagement
{
    using System;
    using System.Collections.ObjectModel;
    using System.Configuration;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using DocumentDB.Samples.Shared.Util;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Documents.Linq;
    using System.Collections.Generic;
    using System.Globalization;

    // ----------------------------------------------------------------------------------------------------------
    // Prerequistes - 
    // 
    // 1. An Azure DocumentDB account - 
    //    https://azure.microsoft.com/en-us/documentation/articles/documentdb-create-account/
    //
    // 2. Microsoft.Azure.DocumentDB NuGet package - 
    //    http://www.nuget.org/packages/Microsoft.Azure.DocumentDB/ 
    // ----------------------------------------------------------------------------------------------------------
    // DocumentDB will, by default, create a Range index for every numeric and string field
    // This default index policy is best for;
    // - Equality queries against strings
    // - Range & equality queries on numbers
    //
    // This sample project demonstrates how to customize and alter the index policy on a DocumentCollection.
    //
    // 1. Exclude a document completely from the Index
    // 2. Use lazy (instead of consistent) indexing
    // 3. Exclude specified paths from document index
    // 4. Using range indexes
    // 5. Perform index transform
    // ----------------------------------------------------------------------------------------------------------
    // Note - 
    // 
    // Running this sample will create (and delete) multiple DocumentCollection resources on your account. 
    // Each time a DocumentCollection is created the account will be billed for 1 hour of usage based on
    // the performance tier of that account. 
    // ----------------------------------------------------------------------------------------------------------
    // See Also - 
    //
    // DocumentDB.Samples.CollectionManagement - basic CRUD operations on a DatabaseCollection
    // ----------------------------------------------------------------------------------------------------------

    public class Program
    {
        private static readonly string databaseId = "samples";
        private static readonly string collectionIdPrefix = "index-samples";

        private static readonly string endpointUrl = ConfigurationManager.AppSettings["EndPointUrl"];
        private static readonly string authorizationKey = ConfigurationManager.AppSettings["AuthorizationKey"];
        private static readonly ConnectionPolicy connectionPolicy = new ConnectionPolicy { UserAgentSuffix = " samples-net/3", ConnectionMode = ConnectionMode.Direct, ConnectionProtocol = Protocol.Tcp };

        //Reusable instance of DocumentClient which represents the connection to a DocumentDB endpoint
        private static DocumentClient client;

        struct QueryStats
        {
            public QueryStats(int count, double requestCharge)
            {
                Count = count;
                RequestCharge = requestCharge;
            }

            public readonly int    Count;
            public readonly double RequestCharge;
        };

        public static void Main(string[] args)
        {
            try
            {
                using (client = new DocumentClient(new Uri(endpointUrl), authorizationKey, connectionPolicy))
                {
                    RunIndexDemo().Wait();
                }
            }
            catch (Exception e)
            {
                LogException(e);
            }
            finally
            {
                Console.WriteLine("\nEnd of demo, press any key to exit.");
                Console.ReadKey();
            }
        }

        private static async Task RunIndexDemo()
        {
            // Init
            var database = await client.CreateDatabaseIfNotExistsAsync(new Database { Id = databaseId });

            // 1. Exclude a document from the index
            await ExplicitlyExcludeFromIndex();

            // 2. Use lazy (instead of consistent) indexing
            await UseLazyIndexing();

            // 3. Exclude specified document paths from the index
            await ExcludePathsFromIndex();

            // 4. Use range indexes on strings
            await UsingRangeIndexes();

            // 5. Perform an index transform
            await PerformIndexTransformations();

            // Uncomment to Cleanup
            // await client.DeleteDatabaseAsync(UriFactory.CreateDatabaseUri(databaseId));
        }

        /// <summary>
        /// The default index policy on a DocumentCollection will AUTOMATICALLY index ALL documents added.
        /// There may be scenarios where you want to exclude a specific doc from the index even though all other 
        /// documents are being indexed automatically. 
        /// This method demonstrates how to use an index directive to control this
        /// </summary>
        private static async Task ExplicitlyExcludeFromIndex()
        {            
            var databaseUri = UriFactory.CreateDatabaseUri(databaseId);
            string collectionId = string.Format(CultureInfo.InvariantCulture, "{0}-ExplicitlyExcludeFromIndex", collectionIdPrefix);
            var collectionUri = UriFactory.CreateDocumentCollectionUri(databaseId, collectionId);
            
            Console.WriteLine("\n1. Exclude a document completely from the Index");
            
            // Create a collection with default index policy (i.e. automatic = true)
            DocumentCollection collection = await client.CreateDocumentCollectionAsync(databaseUri, new DocumentCollection { Id = collectionId });
            Console.WriteLine("Collection {0} created with index policy \n{1}", collection.Id, collection.IndexingPolicy);

            // Create a document
            // Then query on it immediately
            // Will work as this Collection is set to automatically index everything
            Document created = await client.CreateDocumentAsync(collectionUri, new { id = "doc1", orderId = "order1" } );
            Console.WriteLine("\nDocument created: \n{0}", created);

            bool found = client.CreateDocumentQuery(collectionUri, "SELECT * FROM root r WHERE r.orderId='order1'").AsEnumerable().Any();
            Console.WriteLine("Document found by query: {0}", found);

            // Now, create a document but this time explictly exclude it from the collection using IndexingDirective
            // Then query for that document
            // Shoud NOT find it, because we excluded it from the index
            // BUT, the document is there and doing a ReadDocument by Id will prove it
            created = await client.CreateDocumentAsync(collectionUri, new { id = "doc2", orderId = "order2" }, new RequestOptions
            {
                IndexingDirective = IndexingDirective.Exclude
            });
            Console.WriteLine("\nDocument created: \n{0}", created);

            found = client.CreateDocumentQuery(collectionUri, "SELECT * FROM root r WHERE r.orderId='order2'").AsEnumerable().Any();
            Console.WriteLine("Document found by query: {0}", found);

            Document document = await client.ReadDocumentAsync(created.SelfLink);
            Console.WriteLine("Document read by id: {0}", document!=null);
            
            // Cleanup
            await client.DeleteDocumentCollectionAsync(collectionUri);
        }

        /// <summary>
        /// DocumentDB offers synchronous (consistent) and asynchronous (lazy) index updates. 
        /// By default, the index is updated synchronously on each insert, replace or delete of a document to the collection. 
        /// There are times when you might want to configure certain collections to update their index asynchronously. 
        /// Lazy indexing boosts write performance and is ideal for bulk ingestion scenarios for primarily read-heavy collections
        /// It is important to note that you might get inconsistent reads whilst the writes are in progress,
        /// However once the write volume tapers off and the index catches up, then reads continue as normal
        /// 
        /// This method demonstrates how to switch IndexMode to Lazy
        /// </summary>
        private static async Task UseLazyIndexing()
        {
            string collectionId = string.Format(CultureInfo.InvariantCulture, "{0}-UseLazyIndexing", collectionIdPrefix);
            var collectionUri = UriFactory.CreateDocumentCollectionUri(databaseId, collectionId);

            Console.WriteLine("\n2. Use lazy (instead of consistent) indexing");

            var collDefinition = new DocumentCollection { Id = collectionId };
            collDefinition.IndexingPolicy.IndexingMode = IndexingMode.Lazy;

            var collection = await DocumentClientHelper.CreateDocumentCollectionWithRetriesAsync(client, databaseId, collDefinition);
            Console.WriteLine("Collection {0} created with index policy \n{1}", collection.Id, collection.IndexingPolicy);

            //it is very difficult to demonstrate lazy indexing as you only notice the difference under sustained heavy write load
            //because we're using an S1 collection in this demo we'd likely get throttled long before we were able to replicate sustained high throughput
            //which would give the index time to catch-up.

            await client.DeleteDocumentCollectionAsync(collectionUri);
        }

        /// <summary>
        /// The default behavior is for DocumentDB to index every attribute in every document automatically.
        /// There are times when a document contains large amounts of information, in deeply nested structures
        /// that you know you will never search on. In extreme cases like this, you can exclude paths from the 
        /// index to save on storage cost, improve write performance and also improve read performance because the index is smaller
        ///
        /// This method demonstrates how to set IndexingPolicy.ExcludedPaths
        /// </summary>
        private static async Task ExcludePathsFromIndex()
        {
            string collectionId = string.Format(CultureInfo.InvariantCulture, "{0}-ExcludePathsFromIndex", collectionIdPrefix);
            var collectionUri = UriFactory.CreateDocumentCollectionUri(databaseId, collectionId);

            Console.WriteLine("\n3. Exclude specified paths from document index");

            var collDefinition = new DocumentCollection { Id = collectionId };
                      
            collDefinition.IndexingPolicy.IncludedPaths.Add(new IncludedPath { Path = "/*" });  // Special manadatory path of "/*" required to denote include entire tree
            collDefinition.IndexingPolicy.ExcludedPaths.Add(new ExcludedPath { Path = "/metaData/*" });   // exclude metaData node, and anything under it
            collDefinition.IndexingPolicy.ExcludedPaths.Add(new ExcludedPath { Path = "/subDoc/nonSearchable/*" });  // exclude ONLY a part of subDoc    
            collDefinition.IndexingPolicy.ExcludedPaths.Add(new ExcludedPath { Path = "/\"excludedNode\"/*" }); // exclude excludedNode node, and anything under it
            
            // The effect of the above IndexingPolicy is that only id, foo, and the subDoc/searchable are indexed

            var collection = await DocumentClientHelper.CreateDocumentCollectionWithRetriesAsync(client, databaseId, collDefinition);
            Console.WriteLine("Collection {0} created with index policy \n{1}", collection.Id, collection.IndexingPolicy);

            int numDocs = 250;
            Console.WriteLine("Creating {0} documents", numDocs);
            for (int docIndex = 0; docIndex < numDocs; docIndex++)
            {
                dynamic dyn = new
                {
                    id = "doc" + docIndex,
                    foo = "bar" + docIndex,
                    metaData = "meta" + docIndex,
                    subDoc = new { searchable = "searchable" + docIndex, nonSearchable = "value" + docIndex },
                    excludedNode = new { subExcluded = "something" + docIndex, subExcludedNode = new { someProperty = "value" + docIndex } }
                };
                Document created = await client.CreateDocumentAsync(collection.SelfLink, dyn);
                Console.WriteLine("Creating document with id {0}", created.Id);
            }

            // Querying for a document on either metaData or /subDoc/subSubDoc/someProperty will be expensive since they do not utilize the index,
            // but instead are served from scan automatically.
            int queryDocId = numDocs / 2;
            QueryStats queryStats = await GetQueryResult(collection, string.Format(CultureInfo.InvariantCulture, "SELECT * FROM root r WHERE r.metaData='meta{0}'", queryDocId));
            Console.WriteLine("Query on metaData returned {0} results", queryStats.Count);
            Console.WriteLine("Query on metaData consumed {0} RUs", queryStats.RequestCharge);

            queryStats = await GetQueryResult(collection, string.Format(CultureInfo.InvariantCulture, "SELECT * FROM root r WHERE r.subDoc.nonSearchable='value{0}'", queryDocId));
            Console.WriteLine("Query on /subDoc/nonSearchable returned {0} results", queryStats.Count);
            Console.WriteLine("Query on /subDoc/nonSearchable consumed {0} RUs", queryStats.RequestCharge);

            queryStats = await GetQueryResult(collection, string.Format(CultureInfo.InvariantCulture, "SELECT * FROM root r WHERE r.excludedNode.subExcludedNode.someProperty='value{0}'", queryDocId));
            Console.WriteLine("Query on /excludedNode/subExcludedNode/someProperty returned {0} results", queryStats.Count);
            Console.WriteLine("Query on /excludedNode/subExcludedNode/someProperty cost {0} RUs", queryStats.RequestCharge);

            // Querying for a document using food, or even subDoc/searchable > consume less RUs because they were not excluded
            queryStats = await GetQueryResult(collection, string.Format(CultureInfo.InvariantCulture, "SELECT * FROM root r WHERE r.foo='bar{0}'", queryDocId));
            Console.WriteLine("Query on /foo returned {0} results", queryStats.Count);
            Console.WriteLine("Query on /foo cost {0} RUs", queryStats.RequestCharge);

            queryStats = await GetQueryResult(collection, string.Format(CultureInfo.InvariantCulture, "SELECT * FROM root r WHERE r.subDoc.searchable='searchable{0}'", queryDocId));
            Console.WriteLine("Query on /subDoc/searchable returned {0} results", queryStats.Count);
            Console.WriteLine("Query on /subDoc/searchable cost {0} RUs", queryStats.RequestCharge);

            //Cleanup
            await client.DeleteDocumentCollectionAsync(collectionUri);
        }

        private static async Task UsingRangeIndexes()
        {
            string collectionId = string.Format(CultureInfo.InvariantCulture, "{0}-UsingRangeIndexes", collectionIdPrefix);
            var collectionUri = UriFactory.CreateDocumentCollectionUri(databaseId, collectionId);

            Console.WriteLine("\n4. Using range indexes");
                        
            var collDefinition = new DocumentCollection { Id =  collectionId };

            // This is how you can specify a range index on strings (and numbers) for all properties. This is the recommended indexing policy for collections.
            IndexingPolicy indexingPolicy = new IndexingPolicy(new RangeIndex(DataType.String) { Precision = -1 });

            // For demo purposes, we are going to exclude all paths in the document, and only 
            // include a range index on strings for the "region". 
            indexingPolicy = new IndexingPolicy();
            indexingPolicy.IncludedPaths.Add(new IncludedPath
            {
                Path = "/region/?",
                Indexes = new Collection<Index>() 
                {
                    new RangeIndex(DataType.String) { Precision = -1 }
                }
            });
            indexingPolicy.ExcludedPaths.Add(new ExcludedPath { Path = "/*" });

            collDefinition.IndexingPolicy = indexingPolicy;

            var collection = await DocumentClientHelper.CreateDocumentCollectionWithRetriesAsync(client, databaseId, collDefinition);
            Console.WriteLine("Collection {0} created with index policy \n{1}", collection.Id, collection.IndexingPolicy);
            
            await client.CreateDocumentAsync(collection.SelfLink, new { id = "doc1", region = "USA" });
            await client.CreateDocumentAsync(collection.SelfLink, new { id = "doc2", region = "UK" });
            await client.CreateDocumentAsync(collection.SelfLink, new { id = "doc3", region = "Armenia" });
            await client.CreateDocumentAsync(collection.SelfLink, new { id = "doc4", region = "Egypt" });

            Console.WriteLine("Documents ordered by region");
            foreach (var doc in client.CreateDocumentQuery(collectionUri, "SELECT * FROM orders o ORDER BY o.region"))
            {
                Console.WriteLine(doc);
            }

            // You can also perform filters against string comparisons like >= 'UK'. Note that you can perform a prefix query, 
            // the equivalent of LIKE 'U%' (is >= 'U' AND < 'U')
            Console.WriteLine("Documents with region begining with U");
            foreach (var doc in client.CreateDocumentQuery(collection.SelfLink, "SELECT * FROM orders o WHERE o.region >= 'U'"))
            {
                Console.WriteLine(doc);
            }
            
            // Cleanup
            await client.DeleteDocumentCollectionAsync(collection.SelfLink);
        }

        private static async Task PerformIndexTransformations()
        {
            string collectionId = string.Format(CultureInfo.InvariantCulture, "{0}-PerformIndexTransformations", collectionIdPrefix);
            var collectionUri = UriFactory.CreateDocumentCollectionUri(databaseId, collectionId);

            Console.WriteLine("\n5. Perform index transform");
            
            // Create a collection with default indexing policy
            var collection = await DocumentClientHelper.CreateDocumentCollectionWithRetriesAsync(client, databaseId, new DocumentCollection { Id = collectionId });
            Console.WriteLine("Collection {0} created with index policy \n{1}", collection.Id, collection.IndexingPolicy);
            
            // Insert some documents
            await client.CreateDocumentAsync(collectionUri, new { id = "dyn1", length = 10, width = 5, height = 15 });
            await client.CreateDocumentAsync(collectionUri, new { id = "dyn2", length = 7, width = 15 });
            await client.CreateDocumentAsync(collectionUri, new { id = "dyn3", length = 2 });

            // Switch to lazy indexing and wait till complete.
            Console.WriteLine("Changing from Default to Lazy IndexingMode.");

            // change the collection's indexing policy,
            // and then do a replace operation on the collection
            collection.IndexingPolicy.IndexingMode = IndexingMode.Lazy;
            await client.ReplaceDocumentCollectionAsync(collection);

            // Check progress and wait for completion - should be instantaneous since we have only a few documents, but larger
            // collections will take time.
            await WaitForIndexTransformationToComplete(collection);

            // Switch to use string & number range indexing with maximum precision.
            Console.WriteLine("Changing to string & number range indexing with maximum precision (needed for Order By).");

            collection.IndexingPolicy = new IndexingPolicy(new RangeIndex(DataType.String) { Precision = -1 });
            collection.IndexingPolicy.IndexingMode = IndexingMode.Consistent;

            // Apply change and wait until it completes
            await client.ReplaceDocumentCollectionAsync(collection);
            await WaitForIndexTransformationToComplete(collection);

            // Now exclude a path from indexing to save on storage space.
            Console.WriteLine("Changing to exclude some paths from indexing.");

            collection.IndexingPolicy.ExcludedPaths.Add(new ExcludedPath()
            {
                Path = "/length/*"
            });

            // Apply change, and wait for completion. Once complete, you can run string range and order by queries.
            await client.ReplaceDocumentCollectionAsync(collection);
            await WaitForIndexTransformationToComplete(collection);

            // Cleanup
            await client.DeleteDocumentCollectionAsync(collection.SelfLink);
        }

        /// <summary>
        /// Check the index transformation progress using a ReadDocumentCollectionAsync. 
        /// The service returns a 0-100 value based on the progress.
        /// </summary>
        /// <param name="collection">the collection to monitor progress</param>
        private static async Task WaitForIndexTransformationToComplete(DocumentCollection collection)
        {
            long smallWaitTimeMilliseconds = 1000;
            long progress = 0;

            while (progress >= 0 && progress < 100)
            {
                ResourceResponse<DocumentCollection> collectionReadResponse = await client.ReadDocumentCollectionAsync(collection.SelfLink);
                progress = collectionReadResponse.IndexTransformationProgress;

                Console.WriteLine("Waiting...");
                await Task.Delay(TimeSpan.FromMilliseconds(smallWaitTimeMilliseconds));
            }

            Console.WriteLine("Done!");
        }

        private static async Task<QueryStats> GetQueryResult(DocumentCollection collection, string query)
        {
            try
            {
                IDocumentQuery<dynamic> documentQuery = client.CreateDocumentQuery(
                    collection.SelfLink,
                    query,
                    new FeedOptions
                    {
                        PopulateQueryMetrics = true,
                        MaxItemCount = -1
                    }).AsDocumentQuery();

                FeedResponse<dynamic> response = await documentQuery.ExecuteNextAsync();
                return new QueryStats(response.Count, response.RequestCharge);
            }
            catch (Exception e)
            {
                LogException(e);
                return new QueryStats(0, 0.0);
            }
        }
        
        /// <summary>
        /// Log exception error message to the console
        /// </summary>
        /// <param name="e">The caught exception.</param>
        private static void LogException(Exception e)
        {
            ConsoleColor color = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;

            Exception baseException = e.GetBaseException();
            if (e is DocumentClientException)
            {
                DocumentClientException de = (DocumentClientException)e;
                Console.WriteLine("{0} error occurred: {1}, Message: {2}", de.StatusCode, de.Message, baseException.Message);
            }
            else
            {
                Console.WriteLine("Error: {0}, Message: {1}", e.Message, baseException.Message);
            }

            Console.ForegroundColor = color;
        }
    }
}
