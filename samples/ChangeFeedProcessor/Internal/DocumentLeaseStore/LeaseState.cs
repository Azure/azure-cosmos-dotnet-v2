namespace DocumentDB.ChangeFeedProcessor.DocumentLeaseStore
{
    enum LeaseState
    {
        /// <summary>
        /// The lease is in unknown state.
        /// </summary>
        Unspecified = 0,

        /// <summary>
        /// The lease is available in the sense that it is not own, or leased, by any host.
        /// </summary>
        Available,

        /// <summary>
        /// The lease is leased to, or owned by some host.
        /// </summary>
        Leased,
    }
}
