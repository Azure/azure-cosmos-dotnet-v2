namespace DocumentDB.ChangeFeedProcessor
{
    class DocumentFeedObserverFactory<T> : IDocumentFeedObserverFactory where T : IChangeFeedObserver, new() 
    {
        public virtual IChangeFeedObserver CreateObserver()
        {
            return new T();
        }
    }
}
