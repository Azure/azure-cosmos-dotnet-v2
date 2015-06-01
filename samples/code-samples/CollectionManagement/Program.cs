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

    //-----------------------------------------------------------------------------------------------------------
    // This sample demonstrates the basic CRUD operations on a DatabaseCollection resource for Azure DocumentDB
    //
    // For advanced concepts like;
    // DocumentCollection IndexPolicy management please consult DocumentDB.Samples.IndexManagement
    //-----------------------------------------------------------------------------------------------------------

    public class Program
    {
        private static DocumentClient client;

        //Get an Id for your database & collection from config
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
                //Get a DocumentClient            
                using (client = new DocumentClient(new Uri(endpointUrl), authorizationKey))
                {
                    RunDemoAsync().Wait();
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

        private static async Task RunDemoAsync()
        {
            //Try get a Database if exists, else create the Database resource
            Database database = await GetOrCreateDatabaseAsync(databaseId);
            
            //Create a new collection on the Database
            DocumentCollection collection = await client.CreateDocumentCollectionAsync(database.SelfLink, new DocumentCollection { Id = collectionId });
            Console.WriteLine("Created Collection {0}.", collection);

            //Read Collection Feed on the Database
            List<DocumentCollection> cols = await ReadCollectionsFeedAsync(database.SelfLink);
            foreach (var col in cols)
            {
                Console.WriteLine(col);                
            }

            //To list collections on a Database you could also just do a simple Linq query like this
            //The DocumentClient will transparently execute multiple calls to the Database Service
            //if it receives a continuation token. For larger sets of results the above method might be 
            //preferred because you can control the number of records to return per call
            cols = client.CreateDocumentCollectionQuery(database.CollectionsLink).ToList();
            foreach (var col in cols)
            {
                Console.WriteLine(col);
            }

            //Cleanup, 
            //Deleting a DocumentCollection will delete everything linked to the collection.
            //As will deleting the Database. Therefore, we don't actually need to explictly delete the collection
            //it's just being done for demonstration purposes. 
            await client.DeleteDocumentCollectionAsync(collection.SelfLink);
            await client.DeleteDatabaseAsync(database.SelfLink);
        }

        /// <summary>
        ///  This method uses a ReadCollectionsFeedAsync method to read a list of all collections on a database.
        ///  It demonstrates a pattern for how to control paging and deal with continuations
        ///  This should not be needed for reading a list of collections as there are unlikely to be many hundred,
        ///  but this same pattern is introduced here and can be used on other ReadFeed methods.
        /// </summary>
        /// <returns>A List of DocuemntCollection entities</returns>
        private static async Task<List<DocumentCollection>> ReadCollectionsFeedAsync(string databaseSelfLink)
        {
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
        
        /// <summary>
        /// Get the database by name, or create a new one if one with the name provided doesn't exist.
        /// </summary>
        /// <param name="id">The name of the database to search for, or create.</param>
        private static async Task<Database> GetOrCreateDatabaseAsync(string id)
        {
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
