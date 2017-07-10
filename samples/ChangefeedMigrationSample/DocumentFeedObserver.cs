using Microsoft.Azure.Documents.ChangeFeedProcessor;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

class DocumentFeedObserver : IChangeFeedObserver
{
    private static int s_totalDocs = 0;
    private DocumentCollectionInfo destCollInfo;

    public DocumentFeedObserver()
    {
        destCollInfo = new DocumentCollectionInfo
        {
            Uri = new Uri("https://interntest.documents.azure.com:443/"),
            MasterKey = "mXBmwssUDqDNL03M0qkmMBYizwpeIrLqCyFNUOGQsGCEeLRRkWJDleEORnVNzfQ13dkiIyxfhgVM4QAQLzQQzg==",
            DatabaseName = "SmartHome",
            CollectionName = "Dest"
        };
    }

    public Task OpenAsync(ChangeFeedObserverContext context)
    {
        Console.WriteLine("Worker opened, {0}", context.PartitionKeyRangeId);
        return Task.CompletedTask;  // Requires targeting .NET 4.6+.
    }

    public Task CloseAsync(ChangeFeedObserverContext context, ChangeFeedObserverCloseReason reason)
    {
        Console.WriteLine("Worker closed, {0}", context.PartitionKeyRangeId);
        Console.WriteLine("Reason for shutdown, {0}", reason);
        return Task.CompletedTask;
    }

    public Task ProcessChangesAsync(ChangeFeedObserverContext context, IReadOnlyList<Document> docs)
    {

        DocumentClient newClient = new DocumentClient(this.destCollInfo.Uri, this.destCollInfo.MasterKey);

        newClient.CreateDatabaseIfNotExistsAsync(new Database { Id = this.destCollInfo.DatabaseName });

        // create dest collection if it does not exist
        DocumentCollection destCollection = new DocumentCollection();
        destCollection.Id = this.destCollInfo.CollectionName;

        //destCollection.PartitionKey.Paths.Add("add partition key if applicable");

        newClient.CreateDocumentCollectionIfNotExistsAsync(
            UriFactory.CreateDatabaseUri(this.destCollInfo.DatabaseName),
            destCollection,
            new RequestOptions { OfferThroughput = 500 });

        Console.WriteLine("Change feed: total {0} doc(s)", Interlocked.Add(ref s_totalDocs, docs.Count));
        foreach (Document doc in docs)
        {
            Console.WriteLine(doc.ToString());
            newClient.UpsertDocumentAsync(
                UriFactory.CreateDocumentCollectionUri(this.destCollInfo.DatabaseName, this.destCollInfo.CollectionName),
                doc);
        }
        return Task.CompletedTask;
    }
}

