using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;

namespace Patterns.OneToMany
{
    class GameRepo
    {
        private DocumentClient client;
        private const String DatabaseName = "db";
        private const String CollectionName = "games";

        public GameRepo(DocumentClient client)
        {
            this.client = client;
        }
        
        public async Task CreateCollectionIfNotExistsAsync()
        {
            DocumentCollection collection = new DocumentCollection();

            // TIP: If queries are known upfront, index just the properties you need
            collection.Id = CollectionName;
            collection.PartitionKey.Paths.Add("/playerId");

            collection.IndexingPolicy.Automatic = true;
            collection.IndexingPolicy.IndexingMode = IndexingMode.Consistent;
            collection.IndexingPolicy.IncludedPaths.Clear();

            IncludedPath path = new IncludedPath();
            path.Path = "/playerId/?";
            path.Indexes.Add(new RangeIndex(DataType.String) { Precision = -1 });

            collection.IndexingPolicy.IncludedPaths.Add(path);
            collection.IndexingPolicy.ExcludedPaths.Clear();
            collection.IndexingPolicy.ExcludedPaths.Add(new ExcludedPath { Path = "/*" });

            await this.client.CreateDocumentCollectionIfNotExistsAsync(
                UriFactory.CreateDatabaseUri(DatabaseName),
                collection,
                new RequestOptions { OfferThroughput = 10000 });
        }

        public async Task<Game> GetGameAsync(String playerId, String gameId)
        {
            // TIP: When partition key != id, ensure it is passed in via GET (not cross-partition query on gameId)
            Uri documentUri = UriFactory.CreateDocumentUri(DatabaseName, CollectionName, gameId);

            return await client.ReadDocumentAsync<Game>(
                documentUri, 
                new RequestOptions { PartitionKey = new PartitionKey(playerId) });
        }

        public async Task<IEnumerable<Game>> GetGamesAsync(String playerId)
        {
            // TIP: Favor single-partition queries (with pk in filter)
            Uri collectionUri = UriFactory.CreateDocumentCollectionUri(DatabaseName, CollectionName);
            IDocumentQuery<Game> query = client.CreateDocumentQuery<Game>(collectionUri).Where(g => g.PlayerId == playerId).AsDocumentQuery();

            List<Game> games = new List<Game>();
            while(query.HasMoreResults)
            {
                FeedResponse<Game> response = await query.ExecuteNextAsync<Game>();
                games.AddRange(response);
            }

            return games;
        }

        public async Task AddGameAsync(Game game)
        {
            Uri collectionUri = UriFactory.CreateDocumentCollectionUri(DatabaseName, CollectionName);
            await client.UpsertDocumentAsync(collectionUri, game);
        }

        public async Task RemoveGameAsync(String playerId, String gameId)
        {
            Uri documentUri = UriFactory.CreateDocumentUri(DatabaseName, CollectionName, gameId);

            await client.DeleteDocumentAsync(
                documentUri,
                new RequestOptions { PartitionKey = new PartitionKey(playerId) });
        }

        public async Task UpdateGameAsync(Game updatedInfo)
        {
            Uri documentUri = UriFactory.CreateDocumentUri(DatabaseName, CollectionName, updatedInfo.Id);

            Game game = await client.ReadDocumentAsync<Game>(
                documentUri,
                new RequestOptions { PartitionKey = new PartitionKey(updatedInfo.PlayerId) });

            AccessCondition condition = new AccessCondition { Condition = game.ETag, Type = AccessConditionType.IfMatch };
            await client.ReplaceDocumentAsync(documentUri, updatedInfo, new RequestOptions { AccessCondition = condition });
        }
    }
}
