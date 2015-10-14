namespace DocumentDB.Samples.Shared.Util
{
    using System;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Documents.Linq;
    using System.IO;
    using Newtonsoft.Json;

    /// <summary>
    /// Providers common helper methods for working with DocumentClient.
    /// </summary>
    public class DocumentClientHelper
    {
        /// <summary>
        /// Get a Database by id, or create a new one if one with the id provided doesn't exist.
        /// </summary>
        /// <param name="client">The DocumentDB client instance.</param>
        /// <param name="id">The id of the Database to search for, or create.</param>
        /// <returns>The matched, or created, Database object</returns>
        public static async Task<Database> GetDatabaseAsync(DocumentClient client, string id)
        {
            var database = client.CreateDatabaseQuery().Where(db => db.Id == id).ToArray().FirstOrDefault();
            if (database == null)
            {
                database = await client.CreateDatabaseAsync(new Database { Id = id });
            }

            return database;
        }

        /// <summary>
        /// Get a Database by id, or create a new one if one with the id provided doesn't exist.
        /// </summary>
        /// <param name="client">The DocumentDB client instance.</param>
        /// <param name="id">The id of the Database to search for, or create.</param>
        /// <returns>The matched, or created, Database object</returns>
        public static async Task<Database> GetNewDatabaseAsync(DocumentClient client, string id)
        {
            var database = client.CreateDatabaseQuery().Where(db => db.Id == id).ToArray().FirstOrDefault();
            if (database != null)
            {
                await client.DeleteDatabaseAsync(database.SelfLink);
            }

            database = await client.CreateDatabaseAsync(new Database { Id = id });
            return database;
        }

        /// <summary>
        /// Get a DocumentCollection by id, or create a new one if one with the id provided doesn't exist.
        /// </summary>
        /// <param name="client">The DocumentDB client instance.</param>
        /// <param name="database">The Database where this DocumentCollection exists / will be created</param>
        /// <param name="collectionId">The id of the DocumentCollection to search for, or create.</param>
        /// <returns>The matched, or created, DocumentCollection object</returns>
        public static async Task<DocumentCollection> GetCollectionAsync(DocumentClient client, Database database, string collectionId)
        {
            var collection = client.CreateDocumentCollectionQuery(database.SelfLink)
                .Where(c => c.Id == collectionId).ToArray().FirstOrDefault();

            if (collection == null)
            {
                collection = await CreateDocumentCollectionWithRetriesAsync(client, database, new DocumentCollection { Id = collectionId });
            }

            return collection;
        }

        /// <summary>
        /// Get a DocumentCollection by id, or create a new one if one with the id provided doesn't exist.
        /// </summary>
        /// <param name="client">The DocumentDB client instance.</param>
        /// <param name="database">The Database where this DocumentCollection exists / will be created</param>
        /// <param name="collectionId">The id of the DocumentCollection to search for, or create.</param>
        /// <param name="collectionSpec">The spec/template to create collections from.</param>
        /// <returns>The matched, or created, DocumentCollection object</returns>
        public static async Task<DocumentCollection> GetCollectionAsync(
            DocumentClient client,
            Database database,
            string collectionId,
            DocumentCollectionSpec collectionSpec)
        {
            var collection = client.CreateDocumentCollectionQuery(database.SelfLink)
                .Where(c => c.Id == collectionId).ToArray().FirstOrDefault();

            if (collection == null)
            {
                collection = await CreateNewCollection(client, database, collectionId, collectionSpec);
            }

            return collection;
        }

        /// <summary>
        /// Creates a new collection.
        /// </summary>
        /// <param name="client">The DocumentDB client instance.</param>
        /// <param name="database">The Database where this DocumentCollection exists / will be created</param>
        /// <param name="collectionId">The id of the DocumentCollection to search for, or create.</param>
        /// <param name="collectionSpec">The spec/template to create collections from.</param>
        /// <returns>The created DocumentCollection object</returns>
        public static async Task<DocumentCollection> CreateNewCollection(
            DocumentClient client, 
            Database database, 
            string collectionId, 
            DocumentCollectionSpec collectionSpec)
        {
            var collectionDefinition = new DocumentCollection { Id = collectionId };
            if (collectionSpec != null)
            {
                CopyIndexingPolicy(collectionSpec, collectionDefinition);
            }

            var collection = await CreateDocumentCollectionWithRetriesAsync(
                client, 
                database, 
                collectionDefinition,
                (collectionSpec != null) ? collectionSpec.OfferType : null);

            if (collectionSpec != null)
            {
                await RegisterScripts(client, collectionSpec, collection);
            }

            return collection;
        }

        /// <summary>
        /// Registers the stored procedures, triggers and UDFs in the collection spec/template.
        /// </summary>
        /// <param name="client">The DocumentDB client.</param>
        /// <param name="collectionSpec">The collection spec/template.</param>
        /// <param name="collection">The collection.</param>
        /// <returns>The Task object for asynchronous execution.</returns>
        public static async Task RegisterScripts(DocumentClient client, DocumentCollectionSpec collectionSpec, DocumentCollection collection)
        {
            if (collectionSpec.StoredProcedures != null)
            {
                foreach (var sproc in collectionSpec.StoredProcedures)
                {
                    await client.CreateStoredProcedureAsync(collection.SelfLink, sproc);
                }
            }

            if (collectionSpec.Triggers != null)
            {
                foreach (var trigger in collectionSpec.Triggers)
                {
                    await client.CreateTriggerAsync(collection.SelfLink, trigger);
                }
            }

            if (collectionSpec.UserDefinedFunctions != null)
            {
                foreach (var udf in collectionSpec.UserDefinedFunctions)
                {
                    await client.CreateUserDefinedFunctionAsync(collection.SelfLink, udf);
                }
            }
        }

        /// <summary>
        /// Copies the indexing policy from the collection spec.
        /// </summary>
        /// <param name="collectionSpec">The collection spec/template</param>
        /// <param name="collectionDefinition">The collection definition to create.</param>
        public static void CopyIndexingPolicy(DocumentCollectionSpec collectionSpec, DocumentCollection collectionDefinition)
        {
            if (collectionSpec.IndexingPolicy != null)
            {
                collectionDefinition.IndexingPolicy.Automatic = collectionSpec.IndexingPolicy.Automatic;
                collectionDefinition.IndexingPolicy.IndexingMode = collectionSpec.IndexingPolicy.IndexingMode;

                if (collectionSpec.IndexingPolicy.IncludedPaths != null)
                {
                    foreach (var path in collectionSpec.IndexingPolicy.IncludedPaths)
                    {
                        collectionDefinition.IndexingPolicy.IncludedPaths.Add(path);
                    }
                }

                if (collectionSpec.IndexingPolicy.ExcludedPaths != null)
                {
                    foreach (var path in collectionSpec.IndexingPolicy.ExcludedPaths)
                    {
                        collectionDefinition.IndexingPolicy.ExcludedPaths.Add(path);
                    }
                }
            }
        }

        /// <summary>
        /// Create a DocumentCollection, and retry when throttled.
        /// </summary>
        /// <param name="client">The DocumentDB client instance.</param>
        /// <param name="database">The database to use.</param>
        /// <param name="collectionDefinition">The collection definition to use.</param>
        /// <param name="offerType">The offer type for the collection.</param>
        /// <returns>The created DocumentCollection.</returns>
        public static async Task<DocumentCollection> CreateDocumentCollectionWithRetriesAsync(
            DocumentClient client,
            Database database,
            DocumentCollection collectionDefinition,
            string offerType = "S1")
        {
            return await ExecuteWithRetries(
                client,
                () => client.CreateDocumentCollectionAsync(
                        database.SelfLink,
                        collectionDefinition,
                        new RequestOptions 
                        { 
                            OfferType = offerType 
                        }));
        }

        /// <summary>
        /// Execute the function with retries on throttle.
        /// </summary>
        /// <typeparam name="TValue">The type of return value from the execution.</typeparam>
        /// <param name="client">The DocumentDB client instance.</param>
        /// <param name="function">The function to execute.</param>
        /// <returns>The response from the execution.</returns>
        public static async Task<TValue> ExecuteWithRetries<TValue>(DocumentClient client, Func<Task<TValue>> function)
        {
            while (true)
            {
                TimeSpan sleepTime;
                try
                {
                    return await function();
                }
                catch (DocumentClientException de)
                {
                    if (!de.StatusCode.HasValue || (int)de.StatusCode != 429)
                    {
                        throw;
                    }

                    sleepTime = de.RetryAfter;
                }
                catch (AggregateException ae)
                {
                    if (!(ae.InnerException is DocumentClientException))
                    {
                        throw;
                    }

                    var de = (DocumentClientException)ae.InnerException;
                    if (!de.StatusCode.HasValue || (int)de.StatusCode != 429)
                    {
                        throw;
                    }

                    sleepTime = de.RetryAfter;
                }

                await Task.Delay(sleepTime);
            }
        }

        /// <summary>
        /// Bulk import using a stored procedure.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="collection"></param>
        /// <param name="inputDirectory"></param>
        /// <param name="inputFileMask"></param>
        /// <returns></returns>
        public static async Task RunBulkImport(
            DocumentClient client,
            DocumentCollection collection,
            string inputDirectory,
            string inputFileMask = "*.json")
        {
            var maxFiles = 2000;
            var maxScriptSize = 50000;

            // 1. Get the files. 
            var fileNames = Directory.GetFiles(inputDirectory, inputFileMask);

            var currentCount = 0;
            var fileCount = maxFiles != 0 ? Math.Min(maxFiles, fileNames.Length) : fileNames.Length;

            var body = File.ReadAllText(@".\JS\BulkImport.js");
            var sproc = new StoredProcedure
            {
                Id = "BulkImport",
                Body = body
            };

            await TryDeleteStoredProcedure(client, collection, sproc.Id);
            StoredProcedure sp = await ExecuteWithRetries(client, () => client.CreateStoredProcedureAsync(collection.SelfLink, sproc));

            while (currentCount < fileCount)
            {
                var argsJson = CreateBulkInsertScriptArguments(fileNames, currentCount, fileCount, maxScriptSize);
                var args = new [] { JsonConvert.DeserializeObject<dynamic>(argsJson) };

                var scriptResult = await ExecuteWithRetries(client, () => client.ExecuteStoredProcedureAsync<int>(sp.SelfLink, args));

                var currentlyInserted = scriptResult.Response;
                currentCount += currentlyInserted;
            }
        }

        public static async Task TryDeleteStoredProcedure(DocumentClient client, DocumentCollection collection, string sprocId)
        {
            var sproc = client.CreateStoredProcedureQuery(collection.SelfLink).Where(s => s.Id == sprocId).AsEnumerable().FirstOrDefault();
            if (sproc != null)
            {
                await ExecuteWithRetries(client, () => client.DeleteStoredProcedureAsync(sproc.SelfLink));
            }
        }

        /// <summary> 
        /// Creates the script for insertion 
        /// </summary>
        /// <param name="docFileNames">the document files to be inserted</param>
        /// <param name="currentIndex">the current number of documents inserted. this marks the starting point for this script</param>
        /// <param name="maxCount">the maximum units to be inserted.</param>
        /// <param name="maxScriptSize">the maximum number of characters that the script can have</param> 
        /// <returns>Script as a string</returns> 
        private static string CreateBulkInsertScriptArguments(string[] docFileNames, int currentIndex, int maxCount, int maxScriptSize)
        {
            var jsonDocumentArray = new StringBuilder();
            jsonDocumentArray.Append("[");

            if (currentIndex >= maxCount)
            {
                return string.Empty;
            }

            jsonDocumentArray.Append(File.ReadAllText(docFileNames[currentIndex]));

            var scriptCapacityRemaining = maxScriptSize;

            var i = 1;
            while (jsonDocumentArray.Length < scriptCapacityRemaining && (currentIndex + i) < maxCount)
            {
                jsonDocumentArray.Append(", " + File.ReadAllText(docFileNames[currentIndex + i]));
                i++;
            }

            jsonDocumentArray.Append("]");
            return jsonDocumentArray.ToString();
        }
    }
}
