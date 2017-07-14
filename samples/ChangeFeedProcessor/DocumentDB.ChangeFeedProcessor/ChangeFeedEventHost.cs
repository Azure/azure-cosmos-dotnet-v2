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
    using System.Linq;
    using System.Runtime.ExceptionServices;
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
    ///     public Task ProcessChangesAsync(ChangeFeedObserverContext context, IReadOnlyList<Document> docs)
    ///     {
    ///         Console.WriteLine("Change feed: total {0} doc(s)", Interlocked.Add(ref s_totalDocs, docs.Count));
    ///         return Task.CompletedTask;
    ///     }
    /// }
    /// static async Task StartChangeFeedHost()
    /// {
    ///     string hostName = Guid.NewGuid().ToString();
    ///     DocumentCollectionInfo documentCollectionLocation = new DocumentCollectionInfo
    ///     {
    ///         Uri = new Uri("https://YOUR_SERVICE.documents.azure.com:443/"),
    ///         MasterKey = "YOUR_SECRET_KEY==",
    ///         DatabaseName = "db1",
    ///         CollectionName = "documents"
    ///     };
    ///     DocumentCollectionInfo leaseCollectionLocation = new DocumentCollectionInfo
    ///     {
    ///         Uri = new Uri("https://YOUR_SERVICE.documents.azure.com:443/"),
    ///         MasterKey = "YOUR_SECRET_KEY==",
    ///         DatabaseName = "db1",
    ///         CollectionName = "leases"
    ///     };
    ///     Console.WriteLine("Main program: Creating ChangeFeedEventHost...");
    ///     ChangeFeedEventHost host = new ChangeFeedEventHost(hostName, documentCollectionLocation, leaseCollectionLocation);
    ///     await host.RegisterObserverAsync<DocumentFeedObserver>();
    ///     Console.WriteLine("Main program: press Enter to stop...");
    ///     Console.ReadLine();
    ///     await host.UnregisterObserversAsync();
    /// }
    /// ]]>
    /// </code>
    /// </example>
    public class ChangeFeedEventHost : IPartitionObserver<DocumentServiceLease>
    {
        const string DefaultUserAgentSuffix = "changefeed-0.2";
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

        ConcurrentDictionary<string, CheckpointStats> statsSinceLastCheckpoint = new ConcurrentDictionary<string, CheckpointStats>();
        IChangeFeedObserverFactory observerFactory;
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
            this.observerFactory = new ChangeFeedObserverFactory<T>();
            await this.StartAsync();
        }

        /// <summary>
        /// Asynchronously registers the observer factory implementation with the host.
        /// This method also starts the host and enables it to start participating in the partition distribution process.
        /// </summary>
        /// <param name="factory">Implementation of your application-specific event observer factory.</typeparam>
        /// <returns>A task indicating that the <see cref="DocumentDB.ChangeFeedProcessor.ChangeFeedEventHost" /> instance has started.</returns>
        public async Task RegisterObserverFactoryAsync(IChangeFeedObserverFactory factory)
        {
            this.observerFactory = factory;
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

                    CheckpointStats checkpointStats = null;
                    if (!this.statsSinceLastCheckpoint.TryGetValue(lease.PartitionId, out checkpointStats) || checkpointStats == null)
                    {
                        // It could be that the lease was created by different host and we picked it up.
                        checkpointStats = this.statsSinceLastCheckpoint.AddOrUpdate(
                            lease.PartitionId, 
                            new CheckpointStats(), 
                            (partitionId, existingStats) => existingStats);
                        Trace.TraceWarning(string.Format("Added stats for partition '{0}' for which the lease was picked up after the host was started.", lease.PartitionId));
                    }

                    IDocumentQuery<Document> query = this.documentClient.CreateDocumentChangeFeedQuery(this.collectionSelfLink, options);

                    TraceLog.Verbose(string.Format("Worker start: partition '{0}', continuation '{1}'", lease.PartitionId, lease.ContinuationToken));

                    string lastContinuation = options.RequestContinuation;

                    try
                    {
                        while (this.isShutdown == 0)
                        {
                            do
                            {
                                ExceptionDispatchInfo exceptionDispatchInfo = null;
                                FeedResponse<Document> response = null;

                                try
                                {
                                    response = await query.ExecuteNextAsync<Document>();
                                    lastContinuation = response.ResponseContinuation;
                                }
                                catch (DocumentClientException ex)
                                {
                                    exceptionDispatchInfo = ExceptionDispatchInfo.Capture(ex);
                                }

                                if (exceptionDispatchInfo != null)
                                {
                                    DocumentClientException dcex = (DocumentClientException)exceptionDispatchInfo.SourceException;

                                    if (StatusCode.NotFound == (StatusCode)dcex.StatusCode && SubStatusCode.ReadSessionNotAvailable != (SubStatusCode)GetSubStatusCode(dcex))
                                    {
                                        // Most likely, the database or collection was removed while we were enumerating.
                                        // Shut down. The user will need to start over.
                                        // Note: this has to be a new task, can't await for shutdown here, as shudown awaits for all worker tasks.
                                        TraceLog.Error(string.Format("Partition {0}: resource gone (subStatus={1}). Aborting.", context.PartitionKeyRangeId, GetSubStatusCode(dcex)));
                                        await Task.Factory.StartNew(() => this.StopAsync(ChangeFeedObserverCloseReason.ResourceGone));
                                        break;
                                    }
                                    else if (StatusCode.Gone == (StatusCode)dcex.StatusCode)
                                    {
                                        SubStatusCode subStatusCode = (SubStatusCode)GetSubStatusCode(dcex);
                                        if (SubStatusCode.PartitionKeyRangeGone == subStatusCode)
                                        {
                                            bool isSuccess = await HandleSplitAsync(context.PartitionKeyRangeId, lastContinuation, lease.Id);
                                            if (!isSuccess)
                                            {
                                                TraceLog.Error(string.Format("Partition {0}: HandleSplit failed! Aborting.", context.PartitionKeyRangeId));
                                                await Task.Factory.StartNew(() => this.StopAsync(ChangeFeedObserverCloseReason.ResourceGone));
                                                break;
                                            }

                                            // Throw LeaseLostException so that we take the lease down.
                                            throw new LeaseLostException(lease, exceptionDispatchInfo.SourceException, true);
                                        }
                                        else if (SubStatusCode.Splitting == subStatusCode)
                                        {
                                            TraceLog.Warning(string.Format("Partition {0} is splitting. Will retry to read changes until split finishes. {1}", context.PartitionKeyRangeId, dcex.Message));
                                        }
                                        else
                                        {
                                            exceptionDispatchInfo.Throw();
                                        }
                                    }
                                    else if (StatusCode.TooManyRequests == (StatusCode)dcex.StatusCode ||
                                        StatusCode.ServiceUnavailable == (StatusCode)dcex.StatusCode)
                                    {
                                        TraceLog.Warning(string.Format("Partition {0}: retriable exception : {1}", context.PartitionKeyRangeId, dcex.Message));
                                    }
                                    else
                                    {
                                        exceptionDispatchInfo.Throw();
                                    }

                                    await Task.Delay(dcex.RetryAfter != TimeSpan.Zero ? dcex.RetryAfter : this.options.FeedPollDelay, cancellation.Token);
                                }

                                if (response != null)
                                {
                                    if (response.Count > 0)
                                    {
                                        List<Document> docs = new List<Document>();
                                        docs.AddRange(response);

                                        try
                                        {
                                            context.FeedResponse = response;
                                            await observer.ProcessChangesAsync(context, docs);
                                        }
                                        catch (Exception ex)
                                        {
                                            TraceLog.Error(string.Format("IChangeFeedObserver.ProcessChangesAsync exception: {0}", ex));
                                            closeReason = ChangeFeedObserverCloseReason.ObserverError;
                                            throw;
                                        }
                                        finally
                                        {
                                            context.FeedResponse = null;
                                        }
                                    }

                                    checkpointStats.ProcessedDocCount += (uint)response.Count;

                                    if (IsCheckpointNeeded(lease, checkpointStats))
                                    {
                                        lease = await CheckpointAsync(lease, response.ResponseContinuation, context);
                                        checkpointStats.Reset();
                                    }
                                    else if (response.Count > 0)
                                    {
                                        TraceLog.Informational(string.Format("Checkpoint: not checkpointing for partition {0}, {1} docs, new continuation '{2}' as frequency condition is not met", lease.PartitionId, response.Count, response.ResponseContinuation));
                                    }
                                }
                            }
                            while (query.HasMoreResults && this.isShutdown == 0);

                            if (this.isShutdown == 0)
                            {
                                await Task.Delay(this.options.FeedPollDelay, cancellation.Token);
                            }
                        } // Outer while (this.isShutdown == 0) loop.

                        closeReason = ChangeFeedObserverCloseReason.Shutdown;
                    }
                    catch (TaskCanceledException)
                    {
                        Debug.Assert(cancellation.IsCancellationRequested, "cancellation.IsCancellationRequested");
                        TraceLog.Informational(string.Format("Cancel signal received for partition {0} worker!", context.PartitionKeyRangeId));
                    }
                }
                catch (LeaseLostException ex)
                {
                    closeReason = ex.IsGone ? ChangeFeedObserverCloseReason.LeaseGone : ChangeFeedObserverCloseReason.LeaseLost;
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
                TraceLog.Informational(string.Format("Checkpoint: partition {0}, new continuation '{1}'", lease.PartitionId, continuation));
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

            Debug.Assert(result != null);
            return await Task.FromResult<DocumentServiceLease>(result);
        }

        async Task InitializeAsync()
        {
            this.documentClient = new DocumentClient(this.collectionLocation.Uri, this.collectionLocation.MasterKey, this.collectionLocation.ConnectionPolicy);

            Uri databaseUri = UriFactory.CreateDatabaseUri(this.collectionLocation.DatabaseName);
            Database database = await this.documentClient.ReadDatabaseAsync(databaseUri);

            Uri collectionUri = UriFactory.CreateDocumentCollectionUri(this.collectionLocation.DatabaseName, this.collectionLocation.CollectionName);
            ResourceResponse<DocumentCollection> collectionResponse = await this.documentClient.ReadDocumentCollectionAsync(
                collectionUri, 
                new RequestOptions { PopulateQuotaInfo = true });
            DocumentCollection collection = collectionResponse.Resource;
            this.collectionSelfLink = collection.SelfLink;

            // Grab the options-supplied prefix if present otherwise leave it empty.
            string optionsPrefix = this.options.LeasePrefix ?? string.Empty;

            // Beyond this point all access to collection is done via this self link: if collection is removed, we won't access new one using same name by accident.
            this.leasePrefix = string.Format(CultureInfo.InvariantCulture, "{0}{1}_{2}_{3}", optionsPrefix, this.collectionLocation.Uri.Host, database.ResourceId, collection.ResourceId);

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

            var ranges = new Dictionary<string, PartitionKeyRange>();
            foreach (var range in await this.EnumPartitionKeyRangesAsync(this.collectionSelfLink))
            {
                ranges.Add(range.Id, range);
            }

            TraceLog.Informational(string.Format("Source collection: '{0}', {1} partition(s), {2} document(s)", this.collectionLocation.CollectionName, ranges.Count, GetDocumentCount(collectionResponse)));

            await this.CreateLeases(ranges);

            this.partitionManager = new PartitionManager<DocumentServiceLease>(this.HostName, this.leaseManager, this.options);
            await this.partitionManager.SubscribeAsync(this);
            await this.partitionManager.InitializeAsync();
        }

        /// <summary>
        /// Create leases for new partitions and take care of split partitions.
        /// </summary>
        private async Task CreateLeases(IDictionary<string, PartitionKeyRange> ranges)
        {
            Debug.Assert(ranges != null);

            // Get leases after getting ranges, to make sure that no other hosts checked in continuation for split partition after we got leases.
            var existingLeases = new Dictionary<string, DocumentServiceLease>();
            foreach (var lease in await this.leaseManager.ListLeases())
            {
                existingLeases.Add(lease.PartitionId, lease);
            }

            var gonePartitionIds = new HashSet<string>();
            foreach (var partitionId in existingLeases.Keys)
            {
                if (!ranges.ContainsKey(partitionId)) gonePartitionIds.Add(partitionId);
            }

            var addedPartitionIds = new List<string>();
            foreach (var range in ranges)
            {
                if (!existingLeases.ContainsKey(range.Key)) addedPartitionIds.Add(range.Key);
            }

            // Create leases for new partitions, if there was split, use continuation from parent partition.
            var parentIdToChildLeases = new ConcurrentDictionary<string, ConcurrentQueue<DocumentServiceLease>>();
            await addedPartitionIds.ForEachAsync(
                async addedRangeId =>
                {
                    this.statsSinceLastCheckpoint.AddOrUpdate(
                        addedRangeId,
                        new CheckpointStats(),
                        (partitionId, existingStats) => existingStats);

                    string continuationToken = null;
                    string parentIds = string.Empty;
                    var range = ranges[addedRangeId];
                    if (range.Parents != null && range.Parents.Count > 0)   // Check for split.
                    {
                        foreach (var parentRangeId in range.Parents)
                        {
                            if (gonePartitionIds.Contains(parentRangeId))
                            {
                                // Transfer continiation from lease for gone parent to lease for its child partition.
                                Debug.Assert(existingLeases[parentRangeId] != null);

                                parentIds += parentIds.Length == 0 ? parentRangeId : "," + parentRangeId;
                                if (continuationToken != null)
                                {
                                    TraceLog.Warning(string.Format("Partition {0}: found more than one parent, new continuation '{1}', current '{2}', will use '{3}'", addedRangeId, existingLeases[parentRangeId].ContinuationToken, existingLeases[parentRangeId].ContinuationToken));
                                }

                                continuationToken = existingLeases[parentRangeId].ContinuationToken;
                            }
                        }
                    }

                    bool wasCreated = await this.leaseManager.CreateLeaseIfNotExistAsync(addedRangeId, continuationToken);

                    if (wasCreated)
                    {
                        if (parentIds.Length == 0)
                        {
                            TraceLog.Informational(string.Format("Created lease for partition '{0}', continuation '{1}'.", addedRangeId, continuationToken));
                        }
                        else
                        {
                            TraceLog.Informational(string.Format("Created lease for partition '{0}' as child of split partition(s) '{1}', continuation '{2}'.", addedRangeId, parentIds, continuationToken));
                        }
                    }
                    else
                    {
                        TraceLog.Warning(string.Format("Some other host created lease for '{0}' as child of split partition(s) '{1}', continuation '{2}'.", addedRangeId, parentIds, continuationToken));
                    }
                },
                this.options.DegreeOfParallelism);

            // Remove leases for splitted (and thus gone partitions) and update continuation token.
            await gonePartitionIds.ForEachAsync(
                async goneRangeId =>
                {
                    await this.leaseManager.DeleteAsync(existingLeases[goneRangeId]);
                    TraceLog.Informational(string.Format("Deleted lease for gone (splitted) partition '{0}', continuation '{1}'", goneRangeId, existingLeases[goneRangeId].ContinuationToken));

                    CheckpointStats removedStatsUnused;
                    this.statsSinceLastCheckpoint.TryRemove(goneRangeId, out removedStatsUnused);
                },
                this.options.DegreeOfParallelism);
        }

        async Task<List<PartitionKeyRange>> EnumPartitionKeyRangesAsync(string collectionSelfLink)
        {
            Debug.Assert(!string.IsNullOrWhiteSpace(collectionSelfLink), "collectionSelfLink");

            string partitionkeyRangesPath = string.Format(CultureInfo.InvariantCulture, "{0}/pkranges", collectionSelfLink);

            FeedResponse<PartitionKeyRange> response = null;
            var partitionKeyRanges = new List<PartitionKeyRange>();
            do
            {
                FeedOptions feedOptions = new FeedOptions { MaxItemCount = 1000, RequestContinuation = response != null ? response.ResponseContinuation : null };
                response = await this.documentClient.ReadPartitionKeyRangeFeedAsync(partitionkeyRangesPath, feedOptions);
                partitionKeyRanges.AddRange(response);
            }
            while (!string.IsNullOrEmpty(response.ResponseContinuation));

            return partitionKeyRanges;
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

        /// <summary>
        /// Handle split for given partition.
        /// </summary>
        /// <param name="partitionKeyRangeId">The id of the partition that was splitted, aka parent partition.</param>
        /// <param name="continuationToken">Continuation token on split partition before split.</param>
        /// <param name="leaseId">The id of the lease. This is needed to avoid extra call to ILeaseManager to get the lease by partitionId.</param>
        /// <returns>True on success, false on failure.</returns>
        private async Task<bool> HandleSplitAsync(string partitionKeyRangeId, string continuationToken, string leaseId)
        {
            Debug.Assert(!string.IsNullOrEmpty(partitionKeyRangeId));
            Debug.Assert(!string.IsNullOrEmpty(leaseId));

            TraceLog.Informational(string.Format("Partition {0} is gone due to split, continuation '{1}'", partitionKeyRangeId, continuationToken));

            List<PartitionKeyRange> allRanges = await this.EnumPartitionKeyRangesAsync(this.collectionSelfLink);

            var childRanges = new List<PartitionKeyRange>(allRanges.Where(range => range.Parents.Contains(partitionKeyRangeId)));
            if (childRanges.Count < 2)
            {
                TraceLog.Error(string.Format("Partition {0} had split but we failed to find at least 2 child paritions."));
                return false;
            }

            var tasks = new List<Task>();
            foreach (var childRange in childRanges)
            {
                tasks.Add(this.leaseManager.CreateLeaseIfNotExistAsync(childRange.Id, continuationToken));
                TraceLog.Informational(string.Format("Creating lease for partition '{0}' as child of partition '{1}', continuation '{2}'", childRange.Id, partitionKeyRangeId, continuationToken));
            }

            await Task.WhenAll(tasks);
            await this.leaseManager.DeleteAsync(new DocumentServiceLease { Id = leaseId });

            TraceLog.Informational(string.Format("Deleted lease for gone (splitted) partition '{0}' continuation '{1}'", partitionKeyRangeId, continuationToken));

            // Note: the rest is up to lease taker, that after waking up would consume these new leases.
            return true;
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

        private bool IsCheckpointNeeded(DocumentServiceLease lease, CheckpointStats checkpointStats)
        {
            Debug.Assert(lease != null);
            Debug.Assert(checkpointStats != null);

            if (checkpointStats.ProcessedDocCount == 0)
            {
                return false;
            }

            bool isCheckpointNeeded = true;

            if (this.options.CheckpointFrequency != null &&
                (this.options.CheckpointFrequency.ProcessedDocumentCount.HasValue || this.options.CheckpointFrequency.TimeInterval.HasValue))
            {
                // Note: if either condition is satisfied, we checkpoint.
                isCheckpointNeeded = false;
                if (this.options.CheckpointFrequency.ProcessedDocumentCount.HasValue)
                {
                    isCheckpointNeeded = checkpointStats.ProcessedDocCount >= this.options.CheckpointFrequency.ProcessedDocumentCount.Value;
                }

                if (this.options.CheckpointFrequency.TimeInterval.HasValue)
                {
                    isCheckpointNeeded = isCheckpointNeeded ||
                        DateTime.Now - checkpointStats.LastCheckpointTime >= this.options.CheckpointFrequency.TimeInterval.Value;
                }
            }

            return isCheckpointNeeded;
        }

        private static int GetDocumentCount(ResourceResponse<DocumentCollection> response)
        {
            Debug.Assert(response != null);

            var resourceUsage = response.ResponseHeaders["x-ms-resource-usage"];
            if (resourceUsage != null)
            {
                var parts = resourceUsage.Split(';');
                foreach (var part in parts)
                {
                    var name = part.Split('=');
                    if (string.Equals(name[0], "documentsCount", StringComparison.OrdinalIgnoreCase))
                    {
                        return int.Parse(name[1]);
                    }
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

        /// <summary>
        /// Stats since last checkpoint.
        /// </summary>
        private class CheckpointStats
        {
            internal uint ProcessedDocCount { get; set; }

            internal DateTime LastCheckpointTime { get; set; }

            internal void Reset()
            {
                this.ProcessedDocCount = 0;
                this.LastCheckpointTime = DateTime.Now;
            }
        }
    }
}
