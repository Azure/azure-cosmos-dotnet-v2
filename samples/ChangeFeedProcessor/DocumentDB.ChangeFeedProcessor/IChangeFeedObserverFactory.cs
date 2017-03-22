//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//----------------------------------------------------------------

namespace DocumentDB.ChangeFeedProcessor
{
    /// <summary>
    /// Factory class used to create instance(s) of <see cref="DocumentDB.ChangeFeedProcessor.IChangeFeedObserver"/>.
    /// </summary>
    public interface IChangeFeedObserverFactory
    {
        /// <summary>
        /// Creates a new instance of <see cref="DocumentDB.ChangeFeedProcessor.IChangeFeedObserver"/>.
        /// </summary>
        /// <returns>Created instance of <see cref="DocumentDB.ChangeFeedProcessor.IChangeFeedObserver"/>.</returns>
        IChangeFeedObserver CreateObserver();
    }
}
