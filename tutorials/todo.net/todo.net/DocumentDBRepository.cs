namespace todo.net
{
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Documents.Linq;
    using Microsoft.WindowsAzure;
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Web;
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
                    ReadOrCreateDatabase().Wait();
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
                    ReadOrCreateCollection(Database.SelfLink).Wait();
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
                    collectionId = ConfigurationManager.AppSettings["database"];
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
                    String endpoint = ConfigurationManager.AppSettings["database"];
                    string authKey = ConfigurationManager.AppSettings["database"];

                    Uri endpointUri = new Uri(endpoint);
                    client = new DocumentClient(endpointUri, authKey);
                }
                return client;
            }
        }

        public static async Task<Document> CreateDocument(dynamic item)
        {
            return await Client.CreateDocumentAsync(Collection.SelfLink, item);
        }

        public static async Task<Item> GetDocument(string id)
        {
            return await Task<Item>.Run(() =>
                Client.CreateDocumentQuery<Item>(Collection.DocumentsLink)
                    .Where(d => d.ID == id)
                    .AsEnumerable().FirstOrDefault());
        }

        public static async Task<List<Item>> GetIncompleteItems()
        {
            return await Task<List<Item>>.Run(() =>
                Client.CreateDocumentQuery<Item>(Collection.DocumentsLink)
                        .Where(d => !d.Completed)
                        .AsEnumerable()
                        .ToList<Item>());
        }

        public static async Task<Document> UpdateDocument(Item item)
        {
            var doc = Client.CreateDocumentQuery<Document>(Collection.DocumentsLink)
                        .Where(d => d.Id == item.ID)
                        .AsEnumerable().FirstOrDefault();


            return await Client.ReplaceDocumentAsync(doc.SelfLink, item);
        }

        private static async Task ReadOrCreateCollection(string databaseLink)
        {
            var collections = Client.CreateDocumentCollectionQuery(databaseLink)
                              .Where(col => col.Id == CollectionId).ToArray();

            if (collections.Any())
            {
                collection = collections.First();
            }
            else
            {
                collection = await Client.CreateDocumentCollectionAsync(databaseLink,
                    new DocumentCollection { Id = CollectionId });
            }
        }

        private static async Task ReadOrCreateDatabase()
        {
            var query = Client.CreateDatabaseQuery()
                            .Where(db => db.Id == DatabaseId);

            var databases = query.ToArray();
            if (databases.Any())
            {
                database = databases.First();
            }
            else
            {
                database = await Client.CreateDatabaseAsync(new Database { Id = DatabaseId });
            }
        }
    }
}