namespace DocumentDB.ChangeFeedProcessor
{
    using Newtonsoft.Json;

    /// <summary>
    /// Contains partition ownership information.
    /// </summary>
    abstract class Lease
    {
        public Lease()
        {
        }

        public Lease(Lease source)
        {
            this.PartitionId = source.PartitionId;
            this.Owner = source.Owner;
            this.ContinuationToken = source.ContinuationToken;
            this.SequenceNumber = source.SequenceNumber;
        }

        [JsonProperty("PartitionId")]
        public string PartitionId { get; set; }

        [JsonProperty("Owner")]
        public string Owner { get; set; }

        /// <summary>
        /// Gets or sets the current value for the offset in the stream.
        /// </summary>
        [JsonProperty("ContinuationToken")]
        public string ContinuationToken { get; set; }

        /// <summary>
        /// Gets or sets the last checkpointed sequence number in the stream.
        /// </summary>
        [JsonProperty("SequenceNumber")]
        public long SequenceNumber { get; set; }

        [JsonIgnore]
        public abstract string ConcurrencyToken { get; }

        public override bool Equals(object obj)
        {
            if (object.ReferenceEquals(this, obj))
            {
                return true;
            }

            Lease lease = obj as Lease;
            if (lease == null)
            {
                return false;
            }

            return string.Equals(this.PartitionId, lease.PartitionId);
        }

        public override int GetHashCode() 
        { 
            return this.PartitionId.GetHashCode(); 
        }
    }
}
