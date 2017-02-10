namespace DocumentDB.Samples.UserManagement
{
    using DocumentDB.Samples.Shared.Util;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Documents.Linq;
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;

    //------------------------------------------------------------------------------------------------
    // This sample demonstrates the basic CRUD operations on a User resource for Azure DocumentDB
    //------------------------------------------------------------------------------------------------

    public class Program
    {
        private static DocumentClient client;

        //Assign a id for your database & collection 
        private static readonly string DatabaseName = "samples";
        private static readonly string CollectionName = "auth-samples";

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
                    RunDemoAsync(DatabaseName, CollectionName).Wait();
                }
            }
#if !DEBUG
            catch (Exception e)
            {
                LogException(e);
            }
#endif
            finally
            {
                Console.WriteLine("End of demo, press any key to exit.");
                Console.ReadKey();
            }
        }

        private static async Task RunDemoAsync(string databaseId, string collectionId)
        {
            //--------------------------------------------------------------------------------------------------
            // We need a Database, Two Collections, Two Users, and some permissions for this sample,
            // So let's go ahead and set these up initially
            //--------------------------------------------------------------------------------------------------

            // Get, or Create, the Database
            Database db = await client.CreateDatabaseIfNotExistsAsync(new Database { Id = databaseId });
            Database db2 = await client.CreateDatabaseIfNotExistsAsync(new Database { Id = "auth-samples-2" });

            // Get, or Create, two seperate Collections
            DocumentCollection collection1 = new DocumentCollection();
            collection1.Id = "COL1";
            collection1.PartitionKey.Paths.Add("/partitionKey");

            collection1 = await client.CreateDocumentCollectionAsync(
                UriFactory.CreateDatabaseUri(databaseId),
                collection1,
                new RequestOptions { OfferThroughput = 400 });

            DocumentCollection collection2 = new DocumentCollection();
            collection2.Id = "COL2";
            collection2.PartitionKey.Paths.Add("/partitionKey");

            collection2 = await client.CreateDocumentCollectionAsync(
                UriFactory.CreateDatabaseUri(databaseId),
                collection2,
                new RequestOptions { OfferThroughput = 400 });

            // Insert two documents in to col1
            Document doc1 = await client.CreateDocumentAsync(collection1.DocumentsLink, new { id = "doc1", partitionKey = "partitionKey1" });
            Document doc2 = await client.CreateDocumentAsync(collection1.DocumentsLink, new { id = "doc2", partitionKey = "pk2" });

            // Insert one document in to col2
            Document doc3 = await client.CreateDocumentAsync(collection2.DocumentsLink, new { id = "doc3" });

            // Create two users
            User user1 = await client.CreateUserAsync(db.UsersLink, new User { Id = "Thomas Andersen" });
            User user2 = await client.CreateUserAsync(db.UsersLink, new User { Id = "Robin Wakefield" });

            // Read Permission on col1 for user1
            Permission permissionUser1Col1 = await CreatePermissionAsync(collection1.SelfLink, user1.SelfLink, PermissionMode.Read);

            // All Permissions on Doc1 for user1
            Permission permissionUser1Doc1 = await CreatePermissionAsync(doc1.SelfLink, user1.SelfLink, PermissionMode.All, "partitionKey1");

            // Read Permissions on col2 for user1
            Permission permissionUser1Col2 = await CreatePermissionAsync(collection2.SelfLink, user1.SelfLink, PermissionMode.Read);

            // All Permissions on col2 for user2
            Permission permissionUser2Col2 = await CreatePermissionAsync(collection2.SelfLink, user2.SelfLink, PermissionMode.All);
            
            // All user1's permissions in a List
            List<Permission> user1Permissions = await GetUserPermissionsAsync(user1.SelfLink);

            //--------------------------------------------------------------------------------------------------
            // That takes care of the creating Users, Permissions on Resources, Linking user to permissions etc. 
            // Now let's take a look at the result of User.Id = 1 having ALL permission on a single Collection
            // but not on anything else
            //----------------------------------------------------------------------------------------------------

            //Attempt to do admin operations when user only has Read on a collection
            await AttemptAdminOperationsAsync(collection1.SelfLink, permissionUser1Col1);

            //Attempt a write Document with read-only Collection permission
            await AttemptWriteWithReadPermissionAsync(collection1.SelfLink, permissionUser1Col1);

            //Attempt to read across multiple collections
            await AttemptReadFromTwoCollections(new List<string> { collection1.SelfLink, collection2.SelfLink }, user1Permissions);
            
            // Uncomment to Cleanup 
            // await client.DeleteDatabaseAsync(db.SelfLink);
            // await client.DeleteDatabaseAsync(db2.SelfLink);
        }
        
        private static async Task<Permission> CreatePermissionAsync(string resourceLink, string userLink, PermissionMode mode, string resourcePartitionKey = null)
        {
            Permission permission = new Permission
            {
                Id = Guid.NewGuid().ToString("N"),
                PermissionMode = mode,
                ResourceLink = resourceLink
            };

            if (resourcePartitionKey != null)
            {
                permission.ResourcePartitionKey = new PartitionKey(resourcePartitionKey);
            }

            ResourceResponse<Permission> response = await DocumentClientHelper.ExecuteWithRetries<ResourceResponse<Permission>>(
                client, 
                () => client.CreatePermissionAsync(userLink, permission));

            return response.Resource;
        }
        
        private static async Task AttemptReadFromTwoCollections(List<string> collectionLinks, List<Permission> permissions)
        {
            //Now, we're going to use multiple permission tokens.
            //In this case, a read Permission on col1 AND another read Permission for col2
            //This means the user should be able to read from both col1 and col2, but not have 
            //the ability to read other collections should they exist, nor any admin access.
            //the user will also not have permission to write in either collection            
            using (DocumentClient client = new DocumentClient(new Uri(endpointUrl), permissions))
            {
                FeedResponse<dynamic> response;

                //read collection 1 > should succeed
                response = await client.ReadDocumentFeedAsync(collectionLinks[0]);

                //read from collection 2 > should succeed
                response = await client.ReadDocumentFeedAsync(collectionLinks[1]);

                //attempt to write a doc in col 2 > should fail with Forbidden
                try
                {
                    await client.UpsertDocumentAsync(collectionLinks[1], new { id = "not allowed" });

                    //should never get here, because we expect the create to fail
                    throw new ApplicationException("should never get here");
                }
                catch (DocumentClientException de)
                {
                    //expecting an Forbidden exception, anything else, rethrow
                    if (de.StatusCode != HttpStatusCode.Forbidden) throw;
                }
            }

            return;
        }
        
        private static async Task AttemptWriteWithReadPermissionAsync(string collectionLink, Permission permission)
        {            
            using (DocumentClient client = new DocumentClient( new Uri(endpointUrl), permission.Token))
            {
                //attempt to write a document > should fail
                try
                {
                    await client.UpsertDocumentAsync(collectionLink, new { id = "not allowed" });

                    //should never get here, because we expect the create to fail
                    throw new ApplicationException("should never get here");
                }
                catch (DocumentClientException de)
                {
                    //expecting an Forbidden exception, anything else, rethrow
                    if (de.StatusCode != HttpStatusCode.Forbidden) throw;
                }
            }
        }
        
        private static async Task AttemptAdminOperationsAsync(string collectionLink, Permission permission)
        {
            using (DocumentClient client = new DocumentClient(new Uri(endpointUrl), permission.Token))
            {
                //try read collection > should succeed because user1 was granted Read permission on col1
                var docs = await client.ReadDocumentFeedAsync(collectionLink);
                foreach (Document doc in docs)
                {
                    Console.WriteLine(doc);
                }

                //try iterate databases > should fail because the user has no Admin rights 
                //but only read access to a single collection and therefore
                //cannot access anything outside of that collection.
                try
                {
                    var databases = await client.ReadDatabaseFeedAsync();
                    foreach (Database database in databases) { throw new ApplicationException("Should never get here"); }
                }
                catch (DocumentClientException de)
                {
                    //expecting an Unauthorised exception, anything else, rethrow
                    if (de.StatusCode != HttpStatusCode.Forbidden) throw;
                }
            }
        }
        
        public static async Task<List<Permission>> GetUserPermissionsAsync(string userLink)
        {
            List<Permission> listOfPermissions = new List<Permission>();

            //get user resource from the user link
            User user = await client.ReadUserAsync(userLink);

            FeedResponse<Permission> permissions = await client.ReadPermissionFeedAsync(userLink);
            foreach (var permission in permissions)
            {
                listOfPermissions.Add(permission);
            }

            return listOfPermissions;
        }

        /// <summary>
        /// Log exception error message to the console
        /// </summary>
        /// <param name="e">The caught exception.</param>
        private static void LogException(Exception e)
        {
            ConsoleColor color = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;

            Exception baseException = e.GetBaseException();
            if (e is DocumentClientException)
            {
                DocumentClientException de = (DocumentClientException)e;
                Console.WriteLine("{0} error occurred: {1}, Message: {2}", de.StatusCode, de.Message, baseException.Message);
            }
            else
            {
                Console.WriteLine("Error: {0}, Message: {1}", e.Message, baseException.Message);
            }

            Console.ForegroundColor = color;
        }
    }
}
