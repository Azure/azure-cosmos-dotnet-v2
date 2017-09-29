namespace DocumentDB.ChangeFeedProcessor
{
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using System.Diagnostics;
    using System.Threading.Tasks;

    /// <summary>
    /// The context passed to <see cref="DocumentDB.ChangeFeedProcessor.IChangeFeedObserver"/> events.
    /// </summary>
    public class ChangeFeedObserverContext
    {
        ChangeFeedEventHost host;
        IFeedResponse<Document> feedResponse;
        string continuationToken;

        internal ChangeFeedObserverContext(string partitionKeyRangeId, ChangeFeedEventHost host)
        {
            Debug.Assert(!string.IsNullOrEmpty(partitionKeyRangeId));
            Debug.Assert(host != null);

            this.PartitionKeyRangeId = partitionKeyRangeId;
            this.host = host;
        }

        /// <summary>
        /// Gets the id of the partition for current event.
        /// </summary>
        public string PartitionKeyRangeId { get; private set; }

        /// <summary>
        /// The response from the underlying <see cref="Microsoft.Azure.Documents.Linq.IDocumentQuery<T>.ExecuteNextAsync"> call.
        /// This property is only available within <see cref="DocumentDB.ChangeFeedProcessor.ProcessChangesAsync"/>.
        /// </summary>
        public IFeedResponse<Document> FeedResponse
        {
            get { return this.feedResponse; }
            internal set
            {
                this.feedResponse = value;

                // Save continuation, as lifetime of FeedResponse is limited.
                if (value != null)
                {
                    this.continuationToken = value.ResponseContinuation;
                }
            }
        }

        /// <summary>
        /// This provides a way for manual checkpointing (useful when ChangeFeedHostOptions.IsAutoCheckpointEnabled is set to false).
        /// </summary>
        public Task CheckpointAsync()
        {
            return this.host.CheckpointAsync(this.continuationToken, this);
        }
    }
}
