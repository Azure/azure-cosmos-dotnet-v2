namespace DocumentDB.Samples.DatabaseManagement
{
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Documents.Linq;
    using System;
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
                    DatabaseManagementAsync().Wait();
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

        private static async Task DatabaseManagementAsync()
        {
            //Try to get a database
            //Note: we are using query here instead of ReadDatabaseAsync because we're checking if something exists
            //      the ReadDatabaseAsync method expects the resource to be there, if its not we will get an error
            //      instead of an empty 
            Database database = client.CreateDatabaseQuery().Where(db => db.Id == databaseId).AsEnumerable().FirstOrDefault();
            
            //First check if a database was returned
            if (database==null)
            {
                //Create database
                database = await client.CreateDatabaseAsync(new Database { Id = databaseId });
                Console.WriteLine("Created Database: id - {0} and selfLink - {1}", database.Id, database.SelfLink);
            }
            
            //Get a single database
            //Note: that we don't need to use the SelfLink of a Database anymore
            //      the links for a resource are now comprised of their ID properties
            //      using UriFactory will give you the correct URI for a resource
            //
            //      SelfLink will still work if you're already using this
            database = await client.ReadDatabaseAsync(UriFactory.CreateDatabaseUri(databaseId));
            
            //List all databases for an account
            var databases = await client.ReadDatabaseFeedAsync();
            foreach (var db in databases)
            {
                Console.WriteLine(db);    
            }
            
            //Delete a database
            await client.DeleteDatabaseAsync(UriFactory.CreateDatabaseUri(databaseId));
        }        
    }
}
