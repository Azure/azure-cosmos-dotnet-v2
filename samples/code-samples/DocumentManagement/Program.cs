namespace DocumentDB.Samples.DocumentManagement
{
    using DocumentDB.Samples.Shared;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Documents.Linq;
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    // ----------------------------------------------------------------------------------------------------------
    // Prerequistes - 
    // 
    // 1. An Azure DocumentDB account - 
    //    https://azure.microsoft.com/en-us/documentation/articles/documentdb-create-account/
    //
    // 2. Microsoft.Azure.DocumentDB NuGet package - 
    //    http://www.nuget.org/packages/Microsoft.Azure.DocumentDB/ 
    // ----------------------------------------------------------------------------------------------------------
    // Sample - demonstrates the basicCRUD operations on a Document resource for Azure DocumentDB
    //
    // 1. Basic CRUD operations on a document using regular POCOs
    // 1.1 - Create a document
    // 1.2 - Read a document by its Id
    // 1.3 - Read all documents in a Collection
    // 1.4 - Query for documents by a property other than Id
    // 1.5 - Replace a document
    // 1.6 - Upsert a document
    // 1.7 - Delete a document
    //
    // 2. Work with dynamic objects
    //-----------------------------------------------------------------------------------------------------------
    // See Also - 
    //
    // DocumentDB.Samples.QueryingDocuments - We only included a VERY basic query here for completeness,
    //                                        For a detailed exploration of how to query for Documents, 
    //                                        inlcuding how to paginate results of queries.
    //
    // DocumentDB.Samples.ServerSideScripts - In these examples we do simple loops to create small numbers
    //                                        of documents. For insert operations where you are creating many
    //                                        documents we recommend using a Stored Procedure and pass batches
    //                                        of new documents to this sproc. Consult this sample for an example
    //                                        of a BulkInsert stored procedure. 
    // ----------------------------------------------------------------------------------------------------------
    
    public class Program
    {
        //Read config
        private static readonly string endpointUrl = ConfigurationManager.AppSettings["EndPointUrl"];
        private static readonly string authorizationKey = ConfigurationManager.AppSettings["AuthorizationKey"];
        private static readonly string databaseId = ConfigurationManager.AppSettings["DatabaseId"];
        private static readonly string collectionId = ConfigurationManager.AppSettings["CollectionId"];
        private static readonly ConnectionPolicy connectionPolicy = new ConnectionPolicy { UserAgentSuffix = " samples-net/3" };

        //Reusable instance of DocumentClient which represents the connection to a DocumentDB endpoint
        private static DocumentClient client;

        public static void Main(string[] args)
        {
            try
            {
                //Get a single instance of Document client and reuse this for all the samples
                //This is the recommended approach for DocumentClient as opposed to new'ing up new instances each time
                using (client = new DocumentClient(new Uri(endpointUrl), authorizationKey))
                {
                    //ensure the database & collection exist before running samples
                    Init();

                    RunDocumentsDemo().Wait();

                    //Clean-up environment
                    Cleanup();
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
                Console.WriteLine("\nEnd of demo, press any key to exit.");
                Console.ReadKey();
            }
        }

        private static async Task RunDocumentsDemo()
        {
            await BasicCRUDAsync();

            await UseDynamics();
        }
        
        /// <summary>
        /// 1. Basic CRUD operations on a document
        /// 1.1 - Create a document
        /// 1.2 - Read a document by its Id
        /// 1.3 - Read all documents in a Collection
        /// 1.4 - Query for documents by a property other than Id
        /// 1.5 - Replace a document
        /// 1.6 - Upsert a document
        /// 1.7 - Delete a document
        /// </summary>
        private static async Task BasicCRUDAsync()
        {
            var orders = new List<object>();

            //Note we now can use a resource's Id property to reference resources.
            //we no longer need to use SelfLink anywhere. 
            var collectionLink = UriFactory.CreateDocumentCollectionUri(databaseId, collectionId);

            //DocumentDB requires an "id" for all documents. 
            //You can either supply your own unique value for id, or
            //let DocumentDB provide it for you. 
            //In these examples we are supplying an id so DocumentDB will just ensure uniqueness of our values

            //******************************************************************************************************************
            // 1.1 - Create a document
            //
            // Create a list of POCO objects, and insert in to a single collection
            // Inspect listOfOrders and you will notice each document within has 
            // a different schemas, SalesOrder and SalesOrder2 to demonstrate how applications change over time
            // And even though the two documents are from different schemas we're going to 
            // create them inside the same collection and work with them seamlessly together
            //
            // You could also create a SalesOrder, a Customer, and any other kind of document in a single collection,
            // as collections are entirely schema free. If you do this, maybe add a "Type" property to each POCO
            // Which would make it easier to retrieve all documents of a specific type. 
            //******************************************************************************************************************

            Console.WriteLine("\n1.1 - Creating documents");

            orders.Add(new SalesOrder
            {
                Id = "POCO1",
                PurchaseOrderNumber = "PO18009186470",
                OrderDate = new DateTime(2005, 7, 1),
                AccountNumber = "10-4020-000510",
                SubTotal = 419.4589m,
                TaxAmt = 12.5838m,
                Freight = 472.3108m,
                TotalDue = 985.018m,
                Items = new[]
                    {
                        new SalesOrderDetail
                        {
                            OrderQty = 1,
                            ProductId = 760,
                            UnitPrice = 419.4589m,
                            LineTotal = 419.4589m
                        }
                    },
            });

            orders.Add(new SalesOrder2
            {
                Id = "POCO2",
                PurchaseOrderNumber = "PO15428132599",
                OrderDate = new DateTime(2005, 7, 1),
                DueDate = new DateTime(2005, 7, 13),
                ShippedDate = new DateTime(2005, 7, 8),
                AccountNumber = "10-4020-000646",
                SubTotal = 6107.0820m,
                TaxAmt = 586.1203m,
                Freight = 183.1626m,
                DiscountAmt = 1982.872m,            // new property added to SalesOrder2
                TotalDue = 4893.3929m,
                Items = new[]
                {
                    new SalesOrderDetail2
                    {
                        OrderQty = 3,
                        ProductCode = "A-123",      // notice how in SalesOrderDetail2 we no longer reference a ProductId
                        ProductName = "Product 1",  // instead we have decided to denormalise our schema and include 
                        CurrencySymbol = "$",       // the Product details relevant to the Order on to the Order directly
                        CurrencyCode = "USD",       // this is a typical refactor that happens in the course of an application
                        UnitPrice = 17.1m,          // that would have previously required schema changes and data migrations etc. 
                        LineTotal = 5.7m
                    }
                }
            });

            //Remember, to do bulk insert of documents it is recommended to use a Stored Procedure
            //and pass a batch of documents to the Stored Prcoedure. This will cut down on the number
            //of roundtrips required. 
            foreach (var order in orders)
            {
                Document created = await client.CreateDocumentAsync(collectionLink, order);

                Console.WriteLine(created);
            }
            
            //******************************************************************************************************************
            // 1.2 - Read a document by its Id
            // If you wish to retrieve a document by its Id, this is the preferred way as opposed to query WHERE id = 'foo'
            // It'll cost you less RUs than a query.
            //
            // NOTE: You don't need to use SelfLinks anymore anywhere, just using the Id itself is good enough
            //******************************************************************************************************************                        
            Console.WriteLine("\n1.2 - Reading Document by Id");
            var response = await client.ReadDocumentAsync(UriFactory.CreateDocumentUri(databaseId, collectionId, "POCO1"));

            Console.WriteLine("Document read by Id {0}", response.Resource);
            Console.WriteLine("RU Charge for reading a Document by Id {0}", response.RequestCharge);

            SalesOrder readOrder = (SalesOrder)(dynamic)response.Resource;

            //******************************************************************************************************************
            // 1.3 - Read ALL documents in a Collection
            //
            // NOTE: Use MaxItemCount on FeedOptions to control how many documents come back per trip to the server
            //       Important to handle throttles whenever you are doing operations such as this that might
            //       result in a 429 (throttled request)
            //******************************************************************************************************************
            Console.WriteLine("\n1.3 - Reading all documents in a collection");

            var docs = await client.ReadDocumentFeedAsync(collectionLink, new FeedOptions { MaxItemCount = 10 });
            foreach (var d in docs)
            {
                Console.WriteLine(d);
            }

            //******************************************************************************************************************
            // 1.4 - Query for documents by a property other than Id
            //
            // NOTE: Operations like AsEnumberable(), ToList(), ToArray() will make as many trips to the database
            //       as required to fetch the entire result-set. Even if you set MaxItemCount to a smaller number. 
            //       MaxItemCount just controls how many results to fetch each trip. 
            //       If you don't want to fetch the full set of results, then use CreateDocumentQuery().AsDocumentQuery()
            //       For more on this please refer to the Queries project.
            //
            // NOTE: If you want to get the RU charge for a query you also need to use CreateDocumentQuery().AsDocumentQuery()
            //       and check the RequestCharge property of this IQueryable response
            //       Once again, refer to the Queries project for more information and examples of this
            //******************************************************************************************************************
            Console.WriteLine("\n1.4 - Querying for a document using its AccountNumber property");

            SalesOrder querySalesOrder = client.CreateDocumentQuery<SalesOrder>(collectionLink)
                                            .Where(so => so.AccountNumber == "10-4020-000510")
                                            .AsEnumerable()
                                            .FirstOrDefault();

            Console.WriteLine(querySalesOrder.AccountNumber);

            //******************************************************************************************************************
            // 1.5 - Replace a document
            //
            // Just update a property on an existing document and issue a Replace command
            //******************************************************************************************************************
            Console.WriteLine("\n1.5 - Replacing a document using its Id");

            querySalesOrder.ShippedDate = DateTime.UtcNow;
            response = await client.ReplaceDocumentAsync(UriFactory.CreateDocumentUri(databaseId, collectionId, querySalesOrder.Id), querySalesOrder);

            var updated = response.Resource;
            Console.WriteLine("Request charge of replace operation: {0}", response.RequestCharge);
            Console.WriteLine("Shipped date of updated document: {0}", updated.GetPropertyValue<DateTime>("ShippedDate"));

            //******************************************************************************************************************
            // 1.6 - Upsert a document
            // 
            // First upsert on a new object. Result is a document is created.
            // Then update a property on the same document, keeping the Id the same
            // Now upsert again on this document and the result is a Replace second time around
            //******************************************************************************************************************
            Console.WriteLine("\n1.6 - Upserting a document");

            var upsertOrder = new SalesOrder
                                {
                                    Id = "POCO3",
                                    PurchaseOrderNumber = "PO18009423320",
                                    OrderDate = new DateTime(2005, 7, 1),
                                    AccountNumber = "10-4020-000510",
                                    SubTotal = 419.4589m,
                                    TaxAmt = 12.5838m,
                                    Freight = 472.3108m,
                                    TotalDue = 985.018m,
                                    Items = new[]
                                        {
                                            new SalesOrderDetail
                                            {
                                                OrderQty = 1,
                                                ProductId = 760,
                                                UnitPrice = 419.4589m,
                                                LineTotal = 419.4589m
                                            }
                                        },
                                };

            response = await client.UpsertDocumentAsync(collectionLink, upsertOrder);
            var upserted = response.Resource;
            
            Console.WriteLine("Request charge of upsert operation: {0}", response.RequestCharge);
            Console.WriteLine("StatusCode of this operation: {0}", response.StatusCode);
            Console.WriteLine("Id of upserted document: {0}", upserted.Id);
            Console.WriteLine("AccountNumber of upserted document: {0}", upserted.GetPropertyValue<string>("AccountNumber"));

            upserted.SetPropertyValue("AccountNumber", "updated account number");
            response = await client.UpsertDocumentAsync(collectionLink, upserted);
            upserted = response.Resource;

            Console.WriteLine("Request charge of upsert operation: {0}", response.RequestCharge);
            Console.WriteLine("StatusCode of this operation: {0}", response.StatusCode);
            Console.WriteLine("Id of upserted document: {0}", upserted.Id);
            Console.WriteLine("AccountNumber of upserted document: {0}", upserted.GetPropertyValue<string>("AccountNumber"));

            //******************************************************************************************************************
            // 1.7 - Delete a document
            //******************************************************************************************************************
            Console.WriteLine("\n1.7 - Upserting a document");

            response = await client.DeleteDocumentAsync(UriFactory.CreateDocumentUri(databaseId, collectionId, "POCO3"));

            Console.WriteLine("Request charge of upsert operation: {0}", response.RequestCharge);
            Console.WriteLine("StatusCode of operation: {0}", response.StatusCode);
        }

        /// <summary>
        /// 2. Basic CRUD operations using dynamics instead of POCOs
        /// </summary>
        private static async Task UseDynamics()
        {
            Console.WriteLine("\n2. Use Dynamics");

            var collectionLink = UriFactory.CreateDocumentCollectionUri(databaseId, collectionId);

            //Create a dynamic object,
            //Notice the case here. The properties will be created in the database
            //with whatever case you give the property
            //If you read this back in to a Document object id will get mapped to Id
            dynamic dynamicOrder = new
            {
                id = "DYN01",
                purchaseOrderNumber = "PO18009186470",
                orderDate = DateTime.UtcNow,
                total = 5.95,
            };

            Console.WriteLine("\nCreating document");

            ResourceResponse<Document> response = await client.CreateDocumentAsync(collectionLink, dynamicOrder);
            var createdDocument = response.Resource;

            Console.WriteLine("Document with id {0} created", createdDocument.Id);
            Console.WriteLine("Request charge of operation: {0}", response.RequestCharge);

            response = await client.ReadDocumentAsync(UriFactory.CreateDocumentUri(databaseId, collectionId, "DYN01"));
            var readDocument = response.Resource;

            //update a dynamic object by just creating a new Property on the fly
            //Document is itself a dynamic object, so you can just use this directly too if you prefer
            readDocument.SetPropertyValue("shippedDate",DateTime.UtcNow);

            //if you wish to work with a dynamic object so you don't need to use SetPropertyValue() or GetPropertyValue<T>()
            //then you can cast to a dynamic
            dynamicOrder = (dynamic)readDocument;
            dynamicOrder.foo = "bar";

            //now do a replace using this dynamic document
            //notice here you don't have to set collectionLink, or documentSelfLink, 
            //everything that is needed is contained in the readDynOrder object 
            //it has a .self Property
            Console.WriteLine("\nReplacing document");

            response = await client.ReplaceDocumentAsync(dynamicOrder);
            var replaced = response.Resource;

            Console.WriteLine("Request charge of operation: {0}", response.RequestCharge);
            Console.WriteLine("shippedDate: {0} and foo: {1} of replaced document", replaced.GetPropertyValue<DateTime>("shippedDate"), replaced.GetPropertyValue<string>("foo"));
        }

        private static void Cleanup()
        {
            client.DeleteDatabaseAsync(UriFactory.CreateDatabaseUri(databaseId)).Wait();
        }

        private static void Init()
        {
            GetOrCreateDatabaseAsync(databaseId).Wait();
            GetOrCreateCollectionAsync(databaseId, collectionId).Wait();
        }

        private static async Task<DocumentCollection> GetOrCreateCollectionAsync(string databaseId, string collectionId)
        {
            var databaseUri = UriFactory.CreateDatabaseUri(databaseId);

            DocumentCollection collection = client.CreateDocumentCollectionQuery(databaseUri)
                .Where(c => c.Id == collectionId)
                .AsEnumerable()
                .FirstOrDefault();

            if (collection == null)
            {
                collection = await client.CreateDocumentCollectionAsync(databaseUri, new DocumentCollection { Id = collectionId });
            }

            return collection;
        }

        private static async Task<Database> GetOrCreateDatabaseAsync(string databaseId)
        {
            var databaseUri = UriFactory.CreateDatabaseUri(databaseId);

            Database database = client.CreateDatabaseQuery()
                .Where(db => db.Id == databaseId)
                .ToArray()
                .FirstOrDefault();

            if (database == null)
            {
                database = await client.CreateDatabaseAsync(new Database { Id = databaseId });
            }

            return database;
        }
    }
}
