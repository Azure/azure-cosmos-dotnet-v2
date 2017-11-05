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
    /// <remarks>
    /// Since monitored collection is created once and tests are only reading, all test methods are independent and can run in parallel.
    /// </remarks>
    [TestClass]
    public class StaticCollectionTests : IntegrationTest
    {
        const int documentCount = 1519;

        [ClassInitialize]
        public static void ClassInitialize(TestContext testContext)
        {
            RegisterTestClass(typeof(StaticCollectionTests));
        }

        [TestMethod]
        public async Task CountDocumentsInCollection_NormalCase()
        {
            int partitionKeyRangeCount = await Helper.GetPartitionCount(this.ClassData.monitoredCollectionInfo);
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
                this.ClassData.monitoredCollectionInfo,
                this.LeaseCollectionInfo, 
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
                this.ClassData.monitoredCollectionInfo,
                this.LeaseCollectionInfo,
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
            int partitionKeyRangeCount = await Helper.GetPartitionCount(this.ClassData.monitoredCollectionInfo);
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
                this.ClassData.monitoredCollectionInfo,
                this.LeaseCollectionInfo,
                new ChangeFeedOptions { StartFromBeginning = true },
                new ChangeFeedHostOptions { MaxPartitionCount = partitionKeyRangeCount / 2});
            await host1.RegisterObserverFactoryAsync(observerFactory);

            var host2 = new ChangeFeedEventHost(
                Guid.NewGuid().ToString(),
                this.ClassData.monitoredCollectionInfo,
                this.LeaseCollectionInfo,
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
            int partitionKeyRangeCount = await Helper.GetPartitionCount(this.ClassData.monitoredCollectionInfo);
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
                this.ClassData.monitoredCollectionInfo,
                this.LeaseCollectionInfo,
                new ChangeFeedOptions { StartFromBeginning = true, MaxItemCount = 2 },
                new ChangeFeedHostOptions());
            await host.RegisterObserverFactoryAsync(observerFactory);

            quarterDocsProcessed.WaitOne(Debugger.IsAttached ? changeWaitTimeout + changeWaitTimeout : changeWaitTimeout);

            await host.UnregisterObserversAsync();

            Assert.AreEqual(partitionKeyRangeCount, openedCount, "Wrong closedCount");
            Assert.AreEqual(partitionKeyRangeCount, closedCount, "Wrong closedCount");
        }

        protected override async Task FinishTestClassInitializeAsync()
        {
            using (var client = new DocumentClient(this.ClassData.monitoredCollectionInfo.Uri, this.ClassData.monitoredCollectionInfo.MasterKey, this.ClassData.monitoredCollectionInfo.ConnectionPolicy))
            {
                await Helper.CreateDocumentsAsync(
                    client,
                    UriFactory.CreateDocumentCollectionUri(this.ClassData.monitoredCollectionInfo.DatabaseName, this.ClassData.monitoredCollectionInfo.CollectionName),
                    documentCount);
            }
        }
    }
}
