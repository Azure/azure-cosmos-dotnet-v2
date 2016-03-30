namespace DocumentDB.Samples.DatabaseManagement
{
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Documents.Linq;
    using System;
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
    // Sample - demonstrates the basic CRUD operations on a Database resource for Azure DocumentDB
    //
    // 1. Query for Database
    //
    // 2. Create Database
    //
    // 3. Get a Database by its Id property
    //
    // 4. List all Database resources on an account
    //
    // 5. Delete a Database given its Id property
    // ----------------------------------------------------------------------------------------------------------

    public class Program
    {
        //Read config
        private static readonly string databaseName = ConfigurationManager.AppSettings["DatabaseId"];
        private static readonly string endpointUrl = ConfigurationManager.AppSettings["EndPointUrl"];
        private static readonly string authorizationKey = ConfigurationManager.AppSettings["AuthorizationKey"];
        private static readonly ConnectionPolicy connectionPolicy = new ConnectionPolicy { UserAgentSuffix = " samples-net/3" };

        private static DocumentClient client;

        public static void Main(string[] args)
        {
            try
            {   
                // Setup a single instance of DocumentClient that is reused throughout the application
                using (client = new DocumentClient(new Uri(endpointUrl), authorizationKey))
                {
                    RunDatabaseDemo().Wait();
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

        /// <summary>
        /// Run basic database metadata operations as a console app.
        /// </summary>
        /// <returns></returns>
        private static async Task RunDatabaseDemo()
        {
            await CreateDatabaseIfNotExists();

            Database database = await client.ReadDatabaseAsync(UriFactory.CreateDatabaseUri(databaseName));
            Console.WriteLine("\n3. Read a database resource: {0}", database);

            Console.WriteLine("\n4. Reading all databases resources for an account");
            foreach (var db in await client.ReadDatabaseFeedAsync())
            {
                Console.WriteLine(db);
            }

            await client.DeleteDatabaseAsync(UriFactory.CreateDatabaseUri(databaseName));
            Console.WriteLine("\n5. Database {0} deleted.", database.Id);
        }

        /// <summary>
        /// Create a database if it doesn't exist.
        /// </summary>
        /// <returns></returns>
        private static async Task CreateDatabaseIfNotExists()
        {
            Database database = client.CreateDatabaseQuery().Where(db => db.Id == databaseName).AsEnumerable().FirstOrDefault();
            Console.WriteLine("1. Query for a database returned: {0}", database == null ? "no results" : database.Id);

            //check if a database was returned
            if (database == null)
            {
                database = await client.CreateDatabaseAsync(new Database { Id = databaseName });
                Console.WriteLine("\n2. Created Database: id - {0}", database.Id);
            }
        }
    }
}
