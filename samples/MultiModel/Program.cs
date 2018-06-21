
namespace DocumentDB.Sample.MultiModel
{
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using System;
    using System.Configuration;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;

    class Program
    {
        private static readonly string DatabaseName = ConfigurationManager.AppSettings["DatabaseName"];
        private static readonly string DataCollectionName = ConfigurationManager.AppSettings["CollectionName"];
        private static readonly int CollectionThroughput = int.Parse(ConfigurationManager.AppSettings["CollectionThroughput"]);

        private static readonly ConnectionPolicy ConnectionPolicy = new ConnectionPolicy
        {
            ConnectionMode = ConnectionMode.Direct,
            ConnectionProtocol = Protocol.Tcp,
            RequestTimeout = new TimeSpan(1, 0, 0),
            MaxConnectionLimit = 1000,
            RetryOptions = new RetryOptions
            {
                MaxRetryAttemptsOnThrottledRequests = 10,
                MaxRetryWaitTimeInSeconds = 60
            }
        };

        static void Main(string[] args)
        {
            Program.RunAsync().Wait();
        }

        static async Task RunAsync()
        {
            string endpoint = ConfigurationManager.AppSettings["EndPointUrl"];
            string authKey = ConfigurationManager.AppSettings["AuthorizationKey"];

            DocumentClient client = new DocumentClient(new Uri(endpoint), authKey);
            Uri collectionLink = UriFactory.CreateDocumentCollectionUri(Program.DatabaseName, Program.DataCollectionName);

            DocumentCollection collection = await Program.CreateCollectionIfNotExistsAsync(client);
            // Document Operations
            //----------------------
            {
                DocumentModel documentModel = new DocumentModel(client, collectionLink);

                // 1. Insert
                await documentModel.InsertCountyDataAsync();

                // 2. Query 
                await documentModel.QueryCountyAsync();
            }
        }

        private static async Task<DocumentCollection> CreateCollectionIfNotExistsAsync(DocumentClient client)
        {
            if (Program.GetDatabaseIfExists(client, Program.DatabaseName) == null)
            {
                return null;
            }

            DocumentCollection collection = null;

            try
            {
                collection = await client.ReadDocumentCollectionAsync(UriFactory.CreateDocumentCollectionUri(Program.DatabaseName, Program.DataCollectionName));
            }
            catch(DocumentClientException exception)
            {
                if (exception.StatusCode != HttpStatusCode.NotFound)
                    throw;
            }

            if(collection == null)
            {
                return await client.CreateDocumentCollectionAsync(
                    UriFactory.CreateDatabaseUri(Program.DatabaseName),
                    new DocumentCollection() { Id = Program.DataCollectionName },
                    new RequestOptions { OfferThroughput = 1000 });
            }

            return collection;
        }

        private static Database GetDatabaseIfExists(DocumentClient client, string databaseName)
        {
            return client.CreateDatabaseQuery().Where(d => d.Id == databaseName).AsEnumerable().FirstOrDefault();
        }
    }
}
