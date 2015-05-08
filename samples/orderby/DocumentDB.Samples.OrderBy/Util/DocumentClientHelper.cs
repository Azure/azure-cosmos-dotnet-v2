namespace DocumentDB.Samples.OrderBy
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Documents.Linq;

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
            Database database = client.CreateDatabaseQuery().Where(db => db.Id == id).ToArray().FirstOrDefault();
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
            Database database = client.CreateDatabaseQuery().Where(db => db.Id == id).ToArray().FirstOrDefault();
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
            DocumentCollection collection = client.CreateDocumentCollectionQuery(database.SelfLink)
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
        /// <param name="collectionInfo">The spec/template to create collections from.</param>
        /// <returns>The matched, or created, DocumentCollection object</returns>
        public static async Task<DocumentCollection> GetCollectionAsync(
            DocumentClient client,
            Database database,
            string collectionId,
            DocumentCollectionInfo collectionInfo)
        {
            DocumentCollection collection = client.CreateDocumentCollectionQuery(database.SelfLink)
                .Where(c => c.Id == collectionId).ToArray().FirstOrDefault();

            if (collection == null)
            {
                collection = await CreateNewCollection(client, database, collectionId, collectionInfo);
            }

            return collection;
        }

        /// <summary>
        /// Creates a new collection.
        /// </summary>
        /// <param name="client">The DocumentDB client instance.</param>
        /// <param name="database">The Database where this DocumentCollection exists / will be created</param>
        /// <param name="collectionId">The id of the DocumentCollection to search for, or create.</param>
        /// <param name="collectionInfo">The spec/template to create collections from.</param>
        /// <returns>The created DocumentCollection object</returns>
        public static async Task<DocumentCollection> CreateNewCollection(
            DocumentClient client,
            Database database,
            string collectionId,
            DocumentCollectionInfo collectionInfo)
        {
            DocumentCollection collectionDefinition = new DocumentCollection { Id = collectionId };
            if (collectionInfo != null)
            {
                CopyIndexingPolicy(collectionInfo, collectionDefinition);
            }

            DocumentCollection collection = await CreateDocumentCollectionWithRetriesAsync(
                client,
                database,
                collectionDefinition,
                (collectionInfo != null) ? collectionInfo.OfferType : null);

            if (collectionInfo != null)
            {
                await RegisterScripts(client, collectionInfo, collection);
            }

            return collection;
        }

        /// <summary>
        /// Registers the stored procedures, triggers and UDFs in the collection spec/template.
        /// </summary>
        /// <param name="client">The DocumentDB client.</param>
        /// <param name="collectionInfo">The collection spec/template.</param>
        /// <param name="collection">The collection.</param>
        /// <returns>The Task object for asynchronous execution.</returns>
        public static async Task RegisterScripts(DocumentClient client, DocumentCollectionInfo collectionInfo, DocumentCollection collection)
        {
            if (collectionInfo.StoredProcedures != null)
            {
                foreach (StoredProcedure sproc in collectionInfo.StoredProcedures)
                {
                    await client.CreateStoredProcedureAsync(collection.SelfLink, sproc);
                }
            }

            if (collectionInfo.Triggers != null)
            {
                foreach (Trigger trigger in collectionInfo.Triggers)
                {
                    await client.CreateTriggerAsync(collection.SelfLink, trigger);
                }
            }

            if (collectionInfo.UserDefinedFunctions != null)
            {
                foreach (UserDefinedFunction udf in collectionInfo.UserDefinedFunctions)
                {
                    await client.CreateUserDefinedFunctionAsync(collection.SelfLink, udf);
                }
            }
        }

        /// <summary>
        /// Copies the indexing policy from the collection spec.
        /// </summary>
        /// <param name="collectionInfo">The collection spec/template</param>
        /// <param name="collectionDefinition">The collection definition to create.</param>
        public static void CopyIndexingPolicy(DocumentCollectionInfo collectionInfo, DocumentCollection collectionDefinition)
        {
            if (collectionInfo.IndexingPolicy != null)
            {
                collectionDefinition.IndexingPolicy.Automatic = collectionInfo.IndexingPolicy.Automatic;
                collectionDefinition.IndexingPolicy.IndexingMode = collectionInfo.IndexingPolicy.IndexingMode;

                if (collectionInfo.IndexingPolicy.IncludedPaths != null)
                {
                    foreach (IndexingPath path in collectionInfo.IndexingPolicy.IncludedPaths)
                    {
                        collectionDefinition.IndexingPolicy.IncludedPaths.Add(path);
                    }
                }

                if (collectionInfo.IndexingPolicy.ExcludedPaths != null)
                {
                    foreach (string path in collectionInfo.IndexingPolicy.ExcludedPaths)
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
        /// <typeparam name="V">The type of return value from the execution.</typeparam>
        /// <param name="client">The DocumentDB client instance.</param>
        /// <param name="function">The function to execute.</param>
        /// <returns>The response from the execution.</returns>
        public static async Task<V> ExecuteWithRetries<V>(DocumentClient client, Func<Task<V>> function)
        {
            TimeSpan sleepTime = TimeSpan.Zero;

            while (true)
            {
                try
                {
                    return await function();
                }
                catch (DocumentClientException de)
                {
                    if ((int)de.StatusCode != 429)
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

                    DocumentClientException de = (DocumentClientException)ae.InnerException;
                    if ((int)de.StatusCode != 429)
                    {
                        throw;
                    }

                    sleepTime = de.RetryAfter;
                }

                await Task.Delay(sleepTime);
            }
        }
    }
}