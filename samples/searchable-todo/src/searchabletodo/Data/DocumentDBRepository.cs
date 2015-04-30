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
            return Client.CreateDocumentQuery<T>(Collection.DocumentsLink)
                        .Where(predicate)
                        .AsEnumerable()
                        .FirstOrDefault();
        }

        public static T GetById(string id)
        {
            T doc = Client.CreateDocumentQuery<T>(Collection.SelfLink)
                .Where(d => d.Id == id)
                .AsEnumerable()
                .FirstOrDefault();

            return doc;
        }

        public static IEnumerable<T> Find(Expression<Func<T, bool>> predicate)
        {
            var ret = Client.CreateDocumentQuery<T>(Collection.SelfLink)
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

        private static string databaseId;
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

        private static string collectionId;
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

        private static Database database;
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

        private static DocumentCollection collection;
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

        private static DocumentClient client;
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
            var col = Client.CreateDocumentCollectionQuery(databaseLink)
                              .Where(c => c.Id == collectionId)
                              .AsEnumerable()
                              .FirstOrDefault();

            if (col == null)
            {
                col = client.CreateDocumentCollectionAsync(databaseLink,
                    new DocumentCollection { Id = collectionId },
                    new RequestOptions { OfferType = "S1" }).Result;
            }

            return col;
        }
        public static Database GetOrCreateDatabase(string databaseId)
        {
            var db = Client.CreateDatabaseQuery()
                            .Where(d => d.Id == databaseId)
                            .AsEnumerable()
                            .FirstOrDefault();

            if (db == null)
            {
                db = client.CreateDatabaseAsync(new Database { Id = databaseId }).Result;
            }

            return db;
        }
    }
}