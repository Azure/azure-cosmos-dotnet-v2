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

    //-----------------------------------------------------------------------------------------------------------
    // This sample demonstrates the basic CRUD operations on a Document resource for Azure DocumentDB
    //
    // For advanced concepts please consult the relevant Sample project(s);
    // Querying Documents - DocumentDB.Samples.QueryingDocuments
    // Pagination Documents - DocumentDB.Samples.ServerSideScripts
    //-----------------------------------------------------------------------------------------------------------
    
    public class Program
    {
        private static DocumentClient client;

        //Assign a name for your database & collection 
        private static readonly string databaseId = ConfigurationManager.AppSettings["DatabaseId"];
        private static readonly string collectionId = ConfigurationManager.AppSettings["CollectionId"];

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
                    RunDemoAsync(databaseId, collectionId).Wait();
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

        private static async Task RunDemoAsync(string databaseId, string collectionId)
        {
            //Get, or Create, the Database
            var database = await GetOrCreateDatabaseAsync(databaseId);

            //Get, or Create, the Document Collection
            var collection = await GetOrCreateCollectionAsync(database.SelfLink, collectionId);

            //--------------------------------------------------------------------------------------------------------
            // NB:
            // To insert large batches of documents, it is recommended to use a stored procedure to do the insert
            // and control the execution of the stored procedure from your client application.
            // For an example of this please refer to the DocumentDB.Samples.ServerSideScripts project
            // 
            // For the purposes of this sample we're iterating over a small number of documents to illustrate a concept
            //--------------------------------------------------------------------------------------------------------

            //One approach to access to DocumentDB documents in .NET is through “POCOs” or plain-old-clr-objects
            //which will use JSON.NET serialization and deserialization under the covers to transmit the data over the wire
            await UsePOCOs(collection.SelfLink);

            //It is also possible to work with documents entirely as dynamic documents without any schema
            await UseDynamics(collection.SelfLink);

            //For high performance applications that want to avoid the serialization overhead, 
            //the SDK supports reading from streams. 
            await UseStreams(collection.SelfLink);
            
            //Lastly, you can extend from Microsoft.Azure.Documents.Document
            //This allows access to standard DocumentDB resource properties like Id, Name, Timestamp and ETag.
            await UseDocumentExtensions(collection.SelfLink);

            //Create a single document with an attachment and insert in to collection 
            await UseAttachments(collection.SelfLink);
            
            //Clean-up environment
            await DeleteDatabaseAsync(database.SelfLink);
        }

        private static async Task UsePOCOs(string colSelfLink)
        {
            //Create a list of POCO objects, and insert in to the collection
            //Inspect listOfOrders and you will notice each document within has 
            //a different schemas, SalesOrder and SalesOrder2 to demonstrate how applications change over time
            //And even though the two documents are from different schemas we're going to 
            //create them inside the same collection and work with them seamlessly together
            var orders = new List<object>();

            //DocumentDB requires an "id" for all documents. You can either supply your own unique value for id, or
            //let DocumentDB provide it for you. Here we are supplying an id so DocumentDB will just ensure uniqueness of our values
            //We could also use JsonProperty to alter any unique property on our objects and make it "id" over the wire and in DocumentDB.
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
                    },
                    new SalesOrderDetail2
                    {
                        OrderQty = 1,
                        ProductCode = "B-432",
                        ProductName = "Product 2",
                        CurrencySymbol = "$",
                        CurrencyCode = "NZD",
                        UnitPrice = 2039.994m,
                        LineTotal = 2039.994m
                    },
                    new SalesOrderDetail2
                    {
                        OrderQty = 1,
                        ProductCode = "C-2312S",
                        ProductName = "Product 3",
                        CurrencySymbol = "R",
                        CurrencyCode = "ZAR",
                        UnitPrice = 2024.994m,
                        LineTotal = 2024.994m
                    },
                }
            });

            foreach (var order in orders)
            {
                Document created = await client.CreateDocumentAsync(colSelfLink, order);
                Console.WriteLine("Created SalesOrder: " + created);
            }

            //Read a Document from the database as a dynamic, then cast it down to your POCO object
            //If SalesOrder inherited from Document then we wouldn't need the dynamic object
            //The only reason we do this is to first get the doc.SelfLink which we need later when we replace the document.
            //If you don't need to update the document you could just do client.CreateDocumentQuery<SalesOrder>
            //which will do implicit deserialization in to the object type specified. 
            dynamic doc = client.CreateDocumentQuery<Document>(colSelfLink).Where(d => d.Id == "POCO1").AsEnumerable().FirstOrDefault();
            SalesOrder createdOrder = doc;

            //Now update a property on the POCO
            createdOrder.ShippedDate = DateTime.UtcNow;

            //And persist the change to DocumentDB
            Document updatedOrder = await client.ReplaceDocumentAsync(doc.SelfLink, createdOrder);
        }

        private static async Task UseDynamics(string colSelfLink)
        {
            //Create a dynamic object,
            dynamic dynamicOrder = new
            {
                id = "DYN01",
                purchaseOrderNumber = "PO18009186470",
                orderDate = DateTime.UtcNow,
                total = 5.95,
            };

            Document createdDocument = await client.CreateDocumentAsync(colSelfLink, dynamicOrder);

            //get a dynamic object
            dynamic readDynOrder = (await client.ReadDocumentAsync(createdDocument.SelfLink)).Resource;

            //update a dynamic object
            readDynOrder.ShippedDate = DateTime.UtcNow;
            await client.ReplaceDocumentAsync(readDynOrder);
        }

        private static async Task UseStreams(string colSelfLink)
        {
            var dir = new DirectoryInfo(@".\Data");
            var files = dir.EnumerateFiles("*.json");
            foreach (var file in files)
            {
                using (var fileStream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read))
                {
                    Document doc = await client.CreateDocumentAsync(colSelfLink, Resource.LoadFrom<Document>(fileStream));
                    Console.WriteLine("Created Document: ", doc);
                }
            }

            //Read one the documents created above directly in to a Json string
            Document readDoc = client.CreateDocumentQuery(colSelfLink).Where(d => d.Id == "JSON1").AsEnumerable().First();
            string content = JsonConvert.SerializeObject(readDoc);

            //Update a document with some Json text, 
            //Here we're replacing a previously created document with some new text and even introudcing a new Property, Status=Cancelled
            using (var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes("{\"id\": \"JSON1\",\"PurchaseOrderNumber\": \"PO18009186470\",\"Status\": \"Cancelled\"}")))
            {
                await client.ReplaceDocumentAsync(readDoc.SelfLink, Resource.LoadFrom<Document>(memoryStream));
            }
        }

        private static async Task UseDocumentExtensions(string colSelfLink)
        {
            //Create an object that extends Document
            var salesOrderDocument = new SalesOrderDocument
            {
                Id = "DOCO1",
                PurchaseOrderNumber = "PO180091783420",
                OrderDate = new DateTime(2013, 7, 17),
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
                }
            };

            Document created = await client.CreateDocumentAsync(colSelfLink, salesOrderDocument);

            //Read document
            SalesOrderDocument readSalesOrderDocument = (SalesOrderDocument)(dynamic)(await client.ReadDocumentAsync(created.SelfLink)).Resource;
            
            //Update a property on the SalesOrderDocument
            readSalesOrderDocument.ShipDate = DateTime.UtcNow;

            //Persist the change to DocumentDB
            await client.ReplaceDocumentAsync(readSalesOrderDocument.SelfLink, readSalesOrderDocument);
        }
        
        private static async Task UseAttachments(string colSelfLink)
        {
            dynamic documentWithAttachment = new 
            {
                id = "PO1800243243470",
                CustomerId = 1092,
                TotalDue = 985.018m,
            };

            Document doc = await client.CreateDocumentAsync(colSelfLink, documentWithAttachment);

            //This attachment could be any binary you want to attach. Like images, videos, word documents, pdfs etc. it doesn't matter
            using (FileStream fileStream = new FileStream(@".\Attachments\text.txt", FileMode.Open))
            {
                //Create the attachment
                await client.CreateAttachmentAsync(doc.AttachmentsLink, fileStream, new MediaOptions { ContentType = "text/plain", Slug = "text.txt" });
            }

            //Query for document for attachment for attachments
            Attachment attachment = client.CreateAttachmentQuery(doc.SelfLink).AsEnumerable().FirstOrDefault();
            
            //Use DocumentClient to read the Media content
            MediaResponse content = await client.ReadMediaAsync(attachment.MediaLink);

            byte[] bytes = new byte[content.ContentLength];
            await content.Media.ReadAsync(bytes, 0, (int)content.ContentLength);
            string result = Encoding.UTF8.GetString(bytes);
        }
        
        /// <summary>
        /// Deletes a Database resource
        /// </summary>
        /// <param name="databaseLink">The SelfLink of the Database resource to be deleted</param>
        /// <returns></returns>
        private static async Task DeleteDatabaseAsync(string databaseLink)
        {
            await client.DeleteDatabaseAsync(databaseLink);
        }
        
        /// <summary>
        /// Get a DocumentCollection by id, or create a new one if one with the id provided doesn't exist.
        /// </summary>
        /// <param name="dbLink">The Database SelfLink property where this DocumentCollection exists / will be created</param>
        /// <param name="id">The id of the DocumentCollection to search for, or create.</param>
        /// <returns>The matched, or created, DocumentCollection object</returns>
        private static async Task<DocumentCollection> GetOrCreateCollectionAsync(string dbLink, string id)
        {
            DocumentCollection collection = client.CreateDocumentCollectionQuery(dbLink).Where(c => c.Id == id).ToArray().FirstOrDefault();
            if (collection == null)
            {
                collection = await client.CreateDocumentCollectionAsync(dbLink, new DocumentCollection { Id = id });
            }

            return collection;
        }
        
        /// <summary>
        /// Get a Database by id, or create a new one if one with the id provided doesn't exist.
        /// </summary>
        /// <param name="id">The id of the Database to search for, or create.</param>
        /// <returns>The matched, or created, Database object</returns>
        private static async Task<Database> GetOrCreateDatabaseAsync(string id)
        {
            Database database = client.CreateDatabaseQuery().Where(db => db.Id == id).ToArray().FirstOrDefault();
            if (database == null)
            {
                database = await client.CreateDatabaseAsync(new Database { Id = id });
            }

            return database;
        }
    }
}