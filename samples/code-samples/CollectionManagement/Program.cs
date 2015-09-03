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
    // 2. Get Offer
    //    An Offer.OfferType represents the current performance tier of a Collection
    //
    // 3. Replace Offer
    //    By changing the Offer.OfferType you scale the linked Collection up, or down, between performance tiers
    //
    // 4. Delete Collection
    //
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
        private static readonly ConnectionPolicy connectionPolicy = new ConnectionPolicy { UserAgentSuffix = " samples-net/2" };

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
            Console.WriteLine("1.1 Created Collection {0}.\n", c1);

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
            Console.WriteLine("1.2 Created Collection {0}, with custom index policy {1}.\n", c2.Id, c2.IndexingPolicy);

            //DocumentCollection have offers which are of type S1, S2, or S3. Each of these determine the performance throughput of a collection. 
            //DocumentCollection is loosely coupled to Offer through its ResourceId (or its SelfLink)

            //**************
            // 2. Get Offer
            //**************

            //Offers are "linked" to DocumentCollection through the collection's SelfLink
            //Offer.ResourceLink == Collection.SelfLink
            Offer offer = client.CreateOfferQuery().Where(o => o.ResourceLink == c1.SelfLink).AsEnumerable().Single();
            Console.WriteLine("2 Found Offer {0} using collection's SelfLink {1}.\n", offer, c1.SelfLink);

            //*****************
            // 3. Replace Offer
            //*****************

            //So the Offer is S1 by default (we see that b/c we never set this @ creation and it is an S1 as shown above), 
            //Now let's step this collection up to an S2
            //To do this, change the OfferType property of the Offer to S2
            //NB! If you run this you will be billed for at least 1 hour @ S2 price
            offer.OfferType = "S2";
            Offer replaced = await client.ReplaceOfferAsync(offer);
            Console.WriteLine("3 Replaced Offer. OfferType is now {0}.\n", replaced.OfferType);

            //Get the offer again after replace
            offer = client.CreateOfferQuery().Where(o => o.ResourceLink == c1.SelfLink).AsEnumerable().Single();
            Console.WriteLine("2 Found Offer {0} using collection's ResourceId {1}.\n", offer, c1.ResourceId);

            //**************************************
            //3.1 Read a feed of DocumentCollection
            //***************************************

            List<DocumentCollection> cols = await ReadCollectionsFeedAsync(database.SelfLink);
            foreach (var col in cols)
            {
                Console.WriteLine("3.1 Found Collection {0}\n", col.Id);                
            }

            //*********************************
            //3.2 Query for DocumentCollection
            //*********************************

            //You can also query a Database for DocumentCollections. 
            //This is useful when you're looking for a specific matching criteria. E.g. id == "SampleCollection"
            cols = client.CreateDocumentCollectionQuery(database.CollectionsLink).Where(coll => coll.Id == collectionId).ToList();
            foreach (var col in cols)
            {
                Console.WriteLine("3.2 Found Collection {0}\n", col.Id);                
            }

            //********************************
            //4. Delete a DocumentCollection 
            //********************************

            //NB: Deleting a collection will delete everything linked to the collection.
            //    This includes ALL documents, stored procedures, triggers, udfs
            await client.DeleteDocumentCollectionAsync(c1.SelfLink);
            Console.WriteLine("4 Deleted Collection {0}\n", c1.Id);

            //Cleanup
            //Delete Database. 
            // - will delete everything linked to the database, 
            // - we didn't really need to explictly delete the collection above
            // - it was just done for demonstration purposes. 
            await client.DeleteDatabaseAsync(database.SelfLink);
        }

        private static async Task<List<DocumentCollection>> ReadCollectionsFeedAsync(string databaseSelfLink)
        {
            //  This method uses a ReadCollectionsFeedAsync method to read a list of all collections on a database.
            //  It demonstrates a pattern for how to control paging and deal with continuations
            //  This should not be needed for reading a list of collections as there are unlikely to be many hundred,
            //  but this same pattern is introduced here and can be used on other ReadFeed methods.
            
            string continuation = null;
            List<DocumentCollection> collections = new List<DocumentCollection>();

            do
            {
                FeedOptions options = new FeedOptions
                {
                    RequestContinuation = continuation,
                    MaxItemCount = 50
                };

                FeedResponse<DocumentCollection> response = (FeedResponse<DocumentCollection>) await client.ReadDocumentCollectionFeedAsync(databaseSelfLink, options);

                foreach (DocumentCollection col in response)
                {
                    collections.Add(col);
                }

                continuation = response.ResponseContinuation;

            } while (!String.IsNullOrEmpty(continuation));

            return collections;
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
