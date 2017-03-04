namespace DocumentDB.ChangeFeedProcessor
{
    using System.Threading.Tasks;

    interface IPartitionObserver<T> where T : Lease
    {
        Task OnPartitionAcquiredAsync(T lease);

        Task OnPartitionReleasedAsync(T lease, ChangeFeedObserverCloseReason reason);
    }
}