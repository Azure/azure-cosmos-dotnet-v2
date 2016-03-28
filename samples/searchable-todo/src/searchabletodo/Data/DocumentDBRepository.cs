using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using System.Web;

namespace searchabletodo.Data
{
    public static class DocumentDBRepository<T> where T : Document
    {
        public static T Get(Expression<Func<T, bool>> predicate)
        {
            return Client.CreateDocumentQuery<T>(Collection.DocumentsLink, DefaultOptions)
                .Where(predicate)
                .AsEnumerable()
                .FirstOrDefault();
        }

        public static T GetById(string id)
        {
            T doc = Client.CreateDocumentQuery<T>(Collection.SelfLink, DefaultOptions)
                .Where(d => d.Id == id)
                .AsEnumerable()
                .FirstOrDefault();

            return doc;
        }

        public static IEnumerable<T> Find(Expression<Func<T, bool>> predicate)
        {
            var ret = Client.CreateDocumentQuery<T>(Collection.SelfLink, DefaultOptions)
                .Where(predicate)
                .AsEnumerable();

            return ret;
        }

        public static async Task<T> CreateAsync(T entity)
        {
            Document doc = await Client.CreateDocumentAsync(Collection.SelfLink, entity);

            T ret = (T)(dynamic)doc;
            return ret;
        }

        public static async Task<Document> UpdateAsync(string id, T entity)
        {
            Document doc = GetById(id);
            return await Client.ReplaceDocumentAsync(doc.SelfLink, entity);
        }

        public static async Task DeleteAsync(string id)
        {
            Document doc = GetById(id);
            await Client.DeleteDocumentAsync(doc.SelfLink);
        }

        public static String DatabaseId
        {
            get
            {
                if (string.IsNullOrEmpty(databaseId))
                {

                    databaseId = ConfigurationManager.AppSettings["docdb-database"];
                }

                return databaseId;
            }
        }

        public static String CollectionId
        {
            get
            {
                if (string.IsNullOrEmpty(collectionId))
                {

                    collectionId = ConfigurationManager.AppSettings["docdb-collection"];
                }

                return collectionId;
            }
        }

        private static Database Database
        {
            get
            {
                if (database == null)
                {

                    database = GetOrCreateDatabase(DatabaseId);
                }

                return database;
            }
        }

        private static DocumentCollection Collection
        {
            get
            {
                if (collection == null)
                {
                    collection = GetOrCreateCollection(Database.SelfLink, CollectionId);
                }

                return collection;
            }
        }

        private static DocumentClient Client
        {
            get
            {
                if (client == null)
                {
                    string endpoint = ConfigurationManager.AppSettings["docdb-endpoint"];
                    string authKey = ConfigurationManager.AppSettings["docdb-authKey"];

                    //the UserAgentSuffix on the ConnectionPolicy is being used to enable internal tracking metrics
                    //this is not requirted when connecting to DocumentDB but could be useful if you, like us, want to run 
                    //some monitoring tools to track usage by application
                    ConnectionPolicy connectionPolicy = new ConnectionPolicy {  UserAgentSuffix = " samples-net-searchabletodo/1" };
                    
                    client = new DocumentClient(new Uri(endpoint), authKey, connectionPolicy);
                }

                return client;
            }
        }

        public static DocumentCollection GetOrCreateCollection(string databaseLink, string collectionId)
        {
            var collection = Client.CreateDocumentCollectionQuery(databaseLink)
                .Where(c => c.Id == collectionId)
                .AsEnumerable()
                .FirstOrDefault();

            if (collection == null)
            {
                DocumentCollection collectionDefinition = new DocumentCollection();
                collectionDefinition.Id = collectionId;
                collectionDefinition.PartitionKey.Paths.Add("/title");
                collectionDefinition.IndexingPolicy = new IndexingPolicy(new RangeIndex(DataType.String) { Precision = -1 });

                collection = client.CreateDocumentCollectionAsync(
                    databaseLink,
                    collectionDefinition,
                    new RequestOptions { OfferThroughput = 500 }
                    ).Result;
            }

            return collection;
        }
        public static Database GetOrCreateDatabase(string databaseId)
        {
            Database database = Client.CreateDatabaseQuery()
                .Where(d => d.Id == databaseId)
                .AsEnumerable()
                .FirstOrDefault();

            if (database == null)
            {
                database = client.CreateDatabaseAsync(new Database { Id = databaseId }).Result;
            }

            return database;
        }

        private static FeedOptions DefaultOptions = new FeedOptions { EnableCrossPartitionQuery = true };
        private static string databaseId;
        private static string collectionId;
        private static Database database;
        private static DocumentCollection collection;
        private static DocumentClient client;
    }
}