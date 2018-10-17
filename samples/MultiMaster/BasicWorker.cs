using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;

namespace CosmosDB.Sql.DotNet.MultiMaster
{
    

    internal sealed class BasicWorker
    {
        private readonly Uri documentCollectionUri;
        private DocumentClient client;

        public BasicWorker(DocumentClient client, string databaseName, string collectionName)
        {
            this.client = client;
            this.documentCollectionUri = UriFactory.CreateDocumentCollectionUri(databaseName, collectionName);
        }

        public async Task RunLoopAsync(int documentsToInsert)
        {
            int iterationCount = 0;

            List<long> latency = new List<long>();
            while (iterationCount++ < documentsToInsert)
            {
                long startTick = Environment.TickCount;

                await this.client.CreateDocumentAsync(
                    this.documentCollectionUri, new Document { Id = Guid.NewGuid().ToString() });

                long endTick = Environment.TickCount;

                latency.Add(endTick - startTick);
            }

            latency.Sort();
            int p50Index = latency.Count / 2;

            Console.WriteLine(
                "Inserted {2} documents at {0} with p50 {1} ms",
                this.client.WriteEndpoint,
                latency[p50Index],
                documentsToInsert);
        }

        public async Task ReadAllAsync(int expectedNumberOfDocuments)
        {
            while (true)
            {
                int totalItemRead = 0;
                FeedResponse<dynamic> response = null;
                do
                {
                    response = await this.client.ReadDocumentFeedAsync(
                        this.documentCollectionUri,
                        new FeedOptions { RequestContinuation = response != null ? response.ResponseContinuation : null });

                    totalItemRead += response.Count;
                }
                while (response.ResponseContinuation != null);

                if (totalItemRead < expectedNumberOfDocuments)
                {
                    Console.WriteLine(
                        "Total item read {0} from {1} is less than {2}, retrying reads",
                        totalItemRead,
                        this.client.ReadEndpoint,
                        expectedNumberOfDocuments);

                    await Task.Delay(1000);
                    continue;
                }
                else
                {
                    Console.WriteLine("Read {0} items from {1}", totalItemRead, this.client.ReadEndpoint);
                    break;
                }
            }
        }

        public async Task DeleteAllAsync()
        {
            List<dynamic> documents = new List<dynamic>();
            FeedResponse<dynamic> response = null;
            do
            {
                response = await this.client.ReadDocumentFeedAsync(
                    this.documentCollectionUri,
                    new FeedOptions { RequestContinuation = response != null ? response.ResponseContinuation : null });

                documents.AddRange(response);
            }
            while (response.ResponseContinuation != null);

            foreach (Document document in documents)
            {
                try
                {
                    await this.client.DeleteDocumentAsync(document.SelfLink);
                }
                catch (DocumentClientException exception)
                {
                    if (exception.StatusCode != System.Net.HttpStatusCode.NotFound)
                    {
                        Console.WriteLine("Error occurred while deleting {0} from {1}", exception, this.client.WriteEndpoint);
                    }
                }
            }

            Console.WriteLine("Deleted all documents from region {0}", this.client.WriteEndpoint);
        }
    }
}
