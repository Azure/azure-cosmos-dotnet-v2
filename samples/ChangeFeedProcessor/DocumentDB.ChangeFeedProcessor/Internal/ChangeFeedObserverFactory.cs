//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//----------------------------------------------------------------

namespace DocumentDB.ChangeFeedProcessor
{
    class ChangeFeedObserverFactory<T> : IChangeFeedObserverFactory where T : IChangeFeedObserver, new() 
    {
        public virtual IChangeFeedObserver CreateObserver()
        {
            return new T();
        }
    }
}
