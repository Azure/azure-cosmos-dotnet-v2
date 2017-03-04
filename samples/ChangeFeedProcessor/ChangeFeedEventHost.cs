namespace DocumentDB.ChangeFeedProcessor
{
    using ChangeFeedProcessor.DocumentLeaseStore;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Documents.Linq;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Simple host for distributing change feed events across observers and thus allowing these observers scale.
    /// It distributes the load across its instances and allows dynamic scaling:
    ///   - Partitions in partitioned collections are distributed across instances/observers.
    ///   - New instance takes leases from existing instances to make distribution equal.
    ///   - If an instance dies, the leases are distributed across remaining instances.
    /// It's useful for scenario when partition count is high so that one host/VM is not capable of processing that many change feed events.
    /// Client application needs to implement <see cref="DocumentDB.ChangeFeedProcessor.IChangeFeedObserver"/> and register processor implementation with ChangeFeedEventHost.
    /// </summary>
    /// <remarks>
    /// It uses auxiliary document collection for managing leases for a partition.
    /// Every EventProcessorHost instance is performing the following two tasks:
    ///     1) Renew Leases: It keeps track of leases currently owned by the host and continuously keeps on renewing the leases.
    ///     2) Acquire Leases: Each instance continuously polls all leases to check if there are any leases it should acquire 
    ///     for the system to get into balanced state.
    /// </remarks>
    /// <example>
    /// <code language="c#">
    /// <![CDATA[
    /// class DocumentFeedObserver : IChangeFeedObserver
    /// {
    ///     private static int s_totalDocs = 0;
    ///     public Task OpenAsync(ChangeFeedObserverContext context)
    ///     {
    ///         Console.WriteLine("Worker opened, {0}", context.PartitionKeyRangeId);
    ///         return Task.CompletedTask;  // Requires targeting .NET 4.6+.
    ///     }
    ///     public Task CloseAsync(ChangeFeedObserverContext context, ChangeFeedObserverCloseReason reason)
    ///     {
    ///         Console.WriteLine("Worker closed, {0}", context.PartitionKeyRangeId);
    ///         return Task.CompletedTask;
    ///     }
    ///     public Task ProcessEventsAsync(IReadOnlyList<Document> docs, ChangeFeedObserverContext context)
    ///     {
    ///         Console.WriteLine("Change feed: total {0} doc(s)", Interlocked.Add(ref s_totalDocs, docs.Count));
    ///         return Task.CompletedTask;
    ///     }
    /// }
    /// string hostName = Guid.NewGuid().ToString();
    /// DocumentCollectionInfo documentCollectionLocation = new DocumentCollectionInfo
    /// {
    ///     Uri = new Uri("https://YOUR_SERVICE.documents.azure.com:443/"),
    ///     MasterKey = "YOUR_SECRET_KEY==",
    ///     DatabaseName = "db1",
    ///     CollectionName = "documents"
    /// };
    /// DocumentCollectionInfo leaseCollectionLocation = new DocumentCollectionInfo
    /// {
    ///     Uri = new Uri("https://YOUR_SERVICE.documents.azure.com:443/"),
    ///     MasterKey = "YOUR_SECRET_KEY==",
    ///     DatabaseName = "db1",
    ///     CollectionName = "leases"
    /// };
    /// Console.WriteLine("Main program: Creating ChangeFeedEventHost...");
    /// ChangeFeedEventHost host = new ChangeFeedEventHost(hostName, documentCollectionLocation, leaseCollectionLocation);
    /// await host.RegisterObserverAsync<DocumentFeedObserver>();
    /// Console.WriteLine("Main program: press Enter to stop...");
    /// Console.ReadLine();
    /// await host.UnregisterObserversAsync();
    /// ]]>
    /// </code>
    /// </example>
    public class ChangeFeedEventHost : IPartitionObserver<DocumentServiceLease>
    {
        const string DefaultUserAgentSuffix = "changefeed-0.1";
        const string LeaseContainerName = "docdb-changefeed";
        readonly DocumentCollectionInfo collectionLocation;

        string leasePrefix;
        string collectionSelfLink;
        DocumentClient documentClient;
        ChangeFeedOptions changeFeedOptions;
        ChangeFeedHostOptions options;
        PartitionManager<DocumentServiceLease> partitionManager;
        ILeaseManager<DocumentServiceLease> leaseManager;
        ICheckpointManager checkpointManager;
        DocumentCollectionInfo auxCollectionLocation;

        IDocumentFeedObserverFactory observerFactory;
        ConcurrentDictionary<string, WorkerData> partitionKeyRangeIdToWorkerMap;
        int isShutdown = 0;

#if DEBUG
        int partitionCount;
#endif

        /// <summary>
        /// Initializes a new instance of the <see cref="DocumentDB.ChangeFeedProcessor.ChangeFeedEventHost"/> class.
        /// </summary>
        /// <param name="hostName">Unique name for this host.</param>
        /// <param name="documentCollectionLocation">Specifies location of the DocumentDB collection to monitor changes for.</param>
        /// <param name="auxCollectionLocation">Specifies location of auxiliary data for load-balancing instances of <see cref="DocumentDB.ChangeFeedProcessor.ChangeFeedEventHost" />.</param>
        public ChangeFeedEventHost(string hostName, DocumentCollectionInfo documentCollectionLocation, DocumentCollectionInfo auxCollectionLocation)
            : this(hostName, documentCollectionLocation, auxCollectionLocation, new ChangeFeedOptions(), new ChangeFeedHostOptions())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DocumentDB.ChangeFeedProcessor.ChangeFeedEventHost"/> class.
        /// </summary>
        /// <param name="hostName">Unique name for this host.</param>
        /// <param name="documentCollectionLocation">Specifies location of the DocumentDB collection to monitor changes for.</param>
        /// <param name="auxCollectionLocation">Specifies location of auxiliary data for load-balancing instances of <see cref="DocumentDB.ChangeFeedProcessor.ChangeFeedEventHost" />.</param>
        /// <param name="changeFeedOptions">Options to pass to the Microsoft.AzureDocuments.DocumentClient.CreateChangeFeedQuery API.</param>
        /// <param name="hostOptions">Additional options to control load-balancing of <see cref="DocumentDB.ChangeFeedProcessor.ChangeFeedEventHost" /> instances.</param>
        public ChangeFeedEventHost(
            string hostName, 
            DocumentCollectionInfo documentCollectionLocation, 
            DocumentCollectionInfo auxCollectionLocation, 
            ChangeFeedOptions changeFeedOptions, 
            ChangeFeedHostOptions hostOptions)
        {
            if (documentCollectionLocation == null) throw new ArgumentException("documentCollectionLocation");
            if (documentCollectionLocation.Uri == null) throw new ArgumentException("documentCollectionLocation.Uri");
            if (string.IsNullOrWhiteSpace(documentCollectionLocation.DatabaseName)) throw new ArgumentException("documentCollectionLocation.DatabaseName");
            if (string.IsNullOrWhiteSpace(documentCollectionLocation.CollectionName)) throw new ArgumentException("documentCollectionLocation.CollectionName");
            if (hostOptions.MinPartitionCount > hostOptions.MaxPartitionCount) throw new ArgumentException("hostOptions.MinPartitionCount cannot be greater than hostOptions.MaxPartitionCount");

            this.collectionLocation = CanoninicalizeCollectionInfo(documentCollectionLocation);
            this.changeFeedOptions = changeFeedOptions;
            this.options = hostOptions;
            this.HostName = hostName;
            this.auxCollectionLocation = CanoninicalizeCollectionInfo(auxCollectionLocation);
            this.partitionKeyRangeIdToWorkerMap = new ConcurrentDictionary<string, WorkerData>();
        }

        /// <summary>Gets the host name, which is a unique name for the instance.</summary>
        /// <value>The host name.</value>
        public string HostName { get; private set; }

        /// <summary>Asynchronously registers the observer interface implementation with the host.
        /// This method also starts the host and enables it to start participating in the partition distribution process.</summary>
        /// <typeparam name="T">Implementation of your application-specific event observer.</typeparam>
        /// <returns>A task indicating that the <see cref="DocumentDB.ChangeFeedProcessor.ChangeFeedEventHost" /> instance has started.</returns>
        public async Task RegisterObserverAsync<T>() where T : IChangeFeedObserver, new()
        {
            this.observerFactory = new DocumentFeedObserverFactory<T>();
            await this.StartAsync();
        }

        /// <summary>Asynchronously shuts down the host instance. This method maintains the leases on all partitions currently held, and enables each 
        /// host instance to shut down cleanly by invoking the method with object.</summary> 
        /// <returns>A task that indicates the host instance has stopped.</returns>
        public async Task UnregisterObserversAsync()
        {
            await this.StopAsync(ChangeFeedObserverCloseReason.Shutdown);
            this.observerFactory = null;
        }

        async Task IPartitionObserver<DocumentServiceLease>.OnPartitionAcquiredAsync(DocumentServiceLease lease)
        {
            Debug.Assert(lease != null && !string.IsNullOrEmpty(lease.Owner), "lease");
            TraceLog.Informational(string.Format("Host '{0}' partition {1}: acquired!", this.HostName, lease.PartitionId));

#if DEBUG
            Interlocked.Increment(ref this.partitionCount);
#endif

            IChangeFeedObserver observer = this.observerFactory.CreateObserver();
            ChangeFeedObserverContext context = new ChangeFeedObserverContext { PartitionKeyRangeId = lease.PartitionId };
            CancellationTokenSource cancellation = new CancellationTokenSource();

            // Create ChangeFeedOptions to use for this worker.
            ChangeFeedOptions options = new ChangeFeedOptions
            {
                MaxItemCount = this.changeFeedOptions.MaxItemCount,
                PartitionKeyRangeId = this.changeFeedOptions.PartitionKeyRangeId,
                SessionToken = this.changeFeedOptions.SessionToken,
                StartFromBeginning = this.changeFeedOptions.StartFromBeginning,
                RequestContinuation = this.changeFeedOptions.RequestContinuation
            };

            var workerTask = await Task.Factory.StartNew(async () =>
            {
                ChangeFeedObserverCloseReason? closeReason = null;
                try
                {
                    try
                    {
                        await observer.OpenAsync(context);
                    }
                    catch (Exception ex)
                    {
                        TraceLog.Error(string.Format("IChangeFeedObserver.OpenAsync exception: {0}", ex));
                        closeReason = ChangeFeedObserverCloseReason.ObserverError;
                        throw;
                    }

                    options.PartitionKeyRangeId = lease.PartitionId;
                    if (!string.IsNullOrEmpty(lease.ContinuationToken))
                    {
                        options.RequestContinuation = lease.ContinuationToken;
                    }

                    IDocumentQuery<Document> query = this.documentClient.CreateDocumentChangeFeedQuery(this.collectionSelfLink, options);

                    TraceLog.Verbose(string.Format("Worker start: partition '{0}', continuation '{1}'", lease.PartitionId, lease.ContinuationToken));

                    try
                    {
                        while (this.isShutdown == 0)
                        {
                            do
                            {
                                DocumentClientException dcex = null;
                                FeedResponse<Document> response = null;

                                try
                                {
                                    response = await query.ExecuteNextAsync<Document>();
                                }
                                catch (DocumentClientException ex)
                                {
                                    if (StatusCode.NotFound != (StatusCode)ex.StatusCode &&
                                        StatusCode.TooManyRequests != (StatusCode)ex.StatusCode &&
                                        StatusCode.ServiceUnavailable != (StatusCode)ex.StatusCode)
                                    {
                                        throw;
                                    }

                                    dcex = ex;
                                }

                                if (dcex != null)
                                {
                                    const int ReadSessionNotAvailable = 1002;
                                    if (StatusCode.NotFound == (StatusCode)dcex.StatusCode && GetSubStatusCode(dcex) != ReadSessionNotAvailable)
                                    {
                                        // Most likely, the database or collection was removed while we were enumerating.
                                        // Shut down. The user will need to start over.
                                        // Note: this has to be a new task, can't await for shutdown here, as shudown awaits for all worker tasks.
                                        await Task.Factory.StartNew(() => this.StopAsync(ChangeFeedObserverCloseReason.ResourceGone));
                                        break;
                                    }
                                    else
                                    {
                                        Debug.Assert(StatusCode.TooManyRequests == (StatusCode)dcex.StatusCode || StatusCode.ServiceUnavailable == (StatusCode)dcex.StatusCode);
                                        TraceLog.Warning(string.Format("Partition {0}: retriable exception : {1}", context.PartitionKeyRangeId, dcex.Message));
                                        await Task.Delay(dcex.RetryAfter != TimeSpan.Zero ? dcex.RetryAfter : this.options.FeedPollDelay, cancellation.Token);
                                    }
                                }

                                if (response != null)
                                {
                                    if (response.Count > 0)
                                    {
                                        List<Document> docs = new List<Document>();
                                        docs.AddRange(response);

                                        try
                                        {
                                            await observer.ProcessChangesAsync(context, docs);
                                        }
                                        catch (Exception ex)
                                        {
                                            TraceLog.Error(string.Format("IChangeFeedObserver.ProcessChangesAsync exception: {0}", ex));
                                            closeReason = ChangeFeedObserverCloseReason.ObserverError;
                                            throw;
                                        }

                                        // Checkpoint after every successful delivery to the client.
                                        lease = await CheckpointAsync(lease, response.ResponseContinuation, context);
                                    }
                                    else if (string.IsNullOrEmpty(lease.ContinuationToken))
                                    {
                                        // Checkpoint if we've never done that for this lease.
                                        lease = await CheckpointAsync(lease, response.ResponseContinuation, context);
                                    }
                                }
                            }
                            while (query.HasMoreResults && this.isShutdown == 0);

                            if (this.isShutdown == 0)
                            {
                                await Task.Delay(this.options.FeedPollDelay, cancellation.Token);
                            }
                        } // Outer while (this.isShutdown == 0) loop.
                    }
                    catch (TaskCanceledException)
                    {
                        Debug.Assert(cancellation.IsCancellationRequested, "cancellation.IsCancellationRequested");
                        TraceLog.Informational(string.Format("Cancel signal received for partition {0} worker!", context.PartitionKeyRangeId));
                    }
                }
                catch (LeaseLostException)
                {
                    closeReason = ChangeFeedObserverCloseReason.LeaseLost;
                }
                catch (Exception ex)
                {
                    TraceLog.Error(string.Format("Partition {0} exception: {1}", context.PartitionKeyRangeId, ex));
                    if (!closeReason.HasValue)
                    {
                        closeReason = ChangeFeedObserverCloseReason.Unknown;
                    }
                }

                if (closeReason.HasValue)
                {
                    TraceLog.Informational(string.Format("Releasing lease for partition {0} due to an error, reason: {1}!", context.PartitionKeyRangeId, closeReason.Value));

                    // Note: this has to be a new task, because OnPartitionReleasedAsync awaits for worker task.
                    await Task.Factory.StartNew(async () => await this.partitionManager.TryReleasePartitionAsync(context.PartitionKeyRangeId, true, closeReason.Value));
                }

                TraceLog.Informational(string.Format("Partition {0}: worker finished!", context.PartitionKeyRangeId));
            });

            var newWorkerData = new WorkerData(workerTask, observer, context, cancellation);
            this.partitionKeyRangeIdToWorkerMap.AddOrUpdate(context.PartitionKeyRangeId, newWorkerData, (string id, WorkerData d) => { return newWorkerData; });
        }
        
        async Task IPartitionObserver<DocumentServiceLease>.OnPartitionReleasedAsync(DocumentServiceLease l, ChangeFeedObserverCloseReason reason)
        {
#if DEBUG
            Interlocked.Decrement(ref this.partitionCount);
#endif

            TraceLog.Informational(string.Format("Host '{0}' releasing partition {1}...", this.HostName, l.PartitionId));
            WorkerData workerData = null;
            if (this.partitionKeyRangeIdToWorkerMap.TryGetValue(l.PartitionId, out workerData))
            {
                workerData.Cancellation.Cancel();

                try
                { 
                    await workerData.Observer.CloseAsync(workerData.Context, reason);
                }
                catch (Exception ex)
                {
                    // Eat all client exceptions.
                    TraceLog.Error(string.Format("IChangeFeedObserver.CloseAsync: exception: {0}", ex));
                }

                await workerData.Task;
                this.partitionKeyRangeIdToWorkerMap.TryRemove(l.PartitionId, out workerData);
            }

            TraceLog.Informational(string.Format("Host '{0}' partition {1}: released!", this.HostName, workerData.Context.PartitionKeyRangeId));
        }

        static DocumentCollectionInfo CanoninicalizeCollectionInfo(DocumentCollectionInfo collectionInfo)
        {
            DocumentCollectionInfo result = collectionInfo;
            if (string.IsNullOrEmpty(result.ConnectionPolicy.UserAgentSuffix))
            {
                result = new DocumentCollectionInfo(collectionInfo);
                result.ConnectionPolicy.UserAgentSuffix = DefaultUserAgentSuffix;
            }

            return result;
        }

        async Task<DocumentServiceLease> CheckpointAsync(DocumentServiceLease lease, string continuation, ChangeFeedObserverContext context)
        {
            Debug.Assert(lease != null);
            Debug.Assert(!string.IsNullOrEmpty(continuation));

            DocumentServiceLease result = null;
            try
            {
                result = (DocumentServiceLease)await this.checkpointManager.CheckpointAsync(lease, continuation, lease.SequenceNumber + 1);

                Debug.Assert(result.ContinuationToken == continuation, "ContinuationToken was not updated!");
                TraceLog.Verbose(string.Format("Checkpoint: partition {0}, new continuation '{1}'", lease.PartitionId, continuation));
            }
            catch (LeaseLostException)
            {
                TraceLog.Warning(string.Format("Partition {0}: failed to checkpoint due to lost lease", context.PartitionKeyRangeId));
                throw;
            }
            catch (Exception ex)
            {
                TraceLog.Error(string.Format("Partition {0}: failed to checkpoint due to unexpected error: {1}", context.PartitionKeyRangeId, ex.Message));
                throw;
            }

            return await Task.FromResult<DocumentServiceLease>(result);
        }

        async Task InitializeAsync()
        {
            this.documentClient = new DocumentClient(this.collectionLocation.Uri, this.collectionLocation.MasterKey, this.collectionLocation.ConnectionPolicy);

            Uri databaseUri = UriFactory.CreateDatabaseUri(this.collectionLocation.DatabaseName);
            Database database = await this.documentClient.ReadDatabaseAsync(databaseUri);

            Uri collectionUri = UriFactory.CreateDocumentCollectionUri(this.collectionLocation.DatabaseName, this.collectionLocation.CollectionName);
            DocumentCollection collection = await this.documentClient.ReadDocumentCollectionAsync(collectionUri);
            this.collectionSelfLink = collection.SelfLink;

            // Beyond this point all access to colleciton is done via this self link: if collection is removed, we won't access new one using same name by accident.
            this.leasePrefix = string.Format(CultureInfo.InvariantCulture, "{0}_{1}_{2}", this.collectionLocation.Uri.Host, database.ResourceId, collection.ResourceId);

            var leaseManager = new DocumentServiceLeaseManager(
                this.auxCollectionLocation, 
                this.leasePrefix, 
                this.options.LeaseExpirationInterval, 
                this.options.LeaseRenewInterval);
            await leaseManager.InitializeAsync();

            this.leaseManager = leaseManager;
            this.checkpointManager = (ICheckpointManager)leaseManager;

            if (this.options.DiscardExistingLeases)
            {
                TraceLog.Warning(string.Format("Host '{0}': removing all leases, as requested by ChangeFeedHostOptions", this.HostName));
                await this.leaseManager.DeleteAllAsync();
            }

            // Note: lease store is never stale as we use monitored colleciton Rid as id prefix for aux collection.
            // Collection was removed and re-created, the rid would change.
            // If it's not deleted, it's not stale. If it's deleted, it's not stale as it doesn't exist.
            await this.leaseManager.CreateLeaseStoreIfNotExistsAsync();

            string[] rangeIds = await this.EnumPartitionKeyRangeIds(this.collectionSelfLink);
            Parallel.ForEach(rangeIds, async rangeId => await this.leaseManager.CreateLeaseIfNotExistAsync(rangeId));

            this.partitionManager = new PartitionManager<DocumentServiceLease>(this.HostName, this.leaseManager, this.options);
            await this.partitionManager.SubscribeAsync(this);
            await this.partitionManager.InitializeAsync();
        }

        async Task<string[]> EnumPartitionKeyRangeIds(string collectionSelfLink)
        {
            Debug.Assert(!string.IsNullOrWhiteSpace(collectionSelfLink), "collectionSelfLink");

            string partitionkeyRangesPath = string.Format(CultureInfo.InvariantCulture, "{0}/pkranges", collectionSelfLink);

            FeedResponse<PartitionKeyRange> response = null;
            List<string> ids = new List<string>();
            do
            {
                response = await this.documentClient.ReadPartitionKeyRangeFeedAsync(partitionkeyRangesPath, new FeedOptions { MaxItemCount = 1000 });
                foreach (var item in response)
                {
                    ids.Add(item.Id);
                }
            }
            while (!string.IsNullOrEmpty(response.ResponseContinuation));

            return ids.ToArray();
        }

        async Task StartAsync()
        {
            await this.InitializeAsync();
            await this.partitionManager.StartAsync();
        }

        async Task StopAsync(ChangeFeedObserverCloseReason reason)
        {
            if (Interlocked.CompareExchange(ref this.isShutdown, 1, 0) != 0)
            {
                return;
            }

            TraceLog.Informational(string.Format("Host '{0}': STOP signal received!", this.HostName));

            List<Task> closingTasks = new List<Task>();

            // Trigger stop for PartitionManager so it triggers shutdown of AcquireLease task and starts processor shutdown
            closingTasks.Add(this.partitionManager.StopAsync(reason));

            // Stop all workers.
            TraceLog.Informational(string.Format("Host '{0}': Cancelling {1} workers.", this.HostName, this.partitionKeyRangeIdToWorkerMap.Count));
            foreach (var item in this.partitionKeyRangeIdToWorkerMap.Values)
            {
                item.Cancellation.Cancel();
                closingTasks.Add(item.Task);
            }

            // wait for everything to shutdown
            TraceLog.Informational(string.Format("Host '{0}': Waiting for {1} closing tasks...", this.HostName, closingTasks.Count));
            await Task.WhenAll(closingTasks.ToArray());

            this.partitionKeyRangeIdToWorkerMap.Clear();

            if (this.leaseManager is IDisposable)
            {
                ((IDisposable)this.leaseManager).Dispose();
            }

            TraceLog.Informational(string.Format("Host '{0}': stopped.", this.HostName));
        }

        private int GetSubStatusCode(DocumentClientException exception)
        {
            Debug.Assert(exception != null);

            const string SubStatusHeaderName = "x-ms-substatus";
            string valueSubStatus = exception.ResponseHeaders.Get(SubStatusHeaderName);
            if (!string.IsNullOrEmpty(valueSubStatus))
            {
                int subStatusCode = 0;
                if (int.TryParse(valueSubStatus, NumberStyles.Integer, CultureInfo.InvariantCulture, out subStatusCode))
                {
                    return subStatusCode;
                }
            }

            return -1;
        }

        private class WorkerData
        {
            public WorkerData(Task task, IChangeFeedObserver observer, ChangeFeedObserverContext context, CancellationTokenSource cancellation)
            {
                this.Task = task;
                this.Observer = observer;
                this.Context = context;
                this.Cancellation = cancellation;
            }

            public Task Task { get; private set; }

            public IChangeFeedObserver Observer { get; private set; }

            public ChangeFeedObserverContext Context { get; private set; }

            public CancellationTokenSource Cancellation { get; private set; }
        }
    }
}
