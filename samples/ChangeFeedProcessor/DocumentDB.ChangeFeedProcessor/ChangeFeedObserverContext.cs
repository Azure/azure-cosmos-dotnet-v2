namespace DocumentDB.ChangeFeedProcessor
{
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;

    /// <summary>
    /// The context passed to <see cref="DocumentDB.ChangeFeedProcessor.IChangeFeedObserver"/> events.
    /// </summary>
    public class ChangeFeedObserverContext
    {
        /// <summary>
        /// Gets the id of the partition for current event.
        /// </summary>
        public string PartitionKeyRangeId { get; internal set; }

        /// <summary>
        /// The response from the underlying <see cref="Microsoft.Azure.Documents.Linq.IDocumentQuery<T>.ExecuteNextAsync"> call.
        /// </summary>
        public IFeedResponse<Document> FeedResponse { get; internal set; }
    }
}
