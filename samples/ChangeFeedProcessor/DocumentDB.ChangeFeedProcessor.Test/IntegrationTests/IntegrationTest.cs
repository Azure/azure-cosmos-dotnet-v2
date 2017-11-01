using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace DocumentDB.ChangeFeedProcessor.Test
{
    /// <summary>
    /// Base class for intergration tests.
    /// Serves the following:
    /// - Per derived test class, initialize/cleanup of the monitored collection. Each derived test class gets different monitored collection.
    ///   When using "run all tests", the collection used by each derived test class is cleaned up when last test of the class finishes, not later.
    /// - Per test method, initialize/cleanup of lease collection.
    /// - AssemblyInitialize/Cleanup.
    /// - Each derived class needs to register itself by calling RegisterTestClass.
    /// </summary>
    /// <remarks>
    /// [ClassInitialize]/[ClassCleanup] do not shine through inheritance, so each derived clsas must defined these.
    /// [ClassCleanup] is called in the very end and not exactly when last test from the class is done.
    /// Test method instance is not preserved across tests in same test class, can't share state in test class instance across test methods.
    /// </remarks>
    [TestClass]
    public class IntegrationTest
    {
        protected class TestClassData
        {
            internal readonly SemaphoreSlim classInitializeSyncRoot = new SemaphoreSlim(1, 1);
            internal readonly object testContextSyncRoot = new object();
            internal readonly int testCount;
            internal volatile int executedTestCount;
            internal DocumentCollectionInfo monitoredCollectionInfo;
            internal DocumentCollectionInfo leaseCollectionInfoTemplate;

            internal TestClassData(int testCount)
            {
                this.testCount = testCount;
            }
        }

        private const string leaseCollectionInfoPropertyName = "leaseCollectionInfo";
        protected static int monitoredOfferThroughput;
        protected static int leaseOfferThroughput;
        protected static readonly TimeSpan changeWaitTimeout = TimeSpan.FromSeconds(30);

        /// <summary>
        /// This dictionary has one entry per derived class.
        /// </summary>
        private static Dictionary<string, TestClassData> testClasses = new Dictionary<string, TestClassData>();
        private static object testClassesSyncRoot = new object(); 

        public TestContext TestContext { get; set; }

        protected DocumentCollectionInfo LeaseCollectionInfo
        {
            get { return (DocumentCollectionInfo)this.TestContext.Properties[leaseCollectionInfoPropertyName]; }
            set
            {
                lock (this.ClassData.testContextSyncRoot)
                {
                    TestContext.Properties[leaseCollectionInfoPropertyName] = value;
                }
            }
        }

        protected TestClassData ClassData
        {
            get { return testClasses[this.GetType().Name]; }
        }

        [AssemblyInitialize]
        public static void AssemblyInitialize(TestContext testContext)
        {
            System.Net.ServicePointManager.DefaultConnectionLimit = 1000;  // Default is 2.
            ThreadPool.SetMinThreads(1000, 1000);   // 32
            ThreadPool.SetMaxThreads(5000, 5000);   // 32
        }

        [AssemblyCleanup]
        public static async Task AssemblyCleanupAsync()
        {
            var endpoint = new Uri(ConfigurationManager.AppSettings["endpoint"]);
            var masterKey = ConfigurationManager.AppSettings["masterKey"];
            var databaseName = ConfigurationManager.AppSettings["databaseId"];

            using (var client = new DocumentClient(endpoint, masterKey))
            {
                await client.DeleteDatabaseAsync(UriFactory.CreateDatabaseUri(databaseName));
            }
        }

        [TestInitialize]
        public async Task TestInitializeAsync()
        {
            if (this.ClassData.monitoredCollectionInfo == null)
            {
                await this.ClassData.classInitializeSyncRoot.WaitAsync(); // Use semaphore as cannot await inside a lock.

                try
                {
                    if (this.ClassData.monitoredCollectionInfo == null)
                    {
                        this.ClassData.leaseCollectionInfoTemplate = await TestClassInitializeAsync(this, $"data_{this.GetType().Name}");
                    }
                }
                finally
                {
                    this.ClassData.classInitializeSyncRoot.Release();
                }
            }

            this.LeaseCollectionInfo = new DocumentCollectionInfo(this.ClassData.leaseCollectionInfoTemplate);
            this.LeaseCollectionInfo.CollectionName = $"leases_{this.GetType().Name}_{Guid.NewGuid().ToString()}";

            var leaseCollection = new DocumentCollection { Id = this.LeaseCollectionInfo.CollectionName };
            using (var client = new DocumentClient(this.LeaseCollectionInfo.Uri, this.LeaseCollectionInfo.MasterKey, this.LeaseCollectionInfo.ConnectionPolicy))
            {
                await Helper.CreateDocumentCollectionAsync(client, this.LeaseCollectionInfo.DatabaseName, leaseCollection, leaseOfferThroughput);
            }
        }

        [TestCleanup]
        public async Task TestCleanupAsync()
        {
            Debug.Assert(this.LeaseCollectionInfo != null);
            using (var client = new DocumentClient(this.LeaseCollectionInfo.Uri, this.LeaseCollectionInfo.MasterKey, this.LeaseCollectionInfo.ConnectionPolicy))
            {
                await client.DeleteDocumentCollectionAsync(UriFactory.CreateDocumentCollectionUri(this.LeaseCollectionInfo.DatabaseName, this.LeaseCollectionInfo.CollectionName));
            }

            var executedTestCount = Interlocked.Increment(ref this.ClassData.executedTestCount);
            if (this.ClassData.executedTestCount == this.ClassData.testCount)
            {
                await TestClassCleanupAsync(this);
            }
        }

        protected static void RegisterTestClass(Type testClassType)
        {
            Debug.Assert(testClassType != null);

            lock (testClassesSyncRoot)
            {
                testClasses[testClassType.Name] = new TestClassData(GetTestCount(testClassType));
            }
        }

        protected virtual Task FinishTestClassInitializeAsync()
        {
            return Task.CompletedTask;
        }

        private static async Task<DocumentCollectionInfo> TestClassInitializeAsync(IntegrationTest test, string monitoredCollectionName)
        {
            Debug.Assert(test != null);
            Debug.Assert(monitoredCollectionName != null);

            DocumentCollectionInfo leaseCollectionInfo;
            Helper.GetConfigurationSettings(
                out test.ClassData.monitoredCollectionInfo,
                out leaseCollectionInfo,
                out monitoredOfferThroughput,
                out leaseOfferThroughput);

            test.ClassData.monitoredCollectionInfo.CollectionName = monitoredCollectionName;

            var monitoredCollection = new DocumentCollection
            {
                Id = test.ClassData.monitoredCollectionInfo.CollectionName,
                PartitionKey = new PartitionKeyDefinition { Paths = new Collection<string> { "/id" } }
            };

            using (var client = new DocumentClient(test.ClassData.monitoredCollectionInfo.Uri, test.ClassData.monitoredCollectionInfo.MasterKey, test.ClassData.monitoredCollectionInfo.ConnectionPolicy))
            {
                await Helper.CreateDocumentCollectionAsync(client, test.ClassData.monitoredCollectionInfo.DatabaseName, monitoredCollection, monitoredOfferThroughput);
            }

            await test.FinishTestClassInitializeAsync();

            return leaseCollectionInfo;
        }

        private static async Task TestClassCleanupAsync(IntegrationTest test)
        {
            Debug.Assert(test != null);

            using (var client = new DocumentClient(test.ClassData.monitoredCollectionInfo.Uri, test.ClassData.monitoredCollectionInfo.MasterKey, test.ClassData.monitoredCollectionInfo.ConnectionPolicy))
            {
                await client.DeleteDocumentCollectionAsync(UriFactory.CreateDocumentCollectionUri(
                    test.ClassData.monitoredCollectionInfo.DatabaseName, test.ClassData.monitoredCollectionInfo.CollectionName));
            }
        }

        private static int GetTestCount(Type testType)
        {
            Debug.Assert(testType != null);

            int testMethodCount = 0;
            foreach (var method in testType.GetMethods())
            {
                if (method.GetCustomAttribute(typeof(TestMethodAttribute)) != null) testMethodCount++;
            }

            return testMethodCount;
        }
    }
}
