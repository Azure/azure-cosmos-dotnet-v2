using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using System;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace DocumentDB.ChangeFeedProcessor.Test
{
    internal class Helper
    {
        internal static void GetConfigurationSettings(
            out DocumentCollectionInfo monitoredCollectionInfo, 
            out DocumentCollectionInfo leaseCollectionInfo,
            out int monitoredOfferThroughput,
            out int leaseOfferThroughput)
        {
            monitoredCollectionInfo = new DocumentCollectionInfo
            {
                Uri = new Uri(ConfigurationManager.AppSettings["endpoint"]),
                MasterKey = ConfigurationManager.AppSettings["masterKey"],
                DatabaseName = ConfigurationManager.AppSettings["databaseId"],
                ConnectionPolicy = new ConnectionPolicy { ConnectionMode = ConnectionMode.Gateway }
            };

            leaseCollectionInfo = new DocumentCollectionInfo
            {
                Uri = new Uri(ConfigurationManager.AppSettings["endpoint"]),
                MasterKey = ConfigurationManager.AppSettings["masterKey"],
                DatabaseName = ConfigurationManager.AppSettings["databaseId"],
                ConnectionPolicy = new ConnectionPolicy { ConnectionMode = ConnectionMode.Gateway }
            };

            monitoredOfferThroughput = int.Parse(ConfigurationManager.AppSettings["monitoredOfferThroughput"]);
            leaseOfferThroughput = int.Parse(ConfigurationManager.AppSettings["leaseOfferThroughput"]);
        }

        internal static async Task CreateDocumentCollectionAsync(DocumentClient client, string databaseId, DocumentCollection collection, int offerThroughput)
        {
            Debug.Assert(client != null);
            Debug.Assert(collection != null);

            var database = new Database { Id = databaseId };
            database = await client.CreateDatabaseIfNotExistsAsync(database);

            await client.CreateDocumentCollectionAsync(database.SelfLink, collection, new RequestOptions { OfferThroughput = offerThroughput });
        }

        internal static async Task CreateDocumentsAsync(DocumentClient client, Uri collectionUri, int count)
        {
            Debug.Assert(client != null);

            var dummyCounts = Enumerable.Repeat(0, count);
            var emptyDocument = new object();

            await dummyCounts.ForEachAsync(
                async dummyCounter => { await client.CreateDocumentAsync(collectionUri, emptyDocument); },
                128);
        }

        internal static async Task<int> GetPartitionCount(DocumentCollectionInfo collectionInfo)
        {
            Debug.Assert(collectionInfo != null);

            int partitionKeyRangeCount;
            using (var client = new DocumentClient(collectionInfo.Uri, collectionInfo.MasterKey, collectionInfo.ConnectionPolicy))
            {
                DocumentCollection monitoredCollection = await client.ReadDocumentCollectionAsync(UriFactory.CreateDocumentCollectionUri(
                    collectionInfo.DatabaseName, collectionInfo.CollectionName));

                var partitionKeyRanges = await CollectionHelper.EnumPartitionKeyRangesAsync(client, monitoredCollection.SelfLink);
                partitionKeyRangeCount = partitionKeyRanges.Count;
            }

            return partitionKeyRangeCount;
        }
    }
}
