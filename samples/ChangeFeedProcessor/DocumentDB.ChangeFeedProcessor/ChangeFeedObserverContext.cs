namespace DocumentDB.ChangeFeedProcessor
{
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using System.Threading.Tasks;

    /// <summary>
    /// The context passed to <see cref="DocumentDB.ChangeFeedProcessor.IChangeFeedObserver"/> events.
    /// </summary>
    public class ChangeFeedObserverContext
    {
        ChangeFeedEventHost host;

        internal ChangeFeedObserverContext(string partitionKeyRangeId, ChangeFeedEventHost host)
        {
            this.PartitionKeyRangeId = partitionKeyRangeId;
            this.host = host;
        }

        /// <summary>
        /// Gets the id of the partition for current event.
        /// </summary>
        public string PartitionKeyRangeId { get; private set; }

        /// <summary>
        /// The response from the underlying <see cref="Microsoft.Azure.Documents.Linq.IDocumentQuery<T>.ExecuteNextAsync"> call.
        /// </summary>
        public IFeedResponse<Document> FeedResponse { get; internal set; }

        /// <summary>
        /// This provides a way for manual checkpointing (useful when ChangeFeedHostOptions.IsAutoCheckpointEnabled is set to false).
        /// </summary>
        public Task CheckpointAsync()
        {
            return this.host.CheckpointAsync(this.FeedResponse.ResponseContinuation, this);
        }
    }
}
