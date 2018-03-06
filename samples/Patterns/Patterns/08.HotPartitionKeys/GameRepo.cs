using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Patterns.HotPartitionKeys
{
    class GameRepo
    {
        private DocumentClient client;
        private const String DatabaseName = "db";
        private const String GamesCollectionName = "games";
        private const String HotGamesCollectionName = "hotGames";

        public GameRepo(DocumentClient client)
        {
            this.client = client;
        }

        public async Task CreateCollectionsAsync()
        {
            await CreateCollectionIfNotExistsAsync(GamesCollectionName, 10000);
            await CreateCollectionIfNotExistsAsync(HotGamesCollectionName, 5000);
        }

        public async Task CreateCollectionIfNotExistsAsync(String collectionName, int throughput)
        {
            DocumentCollection collection = new DocumentCollection();

            collection.Id = collectionName;
            collection.PartitionKey.Paths.Add("/id");

            collection.IndexingPolicy.Automatic = false;
            collection.IndexingPolicy.IndexingMode = IndexingMode.None;
            collection.IndexingPolicy.IncludedPaths.Clear();
            collection.IndexingPolicy.ExcludedPaths.Clear();

            await this.client.CreateDocumentCollectionIfNotExistsAsync(
                UriFactory.CreateDatabaseUri(DatabaseName),
                collection,
                new RequestOptions { OfferThroughput = throughput });
        }

        public async Task<Game> GetGameAsync(String gameId)
        {
            //TIP: If there is an order of magnitude difference between hot and cold data, build a second lookup collection for hot data
            Game game = null;
            try
            {
                game = await client.ReadDocumentAsync<Game>(
                    UriFactory.CreateDocumentUri(DatabaseName, HotGamesCollectionName, gameId),
                    new RequestOptions { PartitionKey = new PartitionKey(gameId) });
            }
            catch (DocumentClientException de) when (de.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                game = await client.ReadDocumentAsync<Game>(
                    UriFactory.CreateDocumentUri(DatabaseName, GamesCollectionName, gameId),
                    new RequestOptions { PartitionKey = new PartitionKey(gameId) });
            }
            catch (DocumentClientException de) when (de.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
            }

            return game;
        }
    }
}