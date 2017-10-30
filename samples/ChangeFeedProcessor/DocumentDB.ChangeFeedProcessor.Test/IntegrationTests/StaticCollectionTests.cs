using DocumentDB.ChangeFeedProcessor.Test.Observer;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace DocumentDB.ChangeFeedProcessor.Test
{
    /// <summary>
    /// Collection is not modified as part of tests.
    /// </summary>
    [TestClass]
    public class StaticCollectionTests
    {
        static DocumentCollectionInfo monitoredCollectionInfo;
        static DocumentCollectionInfo leaseCollectionInfo;
        static int monitoredOfferThroughput, leaseOfferThroughput;
        const int documentCount = 1519;
        static readonly TimeSpan changeWaitTimeout = TimeSpan.FromSeconds(30);

        [ClassInitialize]
        public static async Task ClassInitialize(TestContext testContext)
        {
            System.Net.ServicePointManager.DefaultConnectionLimit = 1000;  // Default is 2.
            ThreadPool.SetMinThreads(1000, 1000);   // 32
            ThreadPool.SetMaxThreads(5000, 5000);   // 32

            Helper.GetConfigurationSettings(out monitoredCollectionInfo, out leaseCollectionInfo, out monitoredOfferThroughput, out leaseOfferThroughput);

            var monitoredCollection = new DocumentCollection
            {
                Id = monitoredCollectionInfo.CollectionName,
                PartitionKey = new PartitionKeyDefinition { Paths = new Collection<string> { "/id" } }
            };

            using (var client = new DocumentClient(monitoredCollectionInfo.Uri, monitoredCollectionInfo.MasterKey, monitoredCollectionInfo.ConnectionPolicy))
            {
                await Helper.CreateDocumentCollectionAsync(client, monitoredCollectionInfo.DatabaseName, monitoredCollection, monitoredOfferThroughput);

                await Helper.CreateDocumentsAsync(
                    client,
                    UriFactory.CreateDocumentCollectionUri(monitoredCollectionInfo.DatabaseName, monitoredCollectionInfo.CollectionName),
                    documentCount);
            }
        }

        [ClassCleanup]
        public static async Task ClassCleanup()
        {
            using (var client = new DocumentClient(monitoredCollectionInfo.Uri, monitoredCollectionInfo.MasterKey, monitoredCollectionInfo.ConnectionPolicy))
            {
                await client.DeleteDatabaseAsync(UriFactory.CreateDatabaseUri(monitoredCollectionInfo.DatabaseName));
            }
        }

        [TestInitialize]
        public async Task TestInitialize()
        {
            var leaseCollection = new DocumentCollection { Id = leaseCollectionInfo.CollectionName };
            using (var client = new DocumentClient(leaseCollectionInfo.Uri, leaseCollectionInfo.MasterKey, leaseCollectionInfo.ConnectionPolicy))
            {
                await Helper.CreateDocumentCollectionAsync(client, leaseCollectionInfo.DatabaseName, leaseCollection, leaseOfferThroughput);
            }
        }

        [TestCleanup]
        public async Task TestCleanup()
        {
            using (var client = new DocumentClient(leaseCollectionInfo.Uri, leaseCollectionInfo.MasterKey, leaseCollectionInfo.ConnectionPolicy))
            {
                await client.DeleteDocumentCollectionAsync(UriFactory.CreateDocumentCollectionUri(leaseCollectionInfo.DatabaseName, leaseCollectionInfo.CollectionName));
            }
        }

        [TestMethod]
        public async Task CountDocumentsInCollection_NormalCase()
        {
            int partitionKeyRangeCount = await Helper.GetPartitionCount(monitoredCollectionInfo);
            int openedCount = 0, closedCount = 0, processedCount = 0;
            var allDocsProcessed = new ManualResetEvent(false);

            var observerFactory = new TestObserverFactory(
                context => { Interlocked.Increment(ref openedCount); return Task.CompletedTask; },
                (context, reason) => { Interlocked.Increment(ref closedCount); return Task.CompletedTask; },
                (ChangeFeedObserverContext context, IReadOnlyList<Document> docs) =>
                {
                    int newCount = Interlocked.Add(ref processedCount, docs.Count);
                    if (newCount == documentCount)
                    {
                        allDocsProcessed.Set();
                    }
                    return Task.CompletedTask;
                });

            var host = new ChangeFeedEventHost(
                Guid.NewGuid().ToString(), 
                monitoredCollectionInfo, 
                leaseCollectionInfo, 
                new ChangeFeedOptions { StartFromBeginning = true },
                new ChangeFeedHostOptions());
            await host.RegisterObserverFactoryAsync(observerFactory);

            allDocsProcessed.WaitOne(Debugger.IsAttached ? changeWaitTimeout + changeWaitTimeout : changeWaitTimeout);

            try
            {
                Assert.AreEqual(partitionKeyRangeCount, openedCount, "Wrong openedCount");
                Assert.AreEqual(documentCount, processedCount, "Wrong processedCount");
            }
            finally
            {
                await host.UnregisterObserversAsync();
            }

            Assert.AreEqual(partitionKeyRangeCount, closedCount, "Wrong closedCount");
        }

        [TestMethod]
        public async Task CountDocumentsInCollection_ProcessChangesThrows()
        {
            int processedCount = 0;
            var allDocsProcessed = new ManualResetEvent(false);
            bool isFirstChangeNotification = false; // Make sure there was at least one throw.
            int throwCount = 0;

            var observerFactory = new TestObserverFactory((ChangeFeedObserverContext context, IReadOnlyList<Document> docs) =>
            {
                bool shouldThrow = (isFirstChangeNotification || new Random().Next(0, 1) == 1) && throwCount < 10;
                isFirstChangeNotification = false;

                if (shouldThrow)
                {
                    Interlocked.Increment(ref throwCount);
                    throw new Exception("Error injection exception from observer!");
                }

                int newCount = Interlocked.Add(ref processedCount, docs.Count);
                if (newCount == documentCount)
                {
                    allDocsProcessed.Set();
                }
                return Task.CompletedTask;
            });

            var host = new ChangeFeedEventHost(
                Guid.NewGuid().ToString(),
                monitoredCollectionInfo,
                leaseCollectionInfo,
                new ChangeFeedOptions { StartFromBeginning = true },
                new ChangeFeedHostOptions());
            await host.RegisterObserverFactoryAsync(observerFactory);

            allDocsProcessed.WaitOne(Debugger.IsAttached ? changeWaitTimeout + changeWaitTimeout + changeWaitTimeout : changeWaitTimeout + changeWaitTimeout);

            try
            {
                Assert.AreEqual(documentCount, processedCount);
            }
            finally
            {
                await host.UnregisterObserversAsync();
            }
        }

        [TestMethod]
        public async Task CountDocumentsInCollection_TwoHosts()
        {
            int partitionKeyRangeCount = await Helper.GetPartitionCount(monitoredCollectionInfo);
            Assert.IsTrue(partitionKeyRangeCount > 1, "Prerequisite failed: expected monitored collection with at least 2 partitions.");

            int processedCount = 0;
            var allDocsProcessed = new ManualResetEvent(false);

            var observerFactory = new TestObserverFactory(
                (ChangeFeedObserverContext context, IReadOnlyList<Document> docs) =>
                {
                    int newCount = Interlocked.Add(ref processedCount, docs.Count);
                    if (newCount == documentCount)
                    {
                        allDocsProcessed.Set();
                    }
                    return Task.CompletedTask;
                });

            var host1 = new ChangeFeedEventHost(
                Guid.NewGuid().ToString(),
                monitoredCollectionInfo,
                leaseCollectionInfo,
                new ChangeFeedOptions { StartFromBeginning = true },
                new ChangeFeedHostOptions { MaxPartitionCount = partitionKeyRangeCount / 2});
            await host1.RegisterObserverFactoryAsync(observerFactory);

            var host2 = new ChangeFeedEventHost(
                Guid.NewGuid().ToString(),
                monitoredCollectionInfo,
                leaseCollectionInfo,
                new ChangeFeedOptions { StartFromBeginning = true },
                new ChangeFeedHostOptions { MaxPartitionCount = partitionKeyRangeCount - partitionKeyRangeCount / 2 });
            await host2.RegisterObserverFactoryAsync(observerFactory);

            allDocsProcessed.WaitOne(Debugger.IsAttached ? changeWaitTimeout + changeWaitTimeout : changeWaitTimeout);

            try
            {
                Assert.AreEqual(documentCount, processedCount, "Wrong processedCount");
            }
            finally
            {
                await host1.UnregisterObserversAsync();
                await host2.UnregisterObserversAsync();
            }
        }

        [TestMethod]
        public async Task StopAtFullSpeed()
        {
            int partitionKeyRangeCount = await Helper.GetPartitionCount(monitoredCollectionInfo);
            int openedCount = 0, closedCount = 0, processedCount = 0;
            var quarterDocsProcessed = new ManualResetEvent(false);

            var observerFactory = new TestObserverFactory(
                context => { Interlocked.Increment(ref openedCount); return Task.CompletedTask; },
                (context, reason) => { Interlocked.Increment(ref closedCount); return Task.CompletedTask; },
                (ChangeFeedObserverContext context, IReadOnlyList<Document> docs) =>
                {
                    int newCount = Interlocked.Add(ref processedCount, docs.Count);
                    if (newCount >= documentCount / 4)
                    {
                        quarterDocsProcessed.Set();
                    }
                    return Task.CompletedTask;
                });

            var host = new ChangeFeedEventHost(
                Guid.NewGuid().ToString(),
                monitoredCollectionInfo,
                leaseCollectionInfo,
                new ChangeFeedOptions { StartFromBeginning = true, MaxItemCount = 2 },
                new ChangeFeedHostOptions());
            await host.RegisterObserverFactoryAsync(observerFactory);

            quarterDocsProcessed.WaitOne(Debugger.IsAttached ? changeWaitTimeout + changeWaitTimeout : changeWaitTimeout);

            await host.UnregisterObserversAsync();

            Assert.AreEqual(partitionKeyRangeCount, openedCount, "Wrong closedCount");
            Assert.AreEqual(partitionKeyRangeCount, closedCount, "Wrong closedCount");
        }
    }
}
