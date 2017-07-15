namespace DocumentDB.ChangeFeedProcessor
{
    using System;

    /// <summary>
    /// Options to control various aspects of partition distribution happening within <see cref="ChangeFeedEventHost"/> instance.
    /// </summary> 
    public class ChangeFeedHostOptions
    {
        static readonly TimeSpan DefaultRenewInterval = TimeSpan.FromSeconds(17);
        static readonly TimeSpan DefaultAcquireInterval = TimeSpan.FromSeconds(13);
        static readonly TimeSpan DefaultExpirationInterval = TimeSpan.FromSeconds(60);
        static readonly TimeSpan DefaultFeedPollDelay = TimeSpan.FromSeconds(5);

        /// <summary>Initializes a new instance of the <see cref="DocumentDB.ChangeFeedProcessor.ChangeFeedHostOptions" /> class.</summary>
        public ChangeFeedHostOptions()
        { 
            this.LeaseRenewInterval = DefaultRenewInterval;
            this.LeaseAcquireInterval = DefaultAcquireInterval;
            this.LeaseExpirationInterval = DefaultExpirationInterval;
            this.FeedPollDelay = DefaultFeedPollDelay;
        }

        /// <summary>
        /// Gets or sets renew interval for all leases for partitions currently held by <see cref="ChangeFeedEventHost"/> instance.
        /// </summary>
        public TimeSpan LeaseRenewInterval { get; set; }

        /// <summary>
        /// Gets or sets the interval to kick off a task to compute if partitions are distributed evenly among known host instances. 
        /// </summary>
        public TimeSpan LeaseAcquireInterval { get; set; }

        /// <summary>
        /// Gets or sets the interval for which the lease is taken on a lease representing a partition. If the lease is not renewed within this 
        /// interval, it will cause it to expire and ownership of the partition will move to another <see cref="ChangeFeedEventHost"/> instance.
        /// </summary>
        public TimeSpan LeaseExpirationInterval { get; set; }

        /// <summary>
        /// Gets or sets the delay in between polling a partition for new changes on the feed, after all current changes are drained.
        /// </summary>
        public TimeSpan FeedPollDelay { get; set; }

        /// <summary>
        /// Gets or sets the the frequency how often to checkpoint leases.
        /// </summary>
        public CheckpointFrequency CheckpointFrequency { get; set; }
        
        /// <summary>
        /// Gets or sets a prefix to be used as part of the lease id. This can be used to support multiple <see cref="ChangeFeedEventHost"/> 
        /// instances pointing at the same feed while using the same auxiliary collection.
        /// </summary>
        public string LeasePrefix { get; set; }

        /// <summary>
        /// Gets or set the minimum partition count for the host.
        /// This can be used to increase the number of partitions for the host and thus override equal distribution (which is the default) of leases between hosts.
        /// </summary>
        internal int MinPartitionCount { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of partitions the host can serve.
        /// This can be used property to limit the number of partitions for the host and thus override equal distribution (which is the default) of leases between hosts.
        /// Default is 0 (unlimited).
        /// </summary>
        internal int MaxPartitionCount { get; set; }

        /// <summary>
        /// Gets or sets whether on start of the host all existing leases should be deleted and the host should start from scratch.
        /// </summary>
        internal bool DiscardExistingLeases { get; set; }

        /// <summary>
        /// Gets maximum number of tasks to use for auxiliary calls.
        /// </summary>
        internal int DegreeOfParallelism
        { 
            get
            {
                return this.MaxPartitionCount > 0 ? this.MaxPartitionCount : 25;
            }
        }
    }
}
