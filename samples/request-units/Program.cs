using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
using Newtonsoft.Json;

namespace RequestUnits
{
    class Program
    {
        static void Main(string[] args)
        {
            using (DocumentClient client = new DocumentClient(new Uri("https://FILLME.documents.azure.com:443/"), "FILLME",
                new ConnectionPolicy { ConnectionMode = ConnectionMode.Direct, ConnectionProtocol = Protocol.Tcp, MaxConnectionLimit = 1000 }))
            {
                RunAsync(client).Wait();
            }
        }

        private static async Task RunAsync(DocumentClient client)
        {
            // Get a 1KB document by ID
            ResourceResponse<Document> kbReadResponse = await client.ReadDocumentAsync(UriFactory.CreateDocumentUri("db", "demo", "1kb-document"));
            Console.WriteLine("Read document completed with {0} RUs", kbReadResponse.RequestCharge);
            Console.ReadKey();

            // Post a 1KB document
            Document document = kbReadResponse.Resource;
            document.Id = Guid.NewGuid().ToString();

            ResourceResponse<Document> kbWriteResponse = await client.CreateDocumentAsync(UriFactory.CreateDocumentCollectionUri("db", "demo"),
                document);
            Console.WriteLine("Create document completed with {0} RUs", kbWriteResponse.RequestCharge);
            Console.ReadKey();

            document.Id = Guid.NewGuid().ToString();
            kbWriteResponse = await client.CreateDocumentAsync(UriFactory.CreateDocumentCollectionUri("db", "demo"),
                document, new RequestOptions { IndexingDirective = IndexingDirective.Exclude });
            Console.WriteLine("Create document completed with {0} RUs", kbWriteResponse.RequestCharge);
            Console.ReadKey();

            // Query for 100 1KB documents
            IDocumentQuery<Document> query = client.CreateDocumentQuery(
                UriFactory.CreateDocumentCollectionUri("db", "demo"), 
                new FeedOptions { MaxItemCount = -1 }).Take(100).AsDocumentQuery();

            FeedResponse<Document> queryResponse = await query.ExecuteNextAsync<Document>();
            Console.WriteLine("Query TOP 100 documents completed with {0} results and {1} RUs", queryResponse.Count, queryResponse.RequestCharge);
            Console.ReadKey();

            // Query for 1000, query by index
            query = client.CreateDocumentQuery(
                UriFactory.CreateDocumentCollectionUri("db", "demo"), 
                new FeedOptions { MaxItemCount = -1 }).Take(1000).AsDocumentQuery();

            queryResponse = await query.ExecuteNextAsync<Document>();
            Console.WriteLine("Query TOP 1000 documents completed with {0} results and {1} RUs", queryResponse.Count, queryResponse.RequestCharge);
            Console.ReadKey();

            // Query by filter (from index)
            IDocumentQuery<Family> secondQuery = client.CreateDocumentQuery<Family>(UriFactory.CreateDocumentCollectionUri("db", "demo"))
                .Where(f => f.Address.City == "redmond").AsDocumentQuery();

            FeedResponse<Family> secondQueryResponse = await secondQuery.ExecuteNextAsync<Family>();
            Console.WriteLine("Query with filter completed with {0} results and {1} RUs", secondQueryResponse.Count, secondQueryResponse.RequestCharge);
            Console.ReadKey();

            // Get a 16KB document by ID
            ResourceResponse<Document> multiKbReadResponse = await client.ReadDocumentAsync(UriFactory.CreateDocumentUri("db", "demo", "16kb-document"));
            Console.WriteLine("Read 16KB document completed with {0} RUs", multiKbReadResponse.RequestCharge);
            Console.ReadKey();
        }

        public class Family
        {
            [JsonProperty("id")]
            public string Id { get; set; }

            [JsonProperty("lastName")]
            public string LastName { get; set; }

            [JsonProperty("address")]
            public Address Address { get; set; }
        }

        public class Address
        {
            [JsonProperty("state")]
            public string State { get; set; }

            [JsonProperty("county")]
            public string County { get; set; }

            [JsonProperty("city")]
            public string City { get; set; }
        }
    }
}
