using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Patterns.ManyToMany
{
    class GameRepo
    {
        private DocumentClient client;

        private const String DatabaseName = "db";
        private const String PlayerLookupCollectionName = "gamesByPlayerId";
        private const String GameLookupCollectionName = "gamesByGameId";

        public GameRepo(DocumentClient client)
        {
            this.client = client;
        }
        
        public async Task CreateCollectionIfNotExistsAsync()
        {
            DocumentCollection collection = new DocumentCollection();

            collection.Id = PlayerLookupCollectionName;
            collection.PartitionKey.Paths.Add("/playerId");

            collection.IndexingPolicy.Automatic = true;
            collection.IndexingPolicy.IndexingMode = IndexingMode.Consistent;
            collection.IndexingPolicy.IncludedPaths.Clear();

            IncludedPath path = new IncludedPath();
            path.Path = "/playerId/?";
            path.Indexes.Add(new RangeIndex(DataType.String) { Precision = -1 });

            collection.IndexingPolicy.IncludedPaths.Add(path);

            path = new IncludedPath();
            path.Path = "/gameId/?";
            path.Indexes.Add(new RangeIndex(DataType.String) { Precision = -1 });

            collection.IndexingPolicy.IncludedPaths.Add(path);

            collection.IndexingPolicy.ExcludedPaths.Clear();
            collection.IndexingPolicy.ExcludedPaths.Add(new ExcludedPath { Path = "/*" });

            await this.client.CreateDocumentCollectionIfNotExistsAsync(
                UriFactory.CreateDatabaseUri(DatabaseName),
                collection,
                new RequestOptions { OfferThroughput = 10000 });
        }

        public async Task<IEnumerable<Game>> GetGamesAsync(String playerId)
        {
            Uri collectionUri = UriFactory.CreateDocumentCollectionUri(DatabaseName, PlayerLookupCollectionName);
            IDocumentQuery<Game> query = client.CreateDocumentQuery<Game>(collectionUri)
                .Where(g => g.PlayerId == playerId).AsDocumentQuery();

            List<Game> games = new List<Game>();
            while(query.HasMoreResults)
            {
                FeedResponse<Game> response = await query.ExecuteNextAsync<Game>();
                games.AddRange(response);
            }

            return games;
        }

        public async Task<Game> GetGameInfrequentAsync(String gameId)
        {
            Uri collectionUri = UriFactory.CreateDocumentCollectionUri(DatabaseName, PlayerLookupCollectionName);

            FeedOptions feedOptions = new FeedOptions
            {
                EnableCrossPartitionQuery = true,
                MaxDegreeOfParallelism = -1
            };

            IDocumentQuery<Game> query = client.CreateDocumentQuery<Game>(collectionUri, feedOptions)
                .Where(g => g.GameId == gameId).AsDocumentQuery();

            while (query.HasMoreResults)
            {
                FeedResponse<Game> response = await query.ExecuteNextAsync<Game>();
                if (response.Count > 0)
                {
                    return response.First();
                }
            }

            return null;
        }

        public async Task<Game> GetGameAsync(String gameId)
        {
            Uri documentUri = UriFactory.CreateDocumentUri(DatabaseName, GameLookupCollectionName, gameId);

            return await client.ReadDocumentAsync<Game>(
                documentUri,
                new RequestOptions { PartitionKey = new PartitionKey(gameId) });
        }

        public async Task SyncGameLookupCollectionAsync(List<Document> changes)
        {
            Uri sourceCollectionUri = UriFactory.CreateDocumentCollectionUri(DatabaseName, PlayerLookupCollectionName);
            Uri destCollectionUri = UriFactory.CreateDocumentCollectionUri(DatabaseName, GameLookupCollectionName);

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
                        FeedResponse<Game> response = query.ExecuteNextAsync<Game>().Result;
                        foreach (Game changedGame in response)
                        {
                            await this.client.UpsertDocumentAsync(destCollectionUri, changedGame);
                        }

                        checkpoints[pkRange.Id] = response.ResponseContinuation;
                    }
                }

                await Task.Delay(1000);
            }
        }

        public async Task AddGameAsync(Game game)
        {
            Uri collectionUri = UriFactory.CreateDocumentCollectionUri(DatabaseName, PlayerLookupCollectionName);
            await client.UpsertDocumentAsync(collectionUri, game);
        }

        public async Task RemoveGameAsync(String playerId, String gameId)
        {
            Uri documentUri = UriFactory.CreateDocumentUri(DatabaseName, PlayerLookupCollectionName, gameId);

            await client.DeleteDocumentAsync(
                documentUri,
                new RequestOptions { PartitionKey = new PartitionKey(playerId) });
        }

        public async Task UpdateGameAsync(Game updatedInfo)
        {
            Uri documentUri = UriFactory.CreateDocumentUri(DatabaseName, PlayerLookupCollectionName, updatedInfo.Id);

            Game game = await client.ReadDocumentAsync<Game>(
                documentUri,
                new RequestOptions { PartitionKey = new PartitionKey(updatedInfo.PlayerId) });

            AccessCondition condition = new AccessCondition { Condition = game.ETag, Type = AccessConditionType.IfMatch };
            await client.ReplaceDocumentAsync(documentUri, game, new RequestOptions { AccessCondition = condition });
        }
    }
}
