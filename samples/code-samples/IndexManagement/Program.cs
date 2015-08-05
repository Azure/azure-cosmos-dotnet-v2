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

    //----------------------------------------------------------------------------------------------------------
    // DocumentDB DocumentCollections will, by default, create a HASH index for every numeric and string field
    // A HASH index is great for doing point equality & inequality operations but what about other operations? 
    // This sample demonstrates how to control the Index Policy
    //
    // For basic CRUD on DocumentCollection samples please refer to DocumentDB.Samples.CollectionManagement
    //----------------------------------------------------------------------------------------------------------
    
    public class Program
    {
        //an instance of the DocumentDB client that we will create once, and reuse multiple times throughout the sample
        private static DocumentClient client;

        //Read names for your database & collection from configuration file
        private static readonly string databaseId = ConfigurationManager.AppSettings["DatabaseId"];
        private static readonly string CollectionId = ConfigurationManager.AppSettings["CollectionId"];

        //Read the DocumentDB endpointUrl and authorisationKeys from config
        //These values are available from the Azure Management Portal on the DocumentDB Account Blade under "Keys"
        //NB > Keep these values in a safe & secure location. Together they provide Administrative access to your DocDB account
        private static readonly string endpointUrl = ConfigurationManager.AppSettings["EndPointUrl"];
        private static readonly string authorizationKey = ConfigurationManager.AppSettings["AuthorizationKey"];

        public static void Main(string[] args)
        {
            try
            {
                //Get a Document client
                using (client = new DocumentClient(new Uri(endpointUrl), authorizationKey))
                {
                    RunDemoAsync(databaseId, CollectionId).Wait();
                }
            }
            catch (DocumentClientException de)
            {
                Exception baseException = de.GetBaseException();
                Console.WriteLine("{0} error occurred: {1}, Message: {2}", de.StatusCode, de.Message, baseException.Message);
            }
            catch (Exception e)
            {
                Exception baseException = e.GetBaseException();
                Console.WriteLine("Error: {0}, Message: {1}", e.Message, baseException.Message);
            }
            finally
            {
                Console.WriteLine("End of demo, press any key to exit.");
                Console.ReadKey();
            }
        }

        private static async Task RunDemoAsync(string databaseId, string CollectionId)
        {
            //Get, or Create, the Database
            var database = await GetNewDatabaseAsync(databaseId);

            //--------------------------------------------------------------------------------------------------------------------
            // The default behavior when creating a DocumentDollection is creating a Hash index for all string & numeric fields. 
            // Hash indexes are compact and offer efficient performance for equality queries.
            // Let's have a look at some of the options available for controlling the indexing behavior of a collection
            //--------------------------------------------------------------------------------------------------------------------

            // Explicitly exclude a document from automatic indexing
            await ExplicitlyExcludeFromIndex(database);

            // Use opt-in indexing of documents with manual indexing policy
            await UseManualIndexing(database);
            
            // Use lazy indexing of documents for bulk import/read heavy collection
            await UseLazyIndexing(database);

            // By default, DocumentDB supports equality queries on strings and equality, range and order by queries against numbers
            // You can use Range indexing over strings in order to support Order by and range queries against both strings and numbers
            await UseRangeIndexesOnStrings(database);

            // Exclude certain paths from indexing
            await ExcludePathsFromIndex(database);

            // Force a range-based scan on a Hash index
            await RangeScanOnHashIndex(database);

            // make changes to the indexing policy
            await PerformIndexTransformations(database);

            // Cleanup
            await client.DeleteDatabaseAsync(database.SelfLink);
        }

        private static async Task ExplicitlyExcludeFromIndex(Database database)
        {
            //There may be scenarios where you want to exclude a specific doc from the index even though all other 
            //documents are being indexed automatically. You can use an index directive to control this when you
            //create a document

            //Create a document collection with the default indexing policy (Automatically index everything)
            DocumentCollection collection = await DocumentClientHelper.CreateDocumentCollectionWithRetriesAsync(
                client,
                database, 
                new DocumentCollection { Id = ConfigurationManager.AppSettings["CollectionId"]});

            //Create a document and query on it immediately, should work as this Collection is set to automatically index everyting
            Document created = await client.CreateDocumentAsync(collection.SelfLink, new { id = "doc1", orderId = "order1" });

            //Query for document, should find it
            bool found = client.CreateDocumentQuery(collection.SelfLink, "SELECT * FROM root r WHERE r.orderId='order1'").AsEnumerable().Any();
            
            //Now, create a document but this time explictly exclude it from the collection using IndexingDirective
            created = await client.CreateDocumentAsync(collection.SelfLink, new { id = "doc2", orderId = "order2" }, new RequestOptions 
            {
                IndexingDirective = IndexingDirective.Exclude
            });

            //Query for document, should not find it
            found = client.CreateDocumentQuery(collection.SelfLink, "SELECT * FROM root r WHERE r.orderId='order2'").AsEnumerable().Any();

            //Read on document, should still find it
            Document document = await client.ReadDocumentAsync(created.SelfLink);
            
            //Cleanup
            await client.DeleteDocumentCollectionAsync(collection.SelfLink);
        }

        private static async Task UseManualIndexing(Database database)
        {
            Console.WriteLine("Trying manual indexing. Documents are indexed only if the create includes a IndexingDirective.Include");

            //The default behavior for DocumentDB DocumentCollections is to automatically index every document written to it.
            //There are cases where you can want to turn-off automatic indexing on the collection
            //and selectively add only specific documents to the index. 

            var collection = new DocumentCollection { Id = ConfigurationManager.AppSettings["CollectionId"] };
            collection.IndexingPolicy.Automatic = false;

            collection = await DocumentClientHelper.CreateDocumentCollectionWithRetriesAsync(client, database, collection);

            // Create a dynamic document, with just a single property for simplicity, 
            // then query for document using that property and we should find nothing
            Document created = await client.CreateDocumentAsync(collection.SelfLink, new { id = "doc1", orderId = "order1" });

            // This should be false as the document won't be in the index
            bool found = client.CreateDocumentQuery(collection.SelfLink, "SELECT * FROM root r WHERE r.orderId = 'order1'").AsEnumerable().Any();
            
            // If we do a specific Read on the Document we will find it because it is in the collection
            Document doc = await client.ReadDocumentAsync(created.SelfLink);

            // Now create a document, passing in an IndexingDirective saying we want to specifically index this document
            created = await client.CreateDocumentAsync(collection.SelfLink, new { id = "doc2", orderId = "order2" }, new RequestOptions
            {
                IndexingDirective = IndexingDirective.Include
            });
            
            // Query for the document again and this time we should find it because we manually included the document in the index
            found = client.CreateDocumentQuery(collection.SelfLink, "SELECT * FROM root r WHERE r.orderId = 'order2'").AsEnumerable().Any();

            // Cleanup collection
            await client.DeleteDocumentCollectionAsync(collection.SelfLink);

            Console.WriteLine("Done with manual indexing.");
        }

        private static async Task UseLazyIndexing(Database database)
        {
            Console.WriteLine("Trying lazy indexing. Queries will be eventually consistent with this config.");

            // DocumentDB offers synchronous (consistent) and asynchronous (lazy) index updates. 
            // By default, the index is updated synchronously on each insert, replace or delete of a document to the collection. 
            // There are times when you might want to configure certain collections to update their index asynchronously. 
            // Lazy indexing boosts the write performance further and is ideal for bulk ingestion scenarios for primarily read-heavy collections
            // It is important to note that you might get inconsistent reads whilst the writes are in progress,
            // However once the write volume tapers off and the index catches up, then the reads continue as normal

            var collection = new DocumentCollection { Id = ConfigurationManager.AppSettings["CollectionId"] };
            collection.IndexingPolicy.IndexingMode = IndexingMode.Lazy;

            collection = await DocumentClientHelper.CreateDocumentCollectionWithRetriesAsync(client, database, collection);
            
            await client.DeleteDocumentCollectionAsync(collection.SelfLink);

            Console.WriteLine("Done with lazy indexing.");
        }
        
        private static async Task UseRangeIndexesOnStrings(Database database)
        {
            Console.WriteLine("Trying Range index on strings. This enables Order By and range queries on strings.");

            var collection = new DocumentCollection { Id =  ConfigurationManager.AppSettings["CollectionId"] };

            // Overide to Range, Max (-1) for Strings. This allows you to perform string range queries and string order by queries.
            // Note that this might have a higher index storage overhead however if you have long strings or a large number of unique
            // strings. You can be selective of which paths need a Range index through IncludedPath configuration, 
            collection.IndexingPolicy.IncludedPaths.Add(new IncludedPath
            {
                Path = "/*",
                Indexes = new Collection<Index>() 
                {
                    new RangeIndex(DataType.Number) { Precision = -1 },
                    new RangeIndex(DataType.String) { Precision = -1 }
                }
            });

            // Alternatively, you can use the default for /* and just range for the "region".
            // Not creating collection with this in the sample, but this can be used instead.
            IndexingPolicy alternateIndexingPolicy = new IndexingPolicy();
            alternateIndexingPolicy.IncludedPaths.Add(new IncludedPath { Path = "/*" });
            alternateIndexingPolicy.IncludedPaths.Add(new IncludedPath
            {
                Path = "/region/?",
                Indexes = new Collection<Index>() 
                {
                    new RangeIndex(DataType.Number) { Precision = -1 }
                }
            });

            collection = await DocumentClientHelper.CreateDocumentCollectionWithRetriesAsync(client, database, collection);

            await client.CreateDocumentAsync(collection.SelfLink, new { id = "doc1", region = "USA" });
            await client.CreateDocumentAsync(collection.SelfLink, new { id = "doc2", region = "UK" });
            await client.CreateDocumentAsync(collection.SelfLink, new { id = "doc3", region = "Armenia" });
            await client.CreateDocumentAsync(collection.SelfLink, new { id = "doc4", region = "Egypt" });
            
            // Now ordering against region is allowed. You can run the following query
            foreach (var doc in client.CreateDocumentQuery(
                collection.SelfLink, 
                "SELECT * FROM orders o ORDER BY o.region"))
            {
                Console.WriteLine(doc);
            }

            // You can also perform filters against string comparisons like >= 'UK'. Note that you can perform a prefix query
            // i.e., the equivalent of LIKE 'U%' is >='U' AND < 'U\uffff'
            foreach (var doc in client.CreateDocumentQuery(
                collection.SelfLink, 
                "SELECT * FROM orders o WHERE o.region >= 'UK'"))
            {
                Console.WriteLine(doc);
            }

            // Cleanup
            await client.DeleteDocumentCollectionAsync(collection.SelfLink);

            Console.WriteLine("Done with Range indexing on strings.");
        }

        private static async Task ExcludePathsFromIndex(Database database)
        {
            Console.WriteLine("Trying exclusions of paths from indexing to save storage space and improve write throughput.");

            dynamic dyn = new 
            {
                id = "doc1",
                metaData = "meta",
                subDoc = new { searchable = "searchable", subSubDoc = new { someProperty = "value" } }
            };

            // The default behavior is for DocumentDB to index every attribute in every document.
            // There are times when a document contains large amounts of information, in deeply nested structures
            // that you know you will never search on. In extreme cases like this, you can exclude paths from the 
            // index to save on storage cost, improve write performance because there is less work that needs to 
            // happen on writing and also improve read performance because the index is smaller

            var collection = new DocumentCollection { Id = ConfigurationManager.AppSettings["CollectionId"] };

            // Special manadatory path of "/*" required to denote include entire tree
            collection.IndexingPolicy.IncludedPaths.Add(new IncludedPath { Path = "/*" });

            collection.IndexingPolicy.ExcludedPaths.Add(new ExcludedPath { Path = "/metaData/*" });
            collection.IndexingPolicy.ExcludedPaths.Add(new ExcludedPath { Path = "/subDoc/subSubDoc/someProperty/*" });

            collection = await DocumentClientHelper.CreateDocumentCollectionWithRetriesAsync(client, database, collection);

            var created = await client.CreateDocumentAsync(collection.SelfLink, dyn);

            // Querying for a document on either metaData or /subDoc/subSubDoc/someProperty > fail because they were excluded
            ShowQueryIsNotAllowed(collection, "SELECT * FROM root r WHERE r.metaData='meta'");

            ShowQueryIsNotAllowed(collection, "SELECT * FROM root r WHERE r.subDoc.subSubDoc.someProperty='value'");
 
            // Querying for a document using id, or even subDoc/searchable > succeed because they were not excluded
            ShowQueryReturnsResults(collection, "SELECT * FROM root r WHERE r.id='doc1'");

            ShowQueryReturnsResults(collection, "SELECT * FROM root r WHERE r.subDoc.searchable='searchable'");

            // Cleanup collection
            await client.DeleteDocumentCollectionAsync(collection.SelfLink);
            
            // To exclude subDoc and anything under it add an ExcludePath of "/\"subDoc\"/*"
            collection = new DocumentCollection { Id = ConfigurationManager.AppSettings["CollectionId"] };

            // Special manadatory path of "/*" required to denote include entire tree
            collection.IndexingPolicy.IncludedPaths.Add(new IncludedPath { Path = "/*" });
            collection.IndexingPolicy.ExcludedPaths.Add(new ExcludedPath { Path = "/subDoc/*" });

            collection = await DocumentClientHelper.CreateDocumentCollectionWithRetriesAsync(client, database, collection);

            // Query for /subDoc/searchable > fail because we have excluded the whole subDoc, and all its children.
            ShowQueryIsNotAllowed(collection, "SELECT * FROM root r WHERE r.subDoc.searchable='searchable'");

            //Cleanup
            await client.DeleteDocumentCollectionAsync(collection.SelfLink);

            Console.WriteLine("Done with Exclude paths.");
        }

        private static async Task RangeScanOnHashIndex(Database database)
        {
            Console.WriteLine("Trying query with EnableScanInQuery option to run a range query against a hash index");

            // When a range index is not available (i.e. Only hash or no index found on the path), comparisons queries can still 
            // can still be performed as scans using AllowScanInQuery request option using the .NET SDK
            // Warning: This was made an opt-in model by design. Scanning is an expensive operation and doing this 
            //         will have an impact on your RequstUnits and could result in other queries not being throttled.

            var collection = new DocumentCollection { Id = ConfigurationManager.AppSettings["CollectionId"] };
            collection.IndexingPolicy.IncludedPaths.Add(new IncludedPath { Path = "/" });
            collection.IndexingPolicy.ExcludedPaths.Add(new ExcludedPath { Path = "/length/*" });
            
            collection = await DocumentClientHelper.CreateDocumentCollectionWithRetriesAsync(client, database, collection);
            
            var doc1 = await client.CreateDocumentAsync(collection.SelfLink, new { id = "dyn1", length = 10, width = 5, height = 15 });
            var doc2 = await client.CreateDocumentAsync(collection.SelfLink, new { id = "dyn2", length = 7, width = 15});
            var doc3 = await client.CreateDocumentAsync(collection.SelfLink, new { id = "dyn3", length = 2});
            

            // Query for length > 5 - fail, this is a range based query on a Hash index only document
            ShowQueryIsNotAllowed(collection, "SELECT * FROM root r WHERE r.length > 5");
            
            // Now add IndexingDirective and repeat query - expect success because now we are explictly allowing scans in a query 
            // using the EnableScanInQuery directive
            ShowQueryIsAllowed(collection, "SELECT * FROM root r WHERE r.length > 5", new FeedOptions { EnableScanInQuery = true });

            //Cleanup
            await client.DeleteDocumentCollectionAsync(collection.SelfLink);

            Console.WriteLine("Done with Query scan hints.");
        }

        private static async Task PerformIndexTransformations(Database database)
        {
            Console.WriteLine("Performing indexing transformations on an existing collection ...");

            // Create a collection with default indexing policy
            var collection = new DocumentCollection { Id = ConfigurationManager.AppSettings["CollectionId"] };
            collection = await DocumentClientHelper.CreateDocumentCollectionWithRetriesAsync(client, database, collection);

            // Insert some documents
            var doc1 = await client.CreateDocumentAsync(collection.SelfLink, new { id = "dyn1", length = 10, width = 5, height = 15 });
            var doc2 = await client.CreateDocumentAsync(collection.SelfLink, new { id = "dyn2", length = 7, width = 15 });
            var doc3 = await client.CreateDocumentAsync(collection.SelfLink, new { id = "dyn3", length = 2 });
            
            // Switch to lazy indexing and wait till complete.
            Console.WriteLine("Changing from Default to Lazy IndexingMode.");

            collection.IndexingPolicy.IndexingMode = IndexingMode.Lazy;

            await client.ReplaceDocumentCollectionAsync(collection);

            // Check progress and wait for completion - should be instantaneous since we have only a few documents, but larger
            // collections will take time.
            await WaitForIndexTransformationToComplete(collection);

            // Switch to use string range indexing with maximum precision.
            Console.WriteLine("Changing to string range indexing with maximum precision for Order By.");

            collection.IndexingPolicy.IndexingMode = IndexingMode.Consistent;
            collection.IndexingPolicy.IncludedPaths = new Collection<IncludedPath>() {
                new IncludedPath() 
                {
                    Path = "/*",
                    Indexes = new Collection<Index>() {
                        new RangeIndex(DataType.Number) { Precision = -1},
                        new RangeIndex(DataType.String) { Precision = -1 }
                    }
                }
            };

            // Apply change
            await client.ReplaceDocumentCollectionAsync(collection);

            // Wait for completion. Once complete, you can run string range and order by queries.
            await WaitForIndexTransformationToComplete(collection);

            // Now exclude a path from indexing to save on storage space.
            Console.WriteLine("Changing to exclude some paths from indexing.");

            collection.IndexingPolicy.ExcludedPaths.Add(new ExcludedPath()
            {
                Path = "/excluded/*"
            });

            // Apply change
            await client.ReplaceDocumentCollectionAsync(collection);

            // Wait for completion. Once complete, you can run string range and order by queries.
            await WaitForIndexTransformationToComplete(collection);

            Console.WriteLine("Done with indexing policy transformations.");
        }

        /// <summary>
        /// Check the index transformation progress using a ReadDocumentCollectionAsync. 
        /// The service returns a 0-100 value based on the progress.
        /// </summary>
        /// <param name="collection">the collection to monitor progress</param>
        /// <returns>a Task for async completion of the wait operation.</returns>
        private static async Task WaitForIndexTransformationToComplete(DocumentCollection collection)
        {
            long smallWaitTimeMilliseconds = 1000;
            long progress = 0;

            while (progress >= 0 && progress < 100)
            {
                ResourceResponse<DocumentCollection> collectionReadResponse = await client.ReadDocumentCollectionAsync(collection.SelfLink);
                progress = collectionReadResponse.IndexTransformationProgress;

                await Task.Delay(TimeSpan.FromMilliseconds(smallWaitTimeMilliseconds));
            }
        }

        private static void ShowQueryReturnsResults(DocumentCollection collection, string query, FeedOptions options = null)
        {
            int count = ShowQueryIsAllowed(collection, query, options);
            if (count == 0)
            {
                throw new ApplicationException("Expected results");
            }
        }

        private static int ShowQueryIsAllowed(DocumentCollection collection, string query, FeedOptions options = null)
        {
            return client.CreateDocumentQuery(collection.SelfLink, query, options).AsEnumerable().Count();
        }

        private static void ShowQueryIsNotAllowed(DocumentCollection collection, string query, FeedOptions options = null)
        {
            try
            {
                client.CreateDocumentQuery(collection.SelfLink, query, options).AsEnumerable().Any();
            }
            catch (Exception e)
            {
                var baseEx = (DocumentClientException)e.GetBaseException();
                if (baseEx.StatusCode != HttpStatusCode.BadRequest) 
                { 
                    throw; 
                }
            }
        }

        /// <summary>
        /// Get a Database for this id. Delete if it already exists.
        /// </summary>
        /// <param id="id">The id of the Database to create.</param>
        /// <returns>The created Database object</returns>
        private static async Task<Database> GetNewDatabaseAsync(string id)
        {
            Database database = client.CreateDatabaseQuery().Where(c => c.Id == id).ToArray().FirstOrDefault();
            if (database != null)
            {
                await client.DeleteDatabaseAsync(database.SelfLink);
            }

            database = await client.CreateDatabaseAsync(new Database { Id = id });
            return database;
        }
    }
}
