namespace DocumentDB.Samples.Partitioning.Partitioners
{
    /// <summary>
    /// Specifies how to handle requests to partitions in transition.
    /// </summary>
    public enum TransitionReadMode
    {
        /// <summary>
        /// Perform reads using the current PartitionResolver.
        /// </summary>
        ReadCurrent,

        /// <summary>
        /// Perform reads using the targeted PartitionResolver.
        /// </summary>
        ReadNext,

        /// <summary>
        /// Perform reads using partitions from both current and targeted PartitionResolvers, and 
        /// return the union of results.
        /// </summary>
        ReadBoth,

        /// <summary>
        /// Throw an transient Exception when reads are attempted during migration.
        /// </summary>
        None
    }
}
