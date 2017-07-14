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
    private DocumentCollectionInfo collectionInfo;
    private DocumentClient client; 

    public DocumentFeedObserver(DocumentClient client, DocumentCollectionInfo destCollInfo)
    {
        this.client = client;
        this.collectionInfo = destCollInfo; 
    }

    public Task OpenAsync(ChangeFeedObserverContext context)
    { 
        Console.WriteLine("Worker opened, {0}", context.PartitionKeyRangeId);
        return Task.CompletedTask;  
    }

    public Task CloseAsync(ChangeFeedObserverContext context, ChangeFeedObserverCloseReason reason)
    {
        Console.WriteLine("Worker closed, {0}", context.PartitionKeyRangeId);
        Console.WriteLine("Reason for shutdown, {0}", reason);
        return Task.CompletedTask;
    }

    public Task ProcessChangesAsync(ChangeFeedObserverContext context, IReadOnlyList<Document> docs)
    {

        Console.WriteLine("Change feed: total {0} doc(s)", Interlocked.Add(ref s_totalDocs, docs.Count));
        foreach (Document doc in docs)
        {
            Console.WriteLine(doc.ToString());
            this.client.UpsertDocumentAsync(
                UriFactory.CreateDocumentCollectionUri(this.collectionInfo.DatabaseName, this.collectionInfo.CollectionName),
                doc);
        }
        return Task.CompletedTask;
    }
}

