using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Patterns.LargeDocuments
{
    class GameRepo
    {
        private DocumentClient client;
        private const String DatabaseName = "db";
        private const String GameCollectionName = "games";
        private const String GameStateCollectionName = "gameState";

        public GameRepo(DocumentClient client)
        {
            this.client = client;
        }
        
        public async Task CreateCollectionIfNotExistsAsync()
        {
            DocumentCollection collection = new DocumentCollection();

            collection.Id = GameCollectionName;
            collection.PartitionKey.Paths.Add("/playerId");

            collection.IndexingPolicy.Automatic = true;
            collection.IndexingPolicy.IndexingMode = IndexingMode.Consistent;
            collection.IndexingPolicy.IncludedPaths.Clear();

            IncludedPath path = new IncludedPath();
            path.Path = "/*";
            path.Indexes.Add(new RangeIndex(DataType.String) { Precision = -1 });
            collection.IndexingPolicy.IncludedPaths.Add(path);

            collection.IndexingPolicy.ExcludedPaths.Clear();

            //TIP: Exclude large subtrees from indexing if not queried
            collection.IndexingPolicy.ExcludedPaths.Add(new ExcludedPath { Path = "/bigGameState/*" });

            await this.client.CreateDocumentCollectionIfNotExistsAsync(
                UriFactory.CreateDatabaseUri(DatabaseName),
                collection,
                new RequestOptions { OfferThroughput = 10000 });
        }

        public async Task<Game> GetGameAsync(String playerId, String gameId)
        {
            Uri documentUri = UriFactory.CreateDocumentUri(DatabaseName, GameCollectionName, gameId);

            //TIP: Store infrequently accessed attributes in separate document (can also be blob storage)
            Game game = await client.ReadDocumentAsync<Game>(
                documentUri, 
                new RequestOptions { PartitionKey = new PartitionKey(playerId) });

            if (game.GameStateReferenceId != null)
            {
                Uri gameStateDocumentUri = UriFactory.CreateDocumentUri(DatabaseName, GameStateCollectionName, game.GameStateReferenceId);

                Dictionary<String, Object> gameState = await client.ReadDocumentAsync<Dictionary<String, Object>>(
                    gameStateDocumentUri,
                    new RequestOptions { PartitionKey = new PartitionKey(gameStateDocumentUri) });
                game.BigGameState = gameState;
            }

            return game;
        }

        public async Task AddGameAsync(Game game)
        {
            Uri collectionUri = UriFactory.CreateDocumentCollectionUri(DatabaseName, GameCollectionName);
            await client.UpsertDocumentAsync(collectionUri, game);
        }

        public async Task SaveGameAsync(String playerId, String gameId, Dictionary<String, Object> gameState)
        {
            Uri documentUri = UriFactory.CreateDocumentUri(DatabaseName, GameCollectionName, gameId);

            Game game = await client.ReadDocumentAsync<Game>(
                documentUri,
                new RequestOptions { PartitionKey = new PartitionKey(playerId) });

            if (game.GameStateReferenceId != null)
            {
                Uri gameStateDocumentUri = UriFactory.CreateDocumentUri(DatabaseName, GameStateCollectionName, game.GameStateReferenceId);
                gameState["id"] = game.GameStateReferenceId;
                await client.UpsertDocumentAsync(gameStateDocumentUri, gameState);
            }
            else
            {
                String gameStateReferencId = Guid.NewGuid().ToString();
                Uri gameStateDocumentUri = UriFactory.CreateDocumentUri(DatabaseName, GameStateCollectionName, gameStateReferencId);
                await client.CreateDocumentAsync(gameStateDocumentUri, gameState);

                game.GameStateReferenceId = gameStateReferencId;
                await client.UpsertDocumentAsync(documentUri, game);
            }
        }
    }
}
