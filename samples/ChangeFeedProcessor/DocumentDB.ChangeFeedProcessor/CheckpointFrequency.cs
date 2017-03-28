namespace DocumentDB.ChangeFeedProcessor
{
    using System;

    /// <summary>
    /// Specifies the frequency of lease event. The event will trigger when either of conditions is satisfied.
    /// </summary>
    public class CheckpointFrequency
    {
        /// <summary>
        /// Gets or set the value that specifies to checkpoint every speficied number of docs.
        /// </summary>
        public int? ProcessedDocumentCount { get; set; }

        /// <summary>
        /// Gets or set the value that specifies to checkpoint every specified time interval.
        /// </summary>
        public TimeSpan? TimeInterval { get; set; }
    }
}
