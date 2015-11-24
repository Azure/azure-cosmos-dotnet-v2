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
        private static readonly string databaseId = ConfigurationManager.AppSettings["DatabaseId"];
        private static readonly string endpointUrl = ConfigurationManager.AppSettings["EndPointUrl"];
        private static readonly string authorizationKey = ConfigurationManager.AppSettings["AuthorizationKey"];
        private static readonly ConnectionPolicy connectionPolicy = new ConnectionPolicy { UserAgentSuffix = " samples-net/3" };

        //Reusable instance of DocumentClient which represents the connection to a DocumentDB endpoint
        private static DocumentClient client;

        public static void Main(string[] args)
        {
            try
            {   
                //Connect to DocumentDB
                //Setup a single instance of DocumentClient that is reused throughout the application
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

        private static async Task RunDatabaseDemo()
        {
            //********************************************************************************************************
            // 1 -  Query for a Database
            //
            // Note: we are using query here instead of ReadDatabaseAsync because we're checking if something exists
            //       the ReadDatabaseAsync method expects the resource to be there, if its not we will get an error
            //       instead of an empty 
            //********************************************************************************************************
            Database database = client.CreateDatabaseQuery().Where(db => db.Id == databaseId).AsEnumerable().FirstOrDefault();
            Console.WriteLine("1. Query for a database returned: {0}", database==null?"no results":database.Id);

            //check if a database was returned
            if (database==null)
            {
                //**************************
                // 2 -  Create a Database
                //**************************
                database = await client.CreateDatabaseAsync(new Database { Id = databaseId });
                Console.WriteLine("\n2. Created Database: id - {0} and selfLink - {1}", database.Id, database.SelfLink);
            }

            //*********************************************************************************
            // 3 - Get a single database
            // Note: that we don't need to use the SelfLink of a Database anymore
            //       the links for a resource are now comprised of their Id properties
            //       using UriFactory will give you the correct URI for a resource
            //
            //       SelfLink will still work if you're already using this
            //********************************************************************************
            database = await client.ReadDatabaseAsync(UriFactory.CreateDatabaseUri(databaseId));
            Console.WriteLine("\n3. Read a database resource: {0}", database);

            //***************************************
            // 4 - List all databases for an account
            //***************************************
            var databases = await client.ReadDatabaseFeedAsync();
            Console.WriteLine("\n4. Reading all databases resources for an account");
            foreach (var db in databases)
            {
                Console.WriteLine(db);    
            }

            //*************************************
            // 5 - Delete a Database using its Id
            //*************************************
            await client.DeleteDatabaseAsync(UriFactory.CreateDatabaseUri(databaseId));
            Console.WriteLine("\n5. Database {0} deleted.", database.Id);
        }        
    }
}
