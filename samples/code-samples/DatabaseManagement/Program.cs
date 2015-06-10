namespace DocumentDB.Samples.DatabaseManagement
{
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Documents.Linq;
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Linq;
    using System.Threading.Tasks;

    /// <summary>
    /// This sample demonstrates basic CRUD operations on a Database resource for Azure DocumentDB
    /// </summary>
    public class Program
    {
        private static DocumentClient client;
        private static readonly string databaseId = ConfigurationManager.AppSettings["DatabaseId"];

        //Read the DocumentDB endpointUrl and authorisationKeys from config
        //These values are available from the Azure Management Portal on the DocumentDB Account Blade under "Keys"
        //NB > Keep these values in a safe & secure location. Together they provide Administrative access to your DocDB account
        private static readonly string endpointUrl = ConfigurationManager.AppSettings["EndPointUrl"];
        private static readonly string authorizationKey = ConfigurationManager.AppSettings["AuthorizationKey"];
        
        public static void Main(string[] args)
        {
            try
            {   
                //Connect to DocumentDB
                //Setup a single instance of DocumentClient that is reused throughout the application
                using (client = new DocumentClient(new Uri(endpointUrl), authorizationKey))
                {
                    RunAsync().Wait();
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

        private static async Task RunAsync()
        {
            //Try to get a database
            Database database = client.CreateDatabaseQuery().Where(db => db.Id == databaseId).AsEnumerable().FirstOrDefault();
            
            //Create database
            //First check if a database was returned, if not then create it
            if (database==null)
            {
                database = await client.CreateDatabaseAsync(new Database { Id = databaseId });
                Console.WriteLine("Created Database: id - {0} and selfLink - {1}", database.Id, database.SelfLink);
            }
            
            //List databases for an account
            var databases = await ListDatabasesAsync();
            foreach (var db in databases)
            {
                Console.WriteLine(db);    
            }
            
            //Delete a database
            //Cleanup using the SelfLink property of the Database which we either retrieved or created
            //If you do not have this SelfLink property accessible and populated you would need to get the Database using the id, 
            //then read the SelfLink property from that. This SelfLink value never changes for a Database once created;
            //so it would be perfectly acceptable practice to cache the value or store this in your configuratiom files
            await client.DeleteDatabaseAsync(database.SelfLink);
        }

        /// <summary>
        /// This method uses a ReadDatabaseFeedAsync method to read a list of all databases on the account.
        /// It demonstrates a pattern for how to control paging and deal with continuations
        /// This should not be needed for reading a list of databases as there are unlikely to be many hundred,
        /// but this same pattern is introduced here and can be used on other ReadFeed methods.
        /// </summary>
        /// <returns>A List of Database entities</returns>
        private static async Task<List<Database>> ListDatabasesAsync()
        {
            string continuation = null;
            List<Database> databases = new List<Database>();

            do
            {
                FeedOptions options = new FeedOptions
                {
                    RequestContinuation = continuation,
                    MaxItemCount = 50
                };

                FeedResponse<Database> response = await client.ReadDatabaseFeedAsync(options);
                foreach (Database db in response)
                {
                    databases.Add(db);
                }

                continuation = response.ResponseContinuation;
            } 
            while (!String.IsNullOrEmpty(continuation));

            return databases;
        }        
    }
}
