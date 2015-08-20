namespace DocumentDB.Samples.Queries.JavaScript
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using DocumentDB.Samples.Shared;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Documents.Linq;
    using Microsoft.Azure.Documents.Spatial;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    /// <summary>
    /// This sample demonstrates the use of JavaScript language integrated queries with Azure DocumentDB.
    /// </summary>
    public class Program
    {
        /// <summary>
        /// Gets the database ID to use for the demo.
        /// </summary>
        private static readonly string DatabaseId = ConfigurationManager.AppSettings["DatabaseId"];

        /// <summary>
        /// Gets the collection ID to use for the demo.
        /// </summary>
        private static readonly string CollectionId = ConfigurationManager.AppSettings["CollectionId"];

        /// <summary>
        /// Gets the DocumentDB endpoint to use for the demo.
        /// </summary>
        private static readonly string EndpointUrl = ConfigurationManager.AppSettings["EndPointUrl"];

        /// <summary>
        /// Gets the DocumentDB authorization key to use for the demo.
        /// </summary>
        private static readonly string AuthorizationKey = ConfigurationManager.AppSettings["AuthorizationKey"];

        /// <summary>
        /// Gets the client to use.
        /// </summary>
        private static DocumentClient client;

        /// <summary>
        /// The main method to use.
        /// </summary>
        /// <param name="args">The command line arguments.</param>
        public static void Main(string[] args)
        {
            try
            {
                // Get a Document client
                using (client = new DocumentClient(new Uri(EndpointUrl), AuthorizationKey))
                {
                    RunDemoAsync(DatabaseId, CollectionId).Wait();
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
        /// Run the JavaScript language integrated queries samples.
        /// </summary>
        /// <param name="databaseId">The database Id.</param>
        /// <param name="collectionId">The collection Id.</param>
        /// <returns>The Task for asynchronous execution.</returns>
        private static async Task RunDemoAsync(string databaseId, string collectionId)
        {
            Database database = await GetDatabaseAsync(databaseId);

            // Create a new collection, or modify an existing one to enable spatial indexing.
            DocumentCollection collection = await GetCollection(database.SelfLink, collectionId);

            await CreateDocumentAsync(collection.SelfLink, "{ firstName: 'Ryan', lastName: 'Crawcour', address: { country: 'NZ' }}");
            await CreateDocumentAsync(collection.SelfLink, "{ firstName: 'Aravind', lastName: 'Ramachandran', address: { city: 'Kirkland', state: 'WA' }}");
            await CreateDocumentAsync(collection.SelfLink, "{ firstName: 'Andrew', lastName: 'Liu', address: { city: 'Seattle', state: 'WA' }}");

            // Run a simple filter query
            object filterQueryResult = await QueryScalar(
                collection.SelfLink,
                @"__.filter(function(person) { return person.firstName === 'Andrew'; })");
            
            Console.WriteLine("Filter query returned: {0}", filterQueryResult);

            // Run a map (projection) query
            object projectionQueryResult = await QueryScalar(
                collection.SelfLink,
                @"__.map(function(person) { return { familyName: person.lastName, address: person.address }; })");

            Console.WriteLine("Projection query returned: {0}", JsonConvert.SerializeObject(projectionQueryResult, Formatting.Indented));

            // Run a query using filter and map (using chaining)
            object chainQueryResult = await QueryScalar(
                collection.SelfLink,
                @"__.chain()
                    .filter(function(person) { return person.firstName === 'Andrew'; })
                    .map(function(person) { return { familyName: person.lastName, address: person.address }; })
                    .value()");

            Console.WriteLine("Chain (filter & projection) query returned: {0}", JsonConvert.SerializeObject(chainQueryResult, Formatting.Indented));

            // Run a chained filter, map and sorting (using chaining)
            object sortQueryResult = await QueryScalar(
                collection.SelfLink,
                @"__.chain()
                    .filter(function(person) { return person.firstName === 'Andrew' || person.firstName === 'Ryan'; })
                    .sortBy(function(person) { return person.lastName; })
                    .map(function(person) { return { familyName: person.lastName, address: person.address }; })
                    .value()");

            Console.WriteLine("Chain (filter, sort & projection) query returned: {0}", JsonConvert.SerializeObject(sortQueryResult, Formatting.Indented));

            await Cleanup(collection);
        }

        /// <summary>
        /// Get a Database for this id. Delete if it already exists.
        /// </summary>
        /// <param name="id">The id of the Database to create.</param>
        /// <returns>The created Database object</returns>
        private static async Task<Database> GetDatabaseAsync(string id)
        {
            Database database = client.CreateDatabaseQuery().Where(c => c.Id == id).ToArray().FirstOrDefault();
            if (database != null)
            {
                return database;
            }

            Console.WriteLine("Creating new database...");
            database = await client.CreateDatabaseAsync(new Database { Id = id });

            return database;
        }

        /// <summary>
        /// Get a DocumentCollection by id, or create a new one if one with the id provided doesn't exist. 
        /// </summary>
        /// <param name="databaseLink">The database self-link to use.</param>
        /// <param name="id">The id of the DocumentCollection to search for, or create.</param>
        /// <returns>The matched, or created, DocumentCollection object</returns>
        private static async Task<DocumentCollection> GetCollection(string databaseLink, string id)
        {
            DocumentCollection collection = client.CreateDocumentCollectionQuery(databaseLink)
                .Where(c => c.Id == id)
                .AsEnumerable()
                .FirstOrDefault();

            if (collection != null)
            {
                return collection;
            }

            Console.WriteLine("Creating new collection...");
            
            collection = await client.CreateDocumentCollectionAsync(
                databaseLink, 
                new DocumentCollection 
                { 
                    Id = id, 
                    IndexingPolicy = new IndexingPolicy(new RangeIndex(DataType.String) { Precision = -1 }) 
                });

            return collection;
        }

        /// <summary>
        /// Run a query that returns a single document, and display it
        /// </summary>
        /// <param name="collectionLink">The collection self-link</param>
        /// <param name="javascriptQuery">The query to run</param>
        /// <returns>The result of the query as an object.</returns>
        private static async Task<object> QueryScalar(string collectionLink, string javascriptQuery)
        {
            // JavaScript integrated queries are supported using the server side SDK, so you'll be using
            // them within stored procedures and triggers. Here we show them standalone just to demonstrate
            // how to use the functional-Underscore.js style query API
            string javaScriptFunctionStub = string.Format("function() {{ {0}; }}", javascriptQuery);
            string singleQuerySprocName = "query";

            StoredProcedure currentProcedure = client.CreateStoredProcedureQuery(collectionLink)
                .Where(s => s.Id == singleQuerySprocName)
                .AsEnumerable()
                .FirstOrDefault();

            if (currentProcedure != null)
            {
                currentProcedure.Body = javaScriptFunctionStub;
                await client.ReplaceStoredProcedureAsync(currentProcedure);
            }
            else 
            {
                currentProcedure = await client.CreateStoredProcedureAsync(
                    collectionLink,
                    new StoredProcedure 
                    { 
                        Id = singleQuerySprocName, 
                        Body = javaScriptFunctionStub 
                    });
            }

            StoredProcedureResponse<object> result = await client.ExecuteStoredProcedureAsync<object>(currentProcedure.SelfLink);
            return result.Response;
        }

        /// <summary>
        /// Create a document from JSON string
        /// </summary>
        /// <param name="collectionLink">The collection self-link</param>
        /// <param name="json">The JSON to create</param>
        /// <returns>The Task for asynchronous execution.</returns>
        private static async Task CreateDocumentAsync(string collectionLink, string json)
        {
            using (System.IO.MemoryStream ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json)))
            {
                Document documentFromJson = Document.LoadFrom<Document>(ms);
                await client.CreateDocumentAsync(collectionLink, documentFromJson);
            }
        }

        /// <summary>
        /// Cleanup data from previous runs.
        /// </summary>
        /// <param name="collection">The DocumentDB collection.</param>
        /// <returns>The Task for asynchronous execution.</returns>
        private static async Task Cleanup(DocumentCollection collection)
        {
            Console.WriteLine("Cleaning up");
            foreach (Document d in await client.ReadDocumentFeedAsync(collection.SelfLink))
            {
                await client.DeleteDocumentAsync(d.SelfLink);
            }
        }
    }
}