namespace DocumentDB.Samples.IndexManagement
{
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Documents.Linq;
    using System;
    using System.Configuration;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;

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
            var database = await GetOrCreateDatabaseAsync(databaseId);
            
            //--------------------------------------------------------------------------------------------------------------------
            // The default behavior when creating a DocumentDollection is creating a Hash index for all string & numeric fields. 
            // Hash indexes are compact and offer efficient performance for equality queries.
            // Let's have a look at some of the options available for controlling the indexing behavior of a collection
            //--------------------------------------------------------------------------------------------------------------------

            //Explicitly exclude a document from automatic indexing
            await ExplicitlyExcludeFromIndex(database.SelfLink);

            //Use opt-in indexing of documents with manual indexing policy
            await UseManualIndexing(database.SelfLink);
            
            //Use lazy indexing of documents for bulk import/read heavy collection
            await UseLazyIndexing(database.SelfLink);

            //Use Range indexes for fields that need to queried again a range
            await UseRangeIndexes(database.SelfLink);

            //Exclude certain paths from indexing
            await ExcludePathsFromIndex(database.SelfLink);

            //Force a range-based scan on a Hash index
            await RangeScanOnHashIndex(database.SelfLink);

            //Cleanup
            await client.DeleteDatabaseAsync(database.SelfLink);
        }

        private static async Task ExplicitlyExcludeFromIndex(string databaseLink)
        {
            //There may be scenarios where you want to exclude a specific doc from the index even though all other 
            //documents are being indexed automatically. You can use an index directive to control this when you
            //create a document

            //Create a document collection with the default indexing policy (Automatically index everything)
            DocumentCollection collection = await client.CreateDocumentCollectionAsync(databaseLink, new DocumentCollection
            {
                Id = ConfigurationManager.AppSettings["CollectionId"]
            });

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

        private static async Task UseManualIndexing(string databaseLink)
        {
            //The default behavior for DocumentDB DocumentCollections is to automatically index every document written to it.
            //There are cases where you can want to turn-off automatic indexing on the collection
            //and selectively add only specific documents to the index. 

            var collection = new DocumentCollection
            {
                Id = ConfigurationManager.AppSettings["CollectionId"]
            };
            
            collection.IndexingPolicy.Automatic = false;

            collection = await client.CreateDocumentCollectionAsync(databaseLink, collection);

            //Create a dynamic document, with just a single property for simplicity, 
            //then query for document using that property and we should find nothing
            Document created = await client.CreateDocumentAsync(collection.SelfLink, new { id = "doc1", orderId = "order1" });

            //This should be false as the document won't be in the index
            bool found = client.CreateDocumentQuery(collection.SelfLink, "SELECT * FROM root r WHERE r.orderId='order1'").AsEnumerable().Any();
            
            //If we do a specific Read on the Document we will find it because it is in the collection
            Document doc = await client.ReadDocumentAsync(created.SelfLink);

            //Now create a document, passing in an IndexingDirective saying we want to specifically index this document
            created = await client.CreateDocumentAsync(collection.SelfLink, new { id = "doc2", orderId = "order2" }, new RequestOptions
            {
                IndexingDirective = IndexingDirective.Include
            });
            
            //Query for the document again and this time we should find it because we manually included the document in the index
            found = client.CreateDocumentQuery(collection.SelfLink, "SELECT * FROM root r WHERE r.orderId='order2'").AsEnumerable().Any();

            //Cleanup collection
            await client.DeleteDocumentCollectionAsync(collection.SelfLink);
        }

        private static async Task UseLazyIndexing(string databaseLink)
        {
            //DocumentDB offers synchronous (consistent) and asynchronous (lazy) index updates. 
            //By default, the index is updated synchronously on each insert, replace or delete of a document to the collection. 
            //There are times when you might want to configure certain collections to update their index asynchronously. 
            //Lazy indexing boosts the write performance further and is ideal for bulk ingestion scenarios for primarily read-heavy collections
            //It is important to note that you might get inconsistent reads whilst the writes are in progress,
            //However once the write volume tapers off and the index catches up, then the reads continue as normal

            var collection = new DocumentCollection
            {
                Id = ConfigurationManager.AppSettings["CollectionId"]
            };

            collection.IndexingPolicy.IndexingMode = IndexingMode.Lazy;
            collection = await client.CreateDocumentCollectionAsync(databaseLink, collection);
            
            await client.DeleteDocumentCollectionAsync(collection.SelfLink);
        }
        
        private static async Task UseRangeIndexes(string databaseLink)
        {
            //This example configures a collection to enable efficient comparison queries 
            //by setting the default index type to Range for all numeric values. 

            //The default precision for indexes is 5 bytes which works well for numeric fields like 
            //age, ids, hours, etc. If your number spans a large range of values (millions to billions), 
            //then consider increasing the precision to the maximum of 7 bytes. 
            //This is useful for fields like epoch timestamps, which are commonly used to represent datetimes in JSON
            
            var collection = new DocumentCollection 
            { 
                Id =  ConfigurationManager.AppSettings["CollectionId"]
            };

            collection.IndexingPolicy.IncludedPaths.Add(new IndexingPath
            {
                IndexType = IndexType.Hash,
                Path = "/",
            });

            collection.IndexingPolicy.IncludedPaths.Add(new IndexingPath
            {
                IndexType = IndexType.Range,
                Path = @"/""shippedTimestamp""/?",
                NumericPrecision = 7
            });
                        
            collection = await client.CreateDocumentCollectionAsync(databaseLink, collection);

            await client.CreateDocumentAsync(collection.SelfLink, new 
            { 
                id = "doc1",
                shippedTimestamp = ConvertDateTimeToEpoch(DateTime.UtcNow)
            });
            await client.CreateDocumentAsync(collection.SelfLink, new
            {
                id = "doc2",
                shippedTimestamp = ConvertDateTimeToEpoch(DateTime.UtcNow.AddDays(-7))
            });
            await client.CreateDocumentAsync(collection.SelfLink, new
            {
                id = "doc3",
                shippedTimestamp = ConvertDateTimeToEpoch(DateTime.UtcNow.AddDays(-14))
            });
            await client.CreateDocumentAsync(collection.SelfLink, new
            {
                id = "doc4",
                shippedTimestamp = ConvertDateTimeToEpoch(DateTime.UtcNow.AddDays(-30))
            });
            
            //now with our DateTime converted to an Epoch number and 
            //the IndexPath for createTimestamp set to Range with a higher precision
            //querying for items created in the last 10 days should be effecient
            var docs = client.CreateDocumentQuery(collection.SelfLink, string.Format("SELECT * FROM root r WHERE r.shippedTimestamp >= {0} AND r.shippedTimestamp <= {1}",
                    ConvertDateTimeToEpoch(DateTime.UtcNow.AddDays(-10)),
                    ConvertDateTimeToEpoch(DateTime.UtcNow)
            ));

            foreach (var doc in docs)
            {
                Console.WriteLine(doc);
            }

            //Cleanup
            await client.DeleteDocumentCollectionAsync(collection.SelfLink);
        }

        private static async Task ExcludePathsFromIndex(string databaseLink)
        {
            bool found;
            dynamic dyn = new {
                                id = "doc1",                
                                metaData = "meta",
                                subDoc = new 
                                {
                                    searchable = "searchable",
                                    subSubDoc = new
                                    {
                                        someProperty = "value"
                                    }
                                }
                            };

            //The default behavior is for DocumentDB to index every attribute in every document.
            //There are times when a document contains large amounts of information, in deeply nested structures
            //that you know you will never search on. In extreme cases like this, you can exclude paths from the 
            //index to save on storage cost, improve write performance because there is less work that needs to 
            //happen on writing and also improve read performance because the index is smaller

            var collection = new DocumentCollection
            {
                Id = ConfigurationManager.AppSettings["CollectionId"]
            };

            //special manadatory path of "/" required to denote include entire tree
            collection.IndexingPolicy.IncludedPaths.Add(new IndexingPath {Path = "/" });

            collection.IndexingPolicy.ExcludedPaths.Add("/\"metaData\"/*");
            collection.IndexingPolicy.ExcludedPaths.Add("/\"subDoc\"/\"subSubDoc\"/\"someProperty\"/*");
            collection = await client.CreateDocumentCollectionAsync(databaseLink, collection);

            var created = await client.CreateDocumentAsync(collection.SelfLink, dyn);

            //Querying for a document on either metaData or /subDoc/subSubDoc/someProperty > fail because they were excluded
            try
            {
                client.CreateDocumentQuery(collection.SelfLink, String.Format("SELECT * FROM root r WHERE r.metaData='{0}'",
                    "meta")).AsEnumerable().Any();
            }
            catch (Exception e)
            {
                var baseEx = (DocumentClientException) e.GetBaseException();
                if (baseEx.StatusCode != HttpStatusCode.BadRequest) { throw; }
            }

            try
            {
                found = client.CreateDocumentQuery(collection.SelfLink, String.Format("SELECT * FROM root r WHERE r.subDoc.subSubDoc.someProperty='{0}'", 
                    "value")).AsEnumerable().Any();
            }
            catch (Exception e)
            {
                var baseEx = (DocumentClientException)e.GetBaseException();
                if (baseEx.StatusCode != HttpStatusCode.BadRequest) { throw; }
            }

            //Querying for a document using id, or even subDoc/searchable > succeed because they were not excluded
            found = client.CreateDocumentQuery(collection.SelfLink, String.Format("SELECT * FROM root r WHERE r.id='{0}'", "doc1")).AsEnumerable().Any();
            
            if (!found) throw new ApplicationException("Should've found the document");

            found = client.CreateDocumentQuery(collection.SelfLink, String.Format("SELECT * FROM root r WHERE r.subDoc.searchable='{0}'", 
                "searchable")).AsEnumerable().Any();

            if (!found) throw new ApplicationException("Should've found the document");

            //Cleanup collection
            await client.DeleteDocumentCollectionAsync(collection.SelfLink);
            
            //To exclude subDoc and anything under it add an ExcludePath of "/\"subDoc\"/*"
            collection = new DocumentCollection
            {
                Id = ConfigurationManager.AppSettings["CollectionId"]
            };

            //special manadatory path of "/" required to denote include entire tree
            collection.IndexingPolicy.IncludedPaths.Add(new IndexingPath { Path = "/" });

            collection.IndexingPolicy.ExcludedPaths.Add("/\"subDoc\"/*");
            collection = await client.CreateDocumentCollectionAsync(databaseLink, collection);

            //Query for /subDoc/searchable > fail because we have excluded the whole subDoc, and all its children.
            try
            {
                client.CreateDocumentQuery(collection.SelfLink, String.Format("SELECT * FROM root r WHERE r.subDoc.searchable='{0}'",
                    "searchable")).AsEnumerable().Any();
            }
            catch (Exception e)
            {
                var baseEx = (DocumentClientException)e.GetBaseException();
                if (baseEx.StatusCode != HttpStatusCode.BadRequest) { throw; }
            }
            
            //Cleanup
            await client.DeleteDocumentCollectionAsync(collection.SelfLink);
        }

        private static async Task RangeScanOnHashIndex(string databaseLink)
        {
            //When a range index is not available (i.e. Only hash or no index found on the path), comparisons queries can still 
            //can still be performed as scans using AllowScanInQuery request option using the .NET SDK
            //Warning: This was made an opt-in model by design. Scanning is an expensive operation and doing this 
            //         will have an impact on your RequstUnits and could result in other queries not being throttled.

            var collection = new DocumentCollection
            {
                Id = ConfigurationManager.AppSettings["CollectionId"]
            };

            collection.IndexingPolicy.IncludedPaths.Add(new IndexingPath { Path = "/" });
            collection.IndexingPolicy.ExcludedPaths.Add("/\"length\"/*");
            collection = await client.CreateDocumentCollectionAsync(databaseLink, collection);
            
            var doc1 = await client.CreateDocumentAsync(collection.SelfLink, new { id = "dyn1", length = 10, width = 5, height = 15 });
            var doc2 = await client.CreateDocumentAsync(collection.SelfLink, new { id = "dyn2", length = 7, width = 15});
            var doc3 = await client.CreateDocumentAsync(collection.SelfLink, new { id = "dyn3", length = 2});
            
            //query for length > 5 - fail, this is a range based query on a Hash index only document
            try
            {
                bool found = client.CreateDocumentQuery(collection.SelfLink, "SELECT * FROM root r WHERE r.length > 5").AsEnumerable().Any();
            }
            catch (Exception e)
            {
                var baseEx = (DocumentClientException)e.GetBaseException();
                if (baseEx.StatusCode != HttpStatusCode.BadRequest) { throw; }                
            }
            
            //now add IndexingDirective and repeat query - expect success because now we are explictly allowing scans in a query 
            //using the EnableScanInQuery directive
            client.CreateDocumentQuery(collection.SelfLink, "SELECT * FROM root r WHERE r.length > 5", new FeedOptions
            {
                EnableScanInQuery = true
            }).AsEnumerable().Any();
        }

        private static long ConvertDateTimeToEpoch(DateTime datetime)
        {
            DateTime unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0);
            TimeSpan unixTimeSpan = datetime - unixEpoch;

            long epoch = (long)unixTimeSpan.TotalSeconds;
            return epoch;
        }

        /// <summary>
        /// Get a Database by id, or create a new one if one with the id provided doesn't exist.
        /// </summary>
        /// <param id="id">The id of the Database to search for, or create.</param>
        /// <returns>The matched, or created, Database object</returns>
        private static async Task<Database> GetOrCreateDatabaseAsync(string id)
        {
            Database database = client.CreateDatabaseQuery().Where(c => c.Id == id).ToArray().FirstOrDefault();
            if (database == null)
            {
                database = await client.CreateDatabaseAsync(new Database { Id = id });
            }

            return database;
        }
    }
}
