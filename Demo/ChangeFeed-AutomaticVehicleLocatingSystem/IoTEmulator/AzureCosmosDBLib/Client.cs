namespace AzureCosmosDBLib
{
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Documents.Linq;
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Threading.Tasks;

    /*
     * 
     *  How to use this library 
     *  using AzureCosmosDBLib;
     *  Client <IoTData>.Initialize(ConfigurationManager.AppSettings["database"],
                                                          ConfigurationManager.AppSettings["collection"],
                                                          ConfigurationManager.AppSettings["endpoint"],
                                                          ConfigurationManager.AppSettings["authKey"]);

         await Client<IoTData>.CreateItemAsync(data);

         var response =  await Client<IoTData>.GetItemAsync("733b6279-52ce-4ec6-88dc-6cc975bf8f1d", "733b6279-52ce-4ec6-88dc-6cc975bf8f1d");
           
         var response = Client<IoTData>.QueryItem("733b6279-52ce-4ec6-88dc-6cc975bf8f1d");
          
     * 
     * 
     */

    public static class Client<T> where T : class
    {
       
        private static DocumentClient client;
        private static Uri collectionUri;
        private static string _database;
        private static string _collection;

        public static List<Document> QueryItem(string documentId)
        {
            try
            {
                return  client.CreateDocumentQuery<Document>(
                    UriFactory.CreateDocumentCollectionUri(_database, _collection)
                    , "select * from c where c.id = '" + documentId + "'"
                    , new FeedOptions { EnableCrossPartitionQuery = true }
                    ).ToList();
                
            }
            catch (DocumentClientException e)
            {
                if (e.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return null;
                }
                else
                {
                    throw;
                }
            }
        }

        /// <summary>
        /// Need the partition key
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public static async Task<T> GetItemAsync(string partitionKey, string documentId)
        {
            try
            {
                Document document = await client.ReadDocumentAsync( 
                    UriFactory.CreateDocumentUri(_database, _collection, documentId)
                    ,new RequestOptions {PartitionKey = new PartitionKey(partitionKey)}
                    );
                return (T)(dynamic)document;
            }
            catch (DocumentClientException e)
            {
                if (e.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return null;
                }
                else
                {
                    throw;
                }
            }
        }

        public static async Task<IEnumerable<T>> GetItemsAsync(Expression<Func<T, bool>> predicate, string documentId)
        {
            IDocumentQuery<T> query = client.CreateDocumentQuery<T>(UriFactory.CreateDocumentUri(_database, _collection, documentId),
                new FeedOptions { MaxItemCount = -1 })
                .Where(predicate)
                .AsDocumentQuery();

            List<T> results = new List<T>();
            while (query.HasMoreResults)
            {
                
                results.AddRange(await query.ExecuteNextAsync<T>());
            }

            return results;
        }

        public static async Task<Document> CreateItemAsync(T item)
        {
            return await client.CreateDocumentAsync(collectionUri, item);
        }

        public static async Task<Document> UpdateItemAsync(string id, T item)
        {
            
            return await client.ReplaceDocumentAsync(UriFactory.CreateDocumentUri(_database, _collection, null), item);
        }

        //TODO: Fix it
        public static async Task DeleteItemAsync(string id)
        {
            await client.DeleteDocumentAsync(string.Empty);
        }

        public static void Initialize(string database, string collection, string endpoint, string authkey, ConnectionPolicy connectionPolicy)
        {
            _database = database;
            _collection = collection;
            client = new DocumentClient(new Uri(endpoint), authkey, connectionPolicy);
            collectionUri = UriFactory.CreateDocumentCollectionUri(database, collection);
            CreateDatabaseIfNotExistsAsync( database).Wait();
            CreateCollectionIfNotExistsAsync(database, collection).Wait();
        }

        private static async Task CreateDatabaseIfNotExistsAsync(string dataBase)
        {
            try
            {
                await client.ReadDatabaseAsync(UriFactory.CreateDatabaseUri(dataBase));
            }
            catch (DocumentClientException e)
            {
                if (e.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    await client.CreateDatabaseAsync(new Database { Id = dataBase });
                }
                else
                {
                    throw;
                }
            }
        }

        private static async Task CreateCollectionIfNotExistsAsync(string dataBase, string collection)
        {
            try
            {
                await client.ReadDocumentCollectionAsync(UriFactory.CreateDocumentCollectionUri(dataBase, collection));
            }
            catch (DocumentClientException e)
            {
                if (e.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    await client.CreateDocumentCollectionAsync(
                        UriFactory.CreateDatabaseUri(dataBase),
                        new DocumentCollection { Id = collection },
                        new RequestOptions { OfferThroughput = 1000 });
                }
                else
                {
                    throw;
                }
            }
        }
    }
}