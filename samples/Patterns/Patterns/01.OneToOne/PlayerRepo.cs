using System;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;

namespace Patterns.OneToOne
{
    class PlayerRepo
    {
        private DocumentClient client;
        private const String DatabaseName = "db";
        private const String CollectionName = "players";

        public PlayerRepo(DocumentClient client)
        {
            this.client = client;
        }
        
        public async Task CreateCollectionIfNotExistsAsync()
        {
            DocumentCollection collection = new DocumentCollection();

            // TIP: ID may be a good choice for partition key
            collection.Id = CollectionName;
            collection.PartitionKey.Paths.Add("/id");

            // TIP: Disable indexing if it's just KV lookup
            collection.IndexingPolicy.Automatic = false;
            collection.IndexingPolicy.IndexingMode = IndexingMode.None;
            collection.IndexingPolicy.IncludedPaths.Clear();
            collection.IndexingPolicy.ExcludedPaths.Clear();

            await this.client.CreateDocumentCollectionIfNotExistsAsync(
                UriFactory.CreateDatabaseUri(DatabaseName),
                collection,
                new RequestOptions { OfferThroughput = 10000 });
        }

        public async Task<Player> GetPlayerAsync(String playerId)
        {
            Uri documentUri = UriFactory.CreateDocumentUri(DatabaseName, CollectionName, playerId);

            // TIP: Use GET over query when possible
            return await client.ReadDocumentAsync<Player>(
                documentUri, 
                new RequestOptions { PartitionKey = new PartitionKey(playerId) });
        }

        public async Task AddPlayerAsync(Player player)
        {
            // TIP: Use the atomic Upsert for Insert or Replace
            Uri collectionUri = UriFactory.CreateDocumentCollectionUri(DatabaseName, CollectionName);
            await client.UpsertDocumentAsync(collectionUri, player);
        }

        public async Task RemovePlayerAsync(String playerId)
        {
            Uri documentUri = UriFactory.CreateDocumentUri(DatabaseName, CollectionName, playerId);

            await client.DeleteDocumentAsync(
                documentUri,
                new RequestOptions { PartitionKey = new PartitionKey(playerId) });
        }

        public async Task UpdatePlayerAsync(Player updatedInfo)
        {
            // TIP: Use conditional update with ETag
            Uri documentUri = UriFactory.CreateDocumentUri(DatabaseName, CollectionName, updatedInfo.Id);

            Player player = await client.ReadDocumentAsync<Player>(
                documentUri,
                new RequestOptions { PartitionKey = new PartitionKey(updatedInfo.Id) });

            AccessCondition condition = new AccessCondition { Condition = player.ETag, Type = AccessConditionType.IfMatch };
            await client.ReplaceDocumentAsync(documentUri, player, new RequestOptions { AccessCondition = condition });
        }
    }
}
