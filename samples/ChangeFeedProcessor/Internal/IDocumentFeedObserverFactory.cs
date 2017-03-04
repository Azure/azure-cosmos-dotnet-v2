namespace DocumentDB.ChangeFeedProcessor
{
    internal interface IDocumentFeedObserverFactory
    {
        IChangeFeedObserver CreateObserver();
    }
}
