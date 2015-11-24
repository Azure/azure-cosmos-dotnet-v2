namespace DocumentDB.Samples.CollectionManagement
{
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Documents.Linq;
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Linq;
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
    // Sample - demonstrates the basic CRUD operations on a DatabaseCollection resource for Azure DocumentDB
    //
    // 1. Create DocumentCollection
    //    1.1 - Basic Create
    //    1.2 - Create collection with custom IndexPolicy
    //
    // 2. Get DocumentCollection performance tier
    //    An Offer.OfferType represents the current performance tier of a Collection
    //
    // 3. Change performance tier
    //    By changing the Offer.OfferType you scale the linked Collection up, or down, between performance tiers
    //
    // 4. Get a DocumentCollection by its Id property
    //
    // 5. List all DocumentCollection resources in a Database
    //
    // 6. Delete DocumentCollection
    // ----------------------------------------------------------------------------------------------------------
    // Note - 
    // 
    // Running this sample will create (and delete) multiple DocumentCollections on your account. 
    // Each time a DocumentCollection is created the account will be billed for 1 hour of usage based on
    // the performance tier of that account. 
    // ----------------------------------------------------------------------------------------------------------
    // See Also - 
    //
    // DocumentDB.Samples.IndexManagement - for a more detailed look at custom index policies
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

        //The instance of a Database which we will be using for all the Collection operations being demo'd
        private static Database database;

        public static void Main(string[] args)
        {
            try
            {
                //Instantiate a new DocumentClient instance
                using (client = new DocumentClient(new Uri(endpointUrl), authorizationKey, connectionPolicy))
                {
                    //Get, or Create, a reference to Database
                    database = GetOrCreateDatabaseAsync(databaseId).Result;
                    
                    //Do operations on Collections
                    RunCollectionDemo().Wait();
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

        private static async Task RunCollectionDemo()
        {     
            //************************************
            // 1.1 - Basic Create
            //************************************
            DocumentCollection c1 = await client.CreateDocumentCollectionAsync(database.SelfLink, new DocumentCollection { Id = collectionId });

            Console.WriteLine("\n1.1. Created Collection \n{0}", c1);

            //*************************************************
            // 1.2 - Create collection with custom IndexPolicy
            //*************************************************
            //This is just a very simple example with custome index policies
            //We cover index policies in detail in IndexManagement sample project
            DocumentCollection collectionSpec = new DocumentCollection
            {
                Id = "SampleCollectionWithCustomIndexPolicy"
            };

            collectionSpec.IndexingPolicy.Automatic = false;
            collectionSpec.IndexingPolicy.IndexingMode = IndexingMode.Lazy;
            DocumentCollection c2 = await client.CreateDocumentCollectionAsync(database.SelfLink, collectionSpec );

            Console.WriteLine("1.2. Created Collection {0}, with custom index policy \n{1}", c2.Id, c2.IndexingPolicy);

            //*********************************************************************************************
            // 2. Get performance tier of a DocumentCollection
            //
            //    DocumentCollection have offers which are of type S1, S2, or S3. 
            //    Each of these determine the performance throughput of a collection. 
            //    DocumentCollection is loosely coupled to Offer through its ResourceId (or its SelfLink)
            //    Offers are "linked" to DocumentCollection through the collection's SelfLink
            //    Offer.ResourceLink == Collection.SelfLink
            //**********************************************************************************************
            Offer offer = client.CreateOfferQuery().Where(o => o.ResourceLink == c1.SelfLink).AsEnumerable().Single();

            Console.WriteLine("\n2. Found Offer \n{0}\nusing collection's SelfLink \n{1}", offer, c1.SelfLink);

            //******************************************************************************************************************
            // 3. Change performance tier of DocumentCollection
            //    So the Offer is S1 by default (we see that b/c we never set this @ creation and it is an S1 as shown above), 
            //    Now let's step this collection up to an S2
            //    To do this, change the OfferType property of the Offer to S2
            //
            //    NB! If you run this you will be billed for 1 hour @ S2 price until we delete the DocumentCollection
            //******************************************************************************************************************
            offer.OfferType = "S2";
            Offer replaced = await client.ReplaceOfferAsync(offer);

            Console.WriteLine("\n3. Replaced Offer. OfferType is now {0}.\n", replaced.OfferType);

            //Get the offer again after replace
            offer = client.CreateOfferQuery().Where(o => o.ResourceLink == c1.SelfLink).AsEnumerable().Single();
            
            Console.WriteLine("3. Found Offer \n{0}\nusing collection's ResourceId {1}.\n", offer, c1.ResourceId);

            //*************************************************
            //4. Get a DocumentCollection by its Id property
            //*************************************************
            DocumentCollection collection = await client.ReadDocumentCollectionAsync(UriFactory.CreateDocumentCollectionUri(databaseId, collectionId));

            Console.WriteLine("\n4. Found Collection \n{0}\n", collection);

            //******************************************************** 
            //5. List all DocumentCollection resources on a Database
            //********************************************************
            var colls = await client.ReadDocumentCollectionFeedAsync(UriFactory.CreateDatabaseUri(databaseId));
            Console.WriteLine("\n5. Reading all DocumentCollection resources for a database");
            foreach (var coll in colls)
            {
                Console.WriteLine(coll);                
            }

            //*******************************************************************************
            //6. Delete a DocumentCollection 
            //
            //   NB! Deleting a collection will delete everything linked to the collection.
            //       This includes ALL documents, stored procedures, triggers, udfs
            //*******************************************************************************
            await client.DeleteDocumentCollectionAsync(UriFactory.CreateDocumentCollectionUri(databaseId, collectionId));
            
            Console.WriteLine("\n6. Deleted Collection {0}\n", c1.Id);
            
            //Cleanup
            //Delete Database. 
            // - will delete everything linked to the database, 
            // - we didn't really need to explictly delete the collection above
            // - it was just done for demonstration purposes. 
            await client.DeleteDatabaseAsync(database.SelfLink);
        }

        private static async Task<Database> GetOrCreateDatabaseAsync(string id)
        {
            // Get the database by name, or create a new one if one with the name provided doesn't exist.
            // Create a query object for database, filter by name.
            IEnumerable<Database> query = from db in client.CreateDatabaseQuery() 
                                          where db.Id == id
                                          select db;

            // Run the query and get the database (there should be only one) or null if the query didn't return anything.
            // Note: this will run synchronously. If async exectution is preferred, use IDocumentServiceQuery<T>.ExecuteNextAsync.
            Database database = query.FirstOrDefault();
            if (database == null)
            {
                // Create the database.
                database = await client.CreateDatabaseAsync(new Database { Id = id });
            }
            
            return database;
        }
    }
}
