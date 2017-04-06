namespace DocumentDB.ChangeFeedProcessor
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    interface ILeaseManager<T> where T : Lease
    {
        Task<bool> LeaseStoreExistsAsync();

        /// <summary>
        /// Checks whether lease store exists and creates if does not exist.
        /// </summary>
        /// <returns>true if created, false otherwise.</returns>
        Task<bool> CreateLeaseStoreIfNotExistsAsync();

        Task<IEnumerable<T>> ListLeases();

        /// <summary>
        /// Checks whether lease exists and creates if does not exist.
        /// </summary>
        /// <returns>true if created, false otherwise.</returns>
        Task<bool> CreateLeaseIfNotExistAsync(string partitionId, string continuationToken);

        Task<T> GetLeaseAsync(string partitionId);

        Task<T> AcquireAsync(T lease, string owner);

        Task<T> RenewAsync(T lease);

        Task<bool> ReleaseAsync(T lease);

        Task DeleteAsync(T lease);

        Task DeleteAllAsync();

        Task<bool> IsExpired(T lease);
    }
}
