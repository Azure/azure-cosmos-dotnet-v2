namespace DocumentDB.ChangeFeedProcessor
{
    /// <summary>
    /// The context passed to <see cref="DocumentDB.ChangeFeedProcessor.IChangeFeedObserver"/> events.
    /// </summary>
    public class ChangeFeedObserverContext
    {
        /// <summary>
        /// Gets the id of the partition for current event.
        /// </summary>
        public string PartitionKeyRangeId { get; internal set; }
    }
}
