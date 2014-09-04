namespace todo.net
{
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Documents.Linq;
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Linq;
    using System.Threading.Tasks;
    using Todo.NET.Models;

    public class DocumentDBRepository
    {
        private static Database database;
        private static Database Database
        {
            get
            {
                if (database == null)
                {
                    ReadOrCreateDatabaseAsync().Wait();
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
                    ReadOrCreateCollectionAsync(Database.SelfLink).Wait();
                }

                return collection;
            }
        }

        private static string databaseId;
        private static string DatabaseId
        {
            get
            {
                if (string.IsNullOrEmpty(databaseId))
                {
                    databaseId = ConfigurationManager.AppSettings["database"];
                }

                return databaseId;
            }
        }

        private static string collectionId;
        private static string CollectionId
        {
            get
            {
                if (string.IsNullOrEmpty(collectionId))
                {
                    collectionId = ConfigurationManager.AppSettings["collection"];
                }

                return collectionId;
            }
        }

        private static DocumentClient client;
        private static DocumentClient Client
        {
            get
            {
                if (client == null)
                {
                    String endpoint = ConfigurationManager.AppSettings["endpoint"];
                    string authKey = ConfigurationManager.AppSettings["authkey"];

                    Uri endpointUri = new Uri(endpoint);
                    client = new DocumentClient(endpointUri, authKey);
                }
                return client;
            }
        }

        public static async Task<Document> CreateDocumentAsync(dynamic item)
        {
            return await Client.CreateDocumentAsync(Collection.SelfLink, item);
        }

        public static Item GetDocument(string id)
        {
            return Client.CreateDocumentQuery<Item>(Collection.DocumentsLink).Where(d => d.ID == id).AsEnumerable().FirstOrDefault(); ;
        }

        public static IEnumerable<Item> GetIncompleteItems()
        {
            return Client.CreateDocumentQuery<Item>(Collection.DocumentsLink).Where(d => !d.Completed).AsEnumerable();
        }

        public static async Task<Document> UpdateDocumentAsync(Item item)
        {
            Document doc = Client.CreateDocumentQuery<Document>(Collection.DocumentsLink)
                                .Where(d => d.Id == item.ID).AsEnumerable().FirstOrDefault(); ;
            
            return await Client.ReplaceDocumentAsync(doc.SelfLink, item);
        }

        private static async Task ReadOrCreateCollectionAsync(string databaseLink)
        {
            collection = Client.CreateDocumentCollectionQuery(databaseLink).Where(col => col.Id == CollectionId).AsEnumerable().FirstOrDefault(); ;
            if (collection == null)
            {
                collection = await Client.CreateDocumentCollectionAsync(databaseLink, new DocumentCollection { Id = CollectionId });
            }
        }

        private static async Task ReadOrCreateDatabaseAsync()
        {
            database = Client.CreateDatabaseQuery().Where(db => db.Id == DatabaseId).AsEnumerable().FirstOrDefault();            
            if (database == null)
            {
                database = await Client.CreateDatabaseAsync(new Database { Id = DatabaseId });
            }
        }
    }
}