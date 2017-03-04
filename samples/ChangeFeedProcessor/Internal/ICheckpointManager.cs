namespace DocumentDB.ChangeFeedProcessor
{
    using System.Threading.Tasks;

    /// <summary>
    /// Provides methods for running checkpoint asynchronously. Extensibility is provided to specify host-specific storage for storing the offset.
    /// </summary> 
    interface ICheckpointManager
    {
        /// <summary>Stores the offset of a particular partition in the host-specific store.</summary>
        /// <param name="lease">Partition information against which to perform a checkpoint.</param>
        /// <param name="offset">Current position in the stream.</param>
        /// <param name="sequenceNumber">The sequence number of the partition.</param>
        /// <returns>Returns <see cref="System.Threading.Tasks.Task" />.</returns>
        Task<Lease> CheckpointAsync(Lease lease, string offset, long sequenceNumber);
    }
}
