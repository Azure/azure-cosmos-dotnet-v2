using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Patterns.EventSourcing
{
    class OrderEventRepo
    {
        private DocumentClient client;

        private const String DatabaseName = "db";
        private const String EventsCollectionName = "eventStore";

        public OrderEventRepo(DocumentClient client)
        {
            this.client = client;
        }

        public async Task CreateCollectionIfNotExistsAsync()
        {
            //TIP: Consider the event-sourcing pattern if your workload is ~50% writes
            DocumentCollection collection = new DocumentCollection();

            collection.Id = EventsCollectionName;
            collection.PartitionKey.Paths.Add("/orderId");

            collection.IndexingPolicy.Automatic = true;
            collection.IndexingPolicy.IndexingMode = IndexingMode.Consistent;
            collection.IndexingPolicy.IncludedPaths.Clear();

            IncludedPath path = new IncludedPath();
            path.Path = "/orderId/?";
            path.Indexes.Add(new RangeIndex(DataType.String) { Precision = -1 });
            collection.IndexingPolicy.IncludedPaths.Add(path);

            path = new IncludedPath();
            path.Path = "/eventType/?";
            path.Indexes.Add(new RangeIndex(DataType.String) { Precision = -1 });
            collection.IndexingPolicy.IncludedPaths.Add(path);

            path = new IncludedPath();
            path.Path = "/eventTime/?";
            path.Indexes.Add(new RangeIndex(DataType.String) { Precision = -1 });
            collection.IndexingPolicy.IncludedPaths.Add(path);

            collection.IndexingPolicy.ExcludedPaths.Clear();
            collection.IndexingPolicy.ExcludedPaths.Add(new ExcludedPath { Path = "/*" });

            await this.client.CreateDocumentCollectionIfNotExistsAsync(
                UriFactory.CreateDatabaseUri(DatabaseName),
                collection,
                new RequestOptions { OfferThroughput = 10000 });
        }

        public async Task AddEvent(OrderEvent orderEvent)
        {
            //TIP: Insert of 1kb document is 5 RU (compared with replace = 10 RU)
            Uri collectionUri = UriFactory.CreateDocumentCollectionUri(DatabaseName, EventsCollectionName);
            await client.CreateDocumentAsync(collectionUri, orderEvent);
        }

        public async Task<String> GetLatestState(String orderId)
        {
            //TIP: Use query to reconstruct the latest state (can also support <= BeforeTimestamp)
            Uri collectionUri = UriFactory.CreateDocumentCollectionUri(DatabaseName, EventsCollectionName);

            IDocumentQuery<String> query = client.CreateDocumentQuery<OrderEvent>(collectionUri)
                .Where(e => e.OrderId == orderId)
                .OrderByDescending(e => e.EventTime)
                .Take(1)
                .Select(e => e.CurrentState)
                .AsDocumentQuery();

            while (query.HasMoreResults)
            {
                FeedResponse<String> response = await query.ExecuteNextAsync<String>();
                if (response.Count > 0)
                {
                    return response.First();
                }
            }

            return null;
        }

    }
}
