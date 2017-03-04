namespace DocumentDB.ChangeFeedProcessor
{
    using System;
    using System.Threading.Tasks;

    interface IPartitionManager<T> where T : Lease
    {
        Task StartAsync();

        Task StopAsync();

        IDisposable Subscribe(IPartitionObserver<T> observer);
    }
}
