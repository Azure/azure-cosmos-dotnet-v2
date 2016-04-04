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
    // Prerequisites - 
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
    // 2. Get DocumentCollection performance (reserved throughput)
    //    Read the Offer object for the collection and extract OfferThroughput
    //
    // 3. Change performance (reserved throughput)
    //    By changing the Offer.OfferThroughput you can scale throughput up or down
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
        private static readonly string endpointUrl = ConfigurationManager.AppSettings["EndPointUrl"];
        private static readonly string authorizationKey = ConfigurationManager.AppSettings["AuthorizationKey"];
        private static readonly string databaseName = ConfigurationManager.AppSettings["DatabaseId"];
        private static readonly string collectionName = ConfigurationManager.AppSettings["CollectionId"];
        private static readonly ConnectionPolicy connectionPolicy = new ConnectionPolicy { UserAgentSuffix = " samples-net/3" };

        private static DocumentClient client;
        public static void Main(string[] args)
        {
            try
            {
                using (client = new DocumentClient(new Uri(endpointUrl), authorizationKey, connectionPolicy))
                {
                    CreateNewDatabaseAsync().Wait();
                    RunCollectionDemo().Wait();
                }
            }            
            catch (Exception e)
            {
                LogException(e);
            }
            finally
            {
                Console.WriteLine("End of demo, press any key to exit.");
                Console.ReadKey();
            }
        }

        /// <summary>
        /// Create database if it does not exist
        /// </summary>
        /// <returns></returns>
        private static async Task CreateNewDatabaseAsync()
        {
            try
            {
                await client.ReadDatabaseAsync(UriFactory.CreateDatabaseUri(databaseName));
                await client.DeleteDatabaseAsync(UriFactory.CreateDatabaseUri(databaseName));
            }
            catch (DocumentClientException e)
            {
                // If we receive an error other than database not found, fail
                if (e.StatusCode != System.Net.HttpStatusCode.NotFound)
                {
                    throw;
                }
            }

            await client.CreateDatabaseAsync(new Database { Id = databaseName });
        }

        /// <summary>
        /// Run through basic collection access methods as a console app demo.
        /// </summary>
        /// <returns></returns>
        private static async Task RunCollectionDemo()
        {
            DocumentCollection simpleCollection = await CreateCollection();

            await CreateCollectionWithCustomIndexingPolicy();

            await GetAndChangeCollectionPerformance(simpleCollection);

            await ReadCollectionProperties();

            await ListCollectionsInDatabase();

            await DeleteCollection();

            await client.DeleteDatabaseAsync(UriFactory.CreateDatabaseUri(databaseName));
        }
        private static async Task<DocumentCollection> CreateCollection()
        {
            // Set throughput to the minimum value of 400 RU/s
            DocumentCollection simpleCollection = await client.CreateDocumentCollectionAsync(
                UriFactory.CreateDatabaseUri(databaseName),
                new DocumentCollection { Id = collectionName }, 
                new RequestOptions { OfferThroughput = 400 });

            Console.WriteLine("\n1.1. Created Collection \n{0}", simpleCollection);
            return simpleCollection;
        }

        private static async Task CreateCollectionWithCustomIndexingPolicy()
        {
            // Create a collection with custom index policy (lazy indexing)
            // We cover index policies in detail in IndexManagement sample project
            DocumentCollection collectionDefinition = new DocumentCollection();
            collectionDefinition.Id = "SampleCollectionWithCustomIndexPolicy";
            collectionDefinition.IndexingPolicy.IndexingMode = IndexingMode.Lazy;

            DocumentCollection collectionWithLazyIndexing = await client.CreateDocumentCollectionAsync(
                UriFactory.CreateDatabaseUri(databaseName),
                collectionDefinition,
                new RequestOptions { OfferThroughput = 400 });

            Console.WriteLine("1.2. Created Collection {0}, with custom index policy \n{1}", collectionWithLazyIndexing.Id, collectionWithLazyIndexing.IndexingPolicy);
        }
        
        private static async Task GetAndChangeCollectionPerformance(DocumentCollection simpleCollection)
        {

            //*********************************************************************************************
            // Get configured performance (reserved throughput) of a DocumentCollection
            //
            //    DocumentCollections each have a corresponding Offer resource that represents the reserved throughput of the collection.
            //    Offers are "linked" to DocumentCollection through the collection's SelfLink (Offer.ResourceLink == Collection.SelfLink)
            //
            //**********************************************************************************************
            Offer offer = client.CreateOfferQuery().Where(o => o.ResourceLink == simpleCollection.SelfLink).AsEnumerable().Single();

            Console.WriteLine("\n2. Found Offer \n{0}\nusing collection's SelfLink \n{1}", offer, simpleCollection.SelfLink);

            //******************************************************************************************************************
            // Change performance (reserved throughput) of DocumentCollection
            //    Let's change the performance of the collection to 500 RU/s
            //******************************************************************************************************************

            Offer replaced = await client.ReplaceOfferAsync(new OfferV2(offer, 500));
            Console.WriteLine("\n3. Replaced Offer. Offer is now {0}.\n", replaced);

            // Get the offer again after replace
            offer = client.CreateOfferQuery().Where(o => o.ResourceLink == simpleCollection.SelfLink).AsEnumerable().Single();
            OfferV2 offerV2 = (OfferV2)offer;
            Console.WriteLine(offerV2.Content.OfferThroughput);

            Console.WriteLine("3. Found Offer \n{0}\n using collection's ResourceId {1}.\n", offer, simpleCollection.ResourceId);
        }
        
        private static async Task ReadCollectionProperties()
        {
            //*************************************************
            // Get a DocumentCollection by its Id property
            //*************************************************
            DocumentCollection collection = await client.ReadDocumentCollectionAsync(UriFactory.CreateDocumentCollectionUri(databaseName, collectionName));

            Console.WriteLine("\n4. Found Collection \n{0}\n", collection);
        }

        /// <summary>
        /// List the collections within a database by calling the ReadFeed (scan) API.
        /// </summary>
        /// <returns></returns>
        private static async Task ListCollectionsInDatabase()
        {
            Console.WriteLine("\n5. Reading all DocumentCollection resources for a database");

            foreach (var collection in await client.ReadDocumentCollectionFeedAsync(UriFactory.CreateDatabaseUri(databaseName)))
            {
                Console.WriteLine(collection);
            }
        }

        /// <summary>
        /// Delete a collection
        /// </summary>
        /// <param name="simpleCollection"></param>
        /// <returns></returns>
        private static async Task DeleteCollection()
        {
            await client.DeleteDocumentCollectionAsync(UriFactory.CreateDocumentCollectionUri(databaseName, collectionName));
            Console.WriteLine("\n6. Deleted Collection\n");
        }

        private static void LogException(Exception e)
        {
            ConsoleColor color = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;

            if (e is DocumentClientException)
            {
                DocumentClientException de = (DocumentClientException)e;
                Exception baseException = de.GetBaseException();
                Console.WriteLine("{0} error occurred: {1}, Message: {2}", de.StatusCode, de.Message, baseException.Message);
            }
            else
            {
                Exception baseException = e.GetBaseException();
                Console.WriteLine("Error: {0}, Message: {1}", e.Message, baseException.Message);
            }

            Console.ForegroundColor = color;
        }
    }
}
