using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Patterns.LargePartitionKeys
{
    class SensorReadingRepo
    {
        private DocumentClient client;

        private const String DatabaseName = "db";
        private const String RawReadingsCollectionName = "rawReadings";

        public SensorReadingRepo(DocumentClient client)
        {
            this.client = client;
        }

        public async Task CreateCollectionIfNotExistsAsync(String collectionName, int throughput, int expirationDays)
        {
            DocumentCollection collection = new DocumentCollection();

            collection.Id = collectionName;
            collection.PartitionKey.Paths.Add("/partitionKey");

            collection.IndexingPolicy.Automatic = true;
            collection.IndexingPolicy.IndexingMode = IndexingMode.Consistent;
            collection.IndexingPolicy.IncludedPaths.Clear();

            IncludedPath path = new IncludedPath();
            path.Path = "/partitionKey/?";
            path.Indexes.Add(new RangeIndex(DataType.String) { Precision = -1 });
            collection.IndexingPolicy.IncludedPaths.Add(path);

            path = new IncludedPath();
            path.Path = "/sensorId/?";
            path.Indexes.Add(new RangeIndex(DataType.String) { Precision = -1 });
            collection.IndexingPolicy.IncludedPaths.Add(path);

            path = new IncludedPath();
            path.Path = "/ts/?";
            path.Indexes.Add(new RangeIndex(DataType.Number) { Precision = -1 });
            collection.IndexingPolicy.IncludedPaths.Add(path);

            collection.IndexingPolicy.ExcludedPaths.Clear();
            collection.IndexingPolicy.ExcludedPaths.Add(new ExcludedPath { Path = "/*" });

            collection.DefaultTimeToLive = (expirationDays * 86400);

            await this.client.CreateDocumentCollectionIfNotExistsAsync(
                UriFactory.CreateDatabaseUri(DatabaseName),
                collection,
                new RequestOptions { OfferThroughput = throughput });
        }

        public async Task AddSensorReading(SensorReading reading)
        {
            Uri collectionUri = UriFactory.CreateDocumentCollectionUri(DatabaseName, RawReadingsCollectionName);

            //TIP: for hot keys, use a secondary random value to distribute writes
            Random random = new Random();
            reading.Id = TimeSpan.FromMilliseconds(reading.UnixTimestamp).ToString("o");

            int randomFuzz = random.Next() % 10;
            reading.PartitionKey = $"{reading.SensorId}.{randomFuzz}";

            await client.UpsertDocumentAsync(collectionUri, reading);
        }
    }
}
