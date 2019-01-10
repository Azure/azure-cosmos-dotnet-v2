using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Patterns.TimeSeries
{
    class SensorReadingRepo
    {
        private DocumentClient client;

        private const String DatabaseName = "db";
        private const String RawReadingsCollectionName = "rawReadings";
        private const String HourlyReadingsCollectionName = "hourlyRollups";

        public SensorReadingRepo(DocumentClient client)
        {
            this.client = client;
        }

        public async Task CreateCollectionIfNotExistsAsync(String collectionName, int throughput, int expirationDays)
        {
            //TIP: use a fine-grained PK like sensorId, not timestamp
            DocumentCollection collection = new DocumentCollection();

            collection.Id = collectionName;
            collection.PartitionKey.Paths.Add("/sensorId");

            collection.IndexingPolicy.Automatic = true;
            collection.IndexingPolicy.IndexingMode = IndexingMode.Consistent;
            collection.IndexingPolicy.IncludedPaths.Clear();

            IncludedPath path = new IncludedPath();
            path.Path = "/sensorId/?";
            path.Indexes.Add(new RangeIndex(DataType.String) { Precision = -1 });

            collection.IndexingPolicy.IncludedPaths.Add(path);

            path = new IncludedPath();
            path.Path = "/ts/?";
            path.Indexes.Add(new RangeIndex(DataType.Number) { Precision = -1 });

            collection.IndexingPolicy.IncludedPaths.Add(path);

            collection.IndexingPolicy.ExcludedPaths.Clear();
            collection.IndexingPolicy.ExcludedPaths.Add(new ExcludedPath { Path = "/*" });

            //TIP: set TTL for data expiration
            collection.DefaultTimeToLive = (expirationDays * 86400);

            await this.client.CreateDocumentCollectionIfNotExistsAsync(
                UriFactory.CreateDatabaseUri(DatabaseName),
                collection,
                new RequestOptions { OfferThroughput = throughput });
        }

        public async Task AddSensorReading(SensorReading reading)
        {
            Uri collectionUri = UriFactory.CreateDocumentCollectionUri(DatabaseName, RawReadingsCollectionName);
            reading.Id = TimeSpan.FromMilliseconds(reading.UnixTimestamp).ToString("o");
            await client.UpsertDocumentAsync(collectionUri, reading);
        }

        public async Task<IEnumerable<SensorReading>> GetSensorReadingsForTimeRangeAsync(String sensorId, DateTime startTime, DateTime endTime)
        {
            double startTimeMillis = GetEpochTimeMillis(startTime);
            double endTimeMillis = GetEpochTimeMillis(endTime);

            Uri collectionUri = UriFactory.CreateDocumentCollectionUri(DatabaseName, RawReadingsCollectionName);

            IDocumentQuery<SensorReading> query = client.CreateDocumentQuery<SensorReading>(collectionUri)
                .Where(r => r.SensorId == sensorId && r.UnixTimestamp >= startTimeMillis && r.UnixTimestamp < endTimeMillis)
                .AsDocumentQuery();

            List<SensorReading> readings = new List<SensorReading>();
            while (query.HasMoreResults)
            {
                FeedResponse<SensorReading> response = await query.ExecuteNextAsync<SensorReading>();
                readings.AddRange(response);
            }

            return readings;
        }

        private double GetEpochTimeMillis(DateTime dateTime)
        {
            TimeSpan t = DateTime.UtcNow - new DateTime(1970, 1, 1);
            return t.TotalSeconds;
        }

        public async Task CreateCollectionsByHour()
        {
            //TIP: pre-aggregate rollups vs. querying raw data when possible
            await CreateCollectionIfNotExistsAsync(RawReadingsCollectionName, 10000, 7);
            await CreateCollectionIfNotExistsAsync(HourlyReadingsCollectionName, 5000, 90);
        }

        public async Task SyncHourlyRollupCollectionAsync(List<Document> changes)
        {
            //TIP: Use change feed to build time-based rollups
            Uri sourceCollectionUri = UriFactory.CreateDocumentCollectionUri(DatabaseName, RawReadingsCollectionName);
            Uri destCollectionUri = UriFactory.CreateDocumentCollectionUri(DatabaseName, HourlyReadingsCollectionName);

            Dictionary<String, String> checkpoints = new Dictionary<string, string>();

            while (true)
            {
                IEnumerable<PartitionKeyRange> pkRanges = await client.ReadPartitionKeyRangeFeedAsync(sourceCollectionUri);

                foreach (PartitionKeyRange pkRange in pkRanges)
                {
                    string continuation = null;
                    checkpoints.TryGetValue(pkRange.Id, out continuation);

                    IDocumentQuery<Document> query = client.CreateDocumentChangeFeedQuery(
                        sourceCollectionUri,
                        new ChangeFeedOptions
                        {
                            PartitionKeyRangeId = pkRange.Id,
                            RequestContinuation = continuation
                        });

                    while (query.HasMoreResults)
                    {
                        FeedResponse<SensorReading> response = query.ExecuteNextAsync<SensorReading>().Result;
                        foreach (SensorReading reading in response)
                        {
                            DateTime readingTime = new DateTime(TimeSpan.FromMilliseconds(reading.UnixTimestamp).Ticks);
                            DateTime startOfHour = new DateTime(readingTime.Year, readingTime.Month, readingTime.Day, readingTime.Hour, 0, 0);
                            string hourlyRollupId = startOfHour.ToString("o");

                            Uri sensorRollupUri = UriFactory.CreateDocumentUri(DatabaseName, HourlyReadingsCollectionName, hourlyRollupId);

                            SensorReadingRollup rollup = await client.ReadDocumentAsync<SensorReadingRollup>(
                                destCollectionUri,
                                new RequestOptions { PartitionKey = new PartitionKey(reading.SensorId) });

                            rollup.Count++;
                            rollup.SumTemperature += reading.Temperature;
                            rollup.SumPressure += reading.Pressure;

                            await this.client.UpsertDocumentAsync(destCollectionUri, rollup);
                        }

                        checkpoints[pkRange.Id] = response.ResponseContinuation;
                    }
                }

                await Task.Delay(1000);
            }
        }
    }
}
