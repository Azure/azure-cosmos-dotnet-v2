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
    using System.Net;
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
    //
    // 3. Using ETags to control execution
    // 3.1 - Use ETag with ReplaceDocument for optimistic concurrency
    // 3.2 - Use ETag with ReadDocument to only return a result if the ETag of the request does not match
    //-----------------------------------------------------------------------------------------------------------
    // See Also - 
    //
    // DocumentDB.Samples.Queries -           We only included a VERY basic query here for completeness,
    //                                        For a detailed exploration of how to query for Documents, 
    //                                        including how to paginate results of queries.
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
        private static readonly string databaseName = ConfigurationManager.AppSettings["DatabaseId"];
        private static readonly string collectionName = ConfigurationManager.AppSettings["CollectionId"];
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
                    Initialize().Wait();

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

        /// <summary>
        /// Run basic document access methods as a console app demo
        /// </summary>
        /// <returns></returns>
        private static async Task RunDocumentsDemo()
        {
            await RunBasicOperationsOnStronglyTypedObjects();

            await RunBasicOperationsOnDynamicObjects();

            await UseETags();
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
        private static async Task RunBasicOperationsOnStronglyTypedObjects()
        {
            await CreateDocumentsAsync();

            await ReadDocumentAsync();

            SalesOrder result = QueryDocuments();

            await ReplaceDocumentAsync(result);

            await UpsertDocumentAsync();

            await DeleteDocumentAsync();
        }

        private static async Task CreateDocumentsAsync()
        {
            Uri collectionLink = UriFactory.CreateDocumentCollectionUri(databaseName, collectionName);

            Console.WriteLine("\n1.1 - Creating documents");

            // Create a SalesOrder object. This object has nested properties and various types including numbers, DateTimes and strings.
            // This can be saved as JSON as is without converting into rows/columns.
            SalesOrder salesOrder = GetSalesOrderSample("SalesOrder1");
            await client.CreateDocumentAsync(collectionLink, salesOrder);

            // As your app evolves, let's say your object has a new schema. You can insert SalesOrderV2 objects without any 
            // changes to the database tier.
            SalesOrder2 newSalesOrder = GetSalesOrderV2Sample("SalesOrder2");
            await client.CreateDocumentAsync(collectionLink, newSalesOrder);
        }

        private static async Task ReadDocumentAsync()
        {
            Console.WriteLine("\n1.2 - Reading Document by Id");

            // Note that Reads require a partition key to be spcified. This can be skipped if your collection is not
            // partitioned i.e. does not have a partition key definition during creation.
            var response = await client.ReadDocumentAsync(
                UriFactory.CreateDocumentUri(databaseName, collectionName, "SalesOrder1"), 
                new RequestOptions { PartitionKey = new PartitionKey("Account1") });

            // You can measure the throughput consumed by any operation by inspecting the RequestCharge property
            Console.WriteLine("Document read by Id {0}", response.Resource);
            Console.WriteLine("Request Units Charge for reading a Document by Id {0}", response.RequestCharge);

            SalesOrder readOrder = (SalesOrder)(dynamic)response.Resource;

            //******************************************************************************************************************
            // 1.3 - Read ALL documents in a Collection
            //
            // NOTE: Use MaxItemCount on FeedOptions to control how many documents come back per trip to the server
            //       Important to handle throttles whenever you are doing operations such as this that might
            //       result in a 429 (throttled request)
            //******************************************************************************************************************
            Console.WriteLine("\n1.3 - Reading all documents in a collection");

            foreach (Document document in await client.ReadDocumentFeedAsync(
                UriFactory.CreateDocumentCollectionUri(databaseName, collectionName), 
                new FeedOptions { MaxItemCount = 10 }))
            {
                Console.WriteLine(document);
            }
        }
        private static SalesOrder QueryDocuments()
        {
            //******************************************************************************************************************
            // 1.4 - Query for documents by a property other than Id
            //
            // NOTE: Operations like AsEnumerable(), ToList(), ToArray() will make as many trips to the database
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

            SalesOrder querySalesOrder = client.CreateDocumentQuery<SalesOrder>(
                UriFactory.CreateDocumentCollectionUri(databaseName, collectionName))
                .Where(so => so.AccountNumber == "Account1")
                .AsEnumerable()
                .First();

            Console.WriteLine(querySalesOrder.AccountNumber);

            return querySalesOrder;
        }
        private static async Task ReplaceDocumentAsync(SalesOrder order)
        {
            //******************************************************************************************************************
            // 1.5 - Replace a document
            //
            // Just update a property on an existing document and issue a Replace command
            //******************************************************************************************************************
            Console.WriteLine("\n1.5 - Replacing a document using its Id");

            order.ShippedDate = DateTime.UtcNow;
            ResourceResponse<Document> response = await client.ReplaceDocumentAsync(
                UriFactory.CreateDocumentUri(databaseName, collectionName, order.Id), 
                order);

            var updated = response.Resource;
            Console.WriteLine("Request charge of replace operation: {0}", response.RequestCharge);
            Console.WriteLine("Shipped date of updated document: {0}", updated.GetPropertyValue<DateTime>("ShippedDate"));
        }
        private static async Task UpsertDocumentAsync()
        {
            Console.WriteLine("\n1.6 - Upserting a document");

            var upsertOrder = GetSalesOrderSample("SalesOrder3");
            ResourceResponse<Document> response = await client.UpsertDocumentAsync(UriFactory.CreateDocumentCollectionUri(databaseName, collectionName), upsertOrder);
            var upserted = response.Resource;

            Console.WriteLine("Request charge of upsert operation: {0}", response.RequestCharge);
            Console.WriteLine("StatusCode of this operation: {0}", response.StatusCode);
            Console.WriteLine("Id of upserted document: {0}", upserted.Id);
            Console.WriteLine("AccountNumber of upserted document: {0}", upserted.GetPropertyValue<string>("AccountNumber"));

            upserted.SetPropertyValue("AccountNumber", "updated account number");
            response = await client.UpsertDocumentAsync(UriFactory.CreateDocumentCollectionUri(databaseName, collectionName), upserted);
            upserted = response.Resource;

            Console.WriteLine("Request charge of upsert operation: {0}", response.RequestCharge);
            Console.WriteLine("StatusCode of this operation: {0}", response.StatusCode);
            Console.WriteLine("Id of upserted document: {0}", upserted.Id);
            Console.WriteLine("AccountNumber of upserted document: {0}", upserted.GetPropertyValue<string>("AccountNumber"));
        }
        private static async Task DeleteDocumentAsync()
        {
            Console.WriteLine("\n1.7 - Deleting a document");
            ResourceResponse<Document> response = await client.DeleteDocumentAsync(
                UriFactory.CreateDocumentUri(databaseName, collectionName, "SalesOrder3"),
                new RequestOptions { PartitionKey = new PartitionKey("Account1") });

            Console.WriteLine("Request charge of delete operation: {0}", response.RequestCharge);
            Console.WriteLine("StatusCode of operation: {0}", response.StatusCode);
        }

        private static SalesOrder GetSalesOrderSample(string documentId)
        {
            return new SalesOrder
            {
                Id = documentId,
                AccountNumber = "Account1",
                PurchaseOrderNumber = "PO18009186470",
                OrderDate = new DateTime(2005, 7, 1),
                SubTotal = 419.4589m,
                TaxAmount = 12.5838m,
                Freight = 472.3108m,
                TotalDue = 985.018m,
                Items = new SalesOrderDetail[]
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
        }
        private static SalesOrder2 GetSalesOrderV2Sample(string documentId)
        {
            return new SalesOrder2
            {
                Id = documentId,
                AccountNumber = "Account2",
                PurchaseOrderNumber = "PO15428132599",
                OrderDate = new DateTime(2005, 7, 1),
                DueDate = new DateTime(2005, 7, 13),
                ShippedDate = new DateTime(2005, 7, 8),
                SubTotal = 6107.0820m,
                TaxAmt = 586.1203m,
                Freight = 183.1626m,
                DiscountAmt = 1982.872m,            // new property added to SalesOrder2
                TotalDue = 4893.3929m,
                Items = new SalesOrderDetail2[]
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
            };
        }

        /// <summary>
        /// 2. Basic CRUD operations using dynamics instead of strongly typed objects
        /// 
        /// DocumentDB does not require objects to be typed. Applications that merge data from different data sources, or 
        /// need to handle evolving schemas can write data directly as JSON or dynamic objects.
        /// </summary>
        private static async Task RunBasicOperationsOnDynamicObjects()
        {
            Console.WriteLine("\n2. Use Dynamics");

            var collectionLink = UriFactory.CreateDocumentCollectionUri(databaseName, collectionName);

            // Create a dynamic object
            dynamic salesOrder = new { 
                id = "_SalesOrder5", 
                AccountNumber = "NewUser01",
                PurchaseOrderNumber = "PO18009186470",
                OrderDate = DateTime.UtcNow,
                Total = 5.95,
            };

            Console.WriteLine("\nCreating document");

            ResourceResponse<Document> response = await client.CreateDocumentAsync(collectionLink, salesOrder);
            var createdDocument = response.Resource;

            Console.WriteLine("Document with id {0} created", createdDocument.Id);
            Console.WriteLine("Request charge of operation: {0}", response.RequestCharge);

            response = await client.ReadDocumentAsync(
                UriFactory.CreateDocumentUri(databaseName, collectionName, "_SalesOrder5"), 
                new RequestOptions { PartitionKey = new PartitionKey("NewUser01") });
            
            var readDocument = response.Resource;

            //update a dynamic object by just creating a new Property on the fly
            //Document is itself a dynamic object, so you can just use this directly too if you prefer
            readDocument.SetPropertyValue("shippedDate", DateTime.UtcNow);

            //if you wish to work with a dynamic object so you don't need to use SetPropertyValue() or GetPropertyValue<T>()
            //then you can cast to a dynamic
            salesOrder = (dynamic)readDocument;
            salesOrder.foo = "bar";

            //now do a replace using this dynamic document
            //notice here you don't have to set collectionLink, or documentSelfLink, 
            //everything that is needed is contained in the readDynOrder object 
            //it has a .self Property
            Console.WriteLine("\nReplacing document");

            response = await client.ReplaceDocumentAsync(salesOrder);
            var replaced = response.Resource;

            Console.WriteLine("Request charge of operation: {0}", response.RequestCharge);
            Console.WriteLine("shippedDate: {0} and foo: {1} of replaced document", replaced.GetPropertyValue<DateTime>("shippedDate"), replaced.GetPropertyValue<string>("foo"));
        }

        /// <summary>
        /// 3. Using ETags to control execution of operations
        /// 3.1 - Use ETag to control if a ReplaceDocument operation should check if ETag of request matches Document
        /// 3.2 - Use ETag to control if ReadDocument should only return a result if the ETag of the request does not match the Document
        /// </summary>
        /// <returns></returns>
        private static async Task UseETags()
        {

            //******************************************************************************************************************
            // 3.1 - Use ETag to control if a replace should succeed, or not, based on whether the ETag on the requst matches
            //       the current ETag value of the persisted Document
            //
            // All documents in DocumentDB have an _etag field. This gets set on the server every time a document is updated.
            // 
            // When doing a replace of a document you can opt-in to having the server only apply the Replace if the ETag 
            // on the request matches the ETag of the document on the server.
            // If someone did an update to the same document since you read it, then the ETag on the server will not match
            // and the Replace operation can be rejected. 
            //******************************************************************************************************************
            Console.WriteLine("\n3.1 - Using optimistic concurrency when doing a ReplaceDocumentAsync");

            //read a document
            Document readDoc = await client.ReadDocumentAsync(
                UriFactory.CreateDocumentUri(databaseName, collectionName, "SalesOrder1"),
                new RequestOptions { PartitionKey = new PartitionKey("Account1") });

            Console.WriteLine("ETag of read document - {0}", readDoc.ETag);

            //take advantage of the dynamic nature of Document and set a new property on the document we just read
            readDoc.SetPropertyValue("foo", "bar");

            //persist the change back to the server
            Document updatedDoc = await client.ReplaceDocumentAsync(readDoc);
            Console.WriteLine("ETag of document now that is has been updated - {0}", updatedDoc.ETag);

            //now, using the originally retrieved document do another update 
            //but set the AccessCondition class with the ETag of the originally read document and also set the AccessConditionType
            //this tells the service to only do this operation if ETag on the request matches the current ETag on the document
            //in our case it won't, because we updated the document and therefore gave it a new ETag
            try
            {
                var ac = new AccessCondition { Condition = readDoc.ETag, Type = AccessConditionType.IfMatch };
                readDoc.SetPropertyValue("foo", "the updated value of foo");
                updatedDoc = await client.ReplaceDocumentAsync(readDoc, new RequestOptions { AccessCondition = ac });
            }
            catch (DocumentClientException dce)
            {
                //   now notice the failure when attempting the update 
                //   this is because the ETag on the server no longer matches the ETag of doc (b/c it was changed in step 2)
                if (dce.StatusCode == HttpStatusCode.PreconditionFailed)
                {
                    Console.WriteLine("As expected, we have a pre-condition failure exception\n");
                }
            }

            //*******************************************************************************************************************
            // 3.2 - ETag on a ReadDcoumentAsync request can be used to tell the server whether it should return a result, or not
            //
            // By setting the ETag on a ReadDocumentRequest along with an AccessCondition of IfNoneMatch instructs the server
            // to only return a result if the ETag of the request does not match that of the persisted Document
            //*******************************************************************************************************************
            Console.WriteLine("\n3.2 - Using ETag to do a conditional ReadDocumentAsync");

            //get a document
            var response = await client.ReadDocumentAsync(
                UriFactory.CreateDocumentUri(databaseName, collectionName, "SalesOrder2"),
                new RequestOptions { PartitionKey = new PartitionKey("Account2") });

            readDoc = response.Resource;
            Console.WriteLine("Read doc with StatusCode of {0}", response.StatusCode);
            
            //get the document again with conditional access set, no document should be returned
            var accessCondition = new AccessCondition
            {
                Condition = readDoc.ETag,
                Type = AccessConditionType.IfNoneMatch
            };

            response = await client.ReadDocumentAsync(
                UriFactory.CreateDocumentUri(databaseName, collectionName, "SalesOrder2"), 
                new RequestOptions
                {
                    AccessCondition = accessCondition,
                    PartitionKey = new PartitionKey("Account2")
                });

            Console.WriteLine("Read doc with StatusCode of {0}", response.StatusCode);

            //now change something on the document, then do another get and this time we should get the document back
            readDoc.SetPropertyValue("foo", "updated");
            response = await client.ReplaceDocumentAsync(readDoc);

            response = await client.ReadDocumentAsync(
                UriFactory.CreateDocumentUri(databaseName, collectionName, "SalesOrder2"), 
                new RequestOptions
                {
                    AccessCondition = accessCondition,
                    PartitionKey = new PartitionKey("Account2")
                });

            Console.WriteLine("Read doc with StatusCode of {0}", response.StatusCode);
        }

        private static void Cleanup()
        {
            client.DeleteDatabaseAsync(UriFactory.CreateDatabaseUri(databaseName)).Wait();
        }

        private static async Task Initialize()
        {
            await DeleteDatabaseIfExists(databaseName);

            await client.CreateDatabaseAsync(new Database { Id = databaseName });


            // We create a partitioned collection here which needs a partition key. Partitioned collections
            // can be created with very high values of provisioned throughput (up to OfferThroughput = 250,000)
            // and used to store up to 250 GB of data. You can also skip specifying a partition key to create
            // single partition collections that store up to 10 GB of data.
            DocumentCollection collectionDefinition = new DocumentCollection();
            collectionDefinition.Id = collectionName;

            // For this demo, we create a collection to store SalesOrders. We set the partition key to the account
            // number so that we can retrieve all sales orders for an account efficiently from a single partition,
            // and perform transactions across multiple sales order for a single account number. 
            collectionDefinition.PartitionKey.Paths.Add("/AccountNumber");

            // Use the recommended indexing policy which supports range queries/sorting on strings
            collectionDefinition.IndexingPolicy = new IndexingPolicy(new RangeIndex(DataType.String) { Precision = -1 });

            // Create with a throughput of 1000 RU/s
            await client.CreateDocumentCollectionAsync(
                UriFactory.CreateDatabaseUri(databaseName),
                collectionDefinition,
                new RequestOptions { OfferThroughput = 1000 });
        }
        private static async Task<Database> DeleteDatabaseIfExists(string databaseId)
        {
            var databaseUri = UriFactory.CreateDatabaseUri(databaseId);

            Database database = client.CreateDatabaseQuery()
                .Where(db => db.Id == databaseId)
                .ToArray()
                .FirstOrDefault();

            if (database != null)
            {
                await client.DeleteDatabaseAsync(UriFactory.CreateDatabaseUri(databaseId));
            }

            return database;
        }
    }
}
