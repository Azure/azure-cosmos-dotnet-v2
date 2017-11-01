using Microsoft.Azure.Documents;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace DocumentDB.ChangeFeedProcessor.Test.Observer
{
    class TestObserver : IChangeFeedObserver
    {
        private readonly IChangeFeedObserver parent;

        public TestObserver(IChangeFeedObserver parent)
        {
            Debug.Assert(parent != null);
            this.parent = parent;
        }

        public Task OpenAsync(ChangeFeedObserverContext context)
        {
            return parent.OpenAsync(context);
        }

        public Task CloseAsync(ChangeFeedObserverContext context, ChangeFeedObserverCloseReason reason)
        {
            return parent.CloseAsync(context, reason);
        }

        public Task ProcessChangesAsync(ChangeFeedObserverContext context, IReadOnlyList<Document> docs)
        {
            return this.parent.ProcessChangesAsync(context, docs);
        }
    }
}
