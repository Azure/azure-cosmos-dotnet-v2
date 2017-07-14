using Microsoft.Azure.Documents.ChangeFeedProcessor;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;

public class DocumentFeedObserverFactory: IChangeFeedObserverFactory
{
    DocumentClient client;
    DocumentCollectionInfo collectionInfo; 

    public DocumentFeedObserverFactory(DocumentCollectionInfo destCollInfo)
    {
        this.collectionInfo = destCollInfo; 
        this.client = new DocumentClient(destCollInfo.Uri, destCollInfo.MasterKey);
        this.client.CreateDatabaseIfNotExistsAsync(new Database { Id = destCollInfo.DatabaseName });

        // create dest collection if it does not exist
        DocumentCollection destCollection = new DocumentCollection();
        destCollection.Id = destCollInfo.CollectionName;

        //destCollection.PartitionKey.Paths.Add("add partition key if applicable");
        this.client.CreateDocumentCollectionIfNotExistsAsync(
            UriFactory.CreateDatabaseUri(destCollInfo.DatabaseName),
            destCollection,
            new RequestOptions { OfferThroughput = 500 });
    }

    ~DocumentFeedObserverFactory()
    {
        if (this.client != null) this.client.Dispose(); 
    }

    public IChangeFeedObserver CreateObserver()
    {
        DocumentFeedObserver newObserver = new DocumentFeedObserver(this.client, this.collectionInfo);
        return newObserver;  
    }
}
