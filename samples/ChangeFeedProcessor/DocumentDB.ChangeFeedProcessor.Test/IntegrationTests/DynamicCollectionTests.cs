﻿using DocumentDB.ChangeFeedProcessor.Test.Observer;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
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

            var isStartOk = allObserversStarted.WaitOne(
                Debugger.IsAttached ? IntegrationTest.changeWaitTimeout + IntegrationTest.changeWaitTimeout : IntegrationTest.changeWaitTimeout);
            Assert.IsTrue(isStartOk, "Timed out waiting for observres to start");

            using (var client = new DocumentClient(this.ClassData.monitoredCollectionInfo.Uri, this.ClassData.monitoredCollectionInfo.MasterKey, this.ClassData.monitoredCollectionInfo.ConnectionPolicy))
            {
                await Helper.CreateDocumentsAsync(
                    client,
                    UriFactory.CreateDocumentCollectionUri(this.ClassData.monitoredCollectionInfo.DatabaseName, this.ClassData.monitoredCollectionInfo.CollectionName),
                    documentCount);
            }

            allDocsProcessed.WaitOne(IntegrationTest.changeWaitTimeout);

            try
            {
                Assert.AreEqual(documentCount, processedCount, "Wrong processedCount");
            }
            finally
            {
                await host.UnregisterObserversAsync();
            }
        }

        [TestMethod]
        public async Task TestStartTime()
        {
            var collectionUri = UriFactory.CreateDocumentCollectionUri(this.ClassData.monitoredCollectionInfo.DatabaseName, this.ClassData.monitoredCollectionInfo.CollectionName);
            using (var client = new DocumentClient(this.ClassData.monitoredCollectionInfo.Uri, this.ClassData.monitoredCollectionInfo.MasterKey, this.ClassData.monitoredCollectionInfo.ConnectionPolicy))
            {
                await client.CreateDocumentAsync(collectionUri, JsonConvert.DeserializeObject("{\"id\": \"doc1\"}"));

                // In worst case (long transaction, heavy load, the atomicity of StartTime is 5 sec).
                // For this case (different transactions) it's OK to wait timestamp precision time.
                await Task.Delay(TimeSpan.FromSeconds(1));
                DateTime timeInBeweeen = DateTime.Now;
                await Task.Delay(TimeSpan.FromSeconds(1));

                await client.CreateDocumentAsync(collectionUri, JsonConvert.DeserializeObject("{\"id\": \"doc2\"}"));

                int partitionCount = await Helper.GetPartitionCount(this.ClassData.monitoredCollectionInfo);
                var allDocsProcessed = new ManualResetEvent(false);

                var processedDocs = new List<Document>();
                var observerFactory = new TestObserverFactory(
                    null,
                    null,
                    (context, docs) => 
                    {
                        processedDocs.AddRange(docs);
                        foreach (var doc in docs)
                        {
                            if (doc.Id == "doc2") allDocsProcessed.Set();
                        }
                        return Task.CompletedTask;
                    });

                var host = new ChangeFeedEventHost(
                    Guid.NewGuid().ToString(),
                    this.ClassData.monitoredCollectionInfo,
                    this.LeaseCollectionInfo,
                    new ChangeFeedOptions { StartTime = timeInBeweeen },
                    new ChangeFeedHostOptions());
                await host.RegisterObserverFactoryAsync(observerFactory);

                var isStartOk = allDocsProcessed.WaitOne(
                    Debugger.IsAttached ? IntegrationTest.changeWaitTimeout + IntegrationTest.changeWaitTimeout : IntegrationTest.changeWaitTimeout);
                Assert.IsTrue(isStartOk, "Timed out waiting for docs to process");

                try
                {
                    Assert.AreEqual(1, processedDocs.Count, "Wrong processed count");
                    Assert.AreEqual("doc2", processedDocs[0].Id, "Wrong doc.id");
                }
                finally
                {
                    await host.UnregisterObserversAsync();
                }
            }
        }
    }
}
