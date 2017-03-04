namespace DocumentDB.ChangeFeedProcessor
{
    /// <summary>
    /// The reason why an instance of Observer is closed.
    /// </summary>
    public enum ChangeFeedObserverCloseReason
    {
        /// <summary>
        /// Unknown failure. This should never be sent to observers.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// The ChangeFeedEventHost is shutting down.
        /// </summary>
        Shutdown,

        /// <summary>
        /// The resource, such as database or collection was removed.
        /// </summary>
        ResourceGone,

        /// <summary>
        /// Lease was lost due to expiration or load-balancing.
        /// </summary>
        LeaseLost,

        /// <summary>
        /// IChangeFeedObserver threw an exception.
        /// </summary>
        ObserverError,
    }
}
