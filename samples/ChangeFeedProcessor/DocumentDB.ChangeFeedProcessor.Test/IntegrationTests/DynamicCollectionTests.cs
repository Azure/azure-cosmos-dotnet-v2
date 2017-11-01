using DocumentDB.ChangeFeedProcessor.Test.Observer;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DocumentDB.ChangeFeedProcessor.Test.IntegrationTests
{
    /// <summary>
    /// The collection is modified while Change Feed Processor is running.
    /// </summary>
    [TestClass]
    public class DynamicCollectionTests : IntegrationTest
    {
        const int documentCount = 513;

        [ClassInitialize]
        public static void ClassInitialize(TestContext testContext)
        {
            RegisterTestClass(typeof(DynamicCollectionTests));
        }

        [TestMethod]
        public async Task CountAddedDocuments()
        {
            int partitionCount = await Helper.GetPartitionCount(this.ClassData.monitoredCollectionInfo);
            int openedCount = 0, processedCount = 0;
            var allObserversStarted = new ManualResetEvent(false);
            var allDocsProcessed = new ManualResetEvent(false);

            var observerFactory = new TestObserverFactory(
                context => 
                {
                    int newCount = Interlocked.Increment(ref openedCount);
                    if (newCount == partitionCount) allObserversStarted.Set();
                    return Task.CompletedTask;
                },
                null,
                (ChangeFeedObserverContext context, IReadOnlyList<Document> docs) =>
                {
                    int newCount = Interlocked.Add(ref processedCount, docs.Count);
                    if (newCount == documentCount) allDocsProcessed.Set();
                    return Task.CompletedTask;
                });

            var host = new ChangeFeedEventHost(
                Guid.NewGuid().ToString(),
                this.ClassData.monitoredCollectionInfo,
                this.LeaseCollectionInfo,
                new ChangeFeedOptions { StartFromBeginning = false },
                new ChangeFeedHostOptions());
            await host.RegisterObserverFactoryAsync(observerFactory);

            var isStartOk = allObserversStarted.WaitOne(Debugger.IsAttached ? changeWaitTimeout + changeWaitTimeout : changeWaitTimeout);
            Assert.IsTrue(isStartOk, "Timed out waiting for observres to start");

            using (var client = new DocumentClient(this.ClassData.monitoredCollectionInfo.Uri, this.ClassData.monitoredCollectionInfo.MasterKey, this.ClassData.monitoredCollectionInfo.ConnectionPolicy))
            {
                await Helper.CreateDocumentsAsync(
                    client,
                    UriFactory.CreateDocumentCollectionUri(this.ClassData.monitoredCollectionInfo.DatabaseName, this.ClassData.monitoredCollectionInfo.CollectionName),
                    documentCount);
            }

            allDocsProcessed.WaitOne(changeWaitTimeout);

            try
            {
                Assert.AreEqual(documentCount, processedCount, "Wrong processedCount");
            }
            finally
            {
                await host.UnregisterObserversAsync();
            }
        }
    }
}
