using System;
using System.Collections.Generic;
using System.Configuration;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;

namespace CosmosDB.Sql.DotNet.MultiMaster
{
    internal sealed class MultiMasterScenario
    {
        private readonly string accountEndpoint;
        private readonly string accountKey;

        private IList<BasicWorker> workers;
        private ConflictWorker conflictWorker;

        public MultiMasterScenario()
        {
            this.accountEndpoint = ConfigurationManager.AppSettings["endpoint"];
            this.accountKey = ConfigurationManager.AppSettings["key"];

            string[] regions = ConfigurationManager.AppSettings["regions"].Split(
                new string[] { ";" },
                StringSplitOptions.RemoveEmptyEntries);

            string databaseName = ConfigurationManager.AppSettings["databaseName"];
            string manualCollectionName = ConfigurationManager.AppSettings["manualCollectionName"];
            string lwwCollectionName = ConfigurationManager.AppSettings["lwwCollectionName"];
            string udpCollectionName = ConfigurationManager.AppSettings["udpCollectionName"];
            string basicCollectionName = ConfigurationManager.AppSettings["basicCollectionName"];

            this.workers = new List<BasicWorker>();
            this.conflictWorker = new ConflictWorker(databaseName, basicCollectionName, manualCollectionName, lwwCollectionName, udpCollectionName);

            foreach (string region in regions)
            {
                ConnectionPolicy policy = new ConnectionPolicy
                {
                    ConnectionMode = ConnectionMode.Direct,
                    ConnectionProtocol = Protocol.Tcp,
                    UseMultipleWriteLocations = true,
                };
                policy.PreferredLocations.Add(region);
                DocumentClient client = new DocumentClient(new Uri(this.accountEndpoint), this.accountKey, policy, ConsistencyLevel.Eventual);

                this.workers.Add(new BasicWorker(client, databaseName, basicCollectionName));

                this.conflictWorker.AddClient(client);
            }
        }
        public async Task InitializeAsync()
        {
            await this.conflictWorker.InitializeAsync();
            Console.WriteLine("Initialized collections.");
        }
        public async Task RunBasicAsync()
        {
            Console.WriteLine("\n####################################################");
            Console.WriteLine("Basic Active-Active");
            Console.WriteLine("####################################################");

            Console.WriteLine("1) Starting insert loops across multiple regions ...");

            IList<Task> basicTask = new List<Task>();

            int documentsToInsertPerWorker = 100;

            foreach (BasicWorker worker in this.workers)
            {
                basicTask.Add(worker.RunLoopAsync(documentsToInsertPerWorker));
            }

            await Task.WhenAll(basicTask);

            basicTask.Clear();

            Console.WriteLine("2) Reading from every region ...");

            int expectedDocuments = this.workers.Count * documentsToInsertPerWorker;
            foreach (BasicWorker worker in this.workers)
            {
                basicTask.Add(worker.ReadAllAsync(expectedDocuments));
            }

            await Task.WhenAll(basicTask);

            basicTask.Clear();

            Console.WriteLine("3) Deleting all the documents ...");

            await this.workers[0].DeleteAllAsync();

            Console.WriteLine("####################################################");
        }
        public async Task RunManualConflictAsync()
        {
            Console.WriteLine("\n####################################################");
            Console.WriteLine("Manual Conflict Resolution");
            Console.WriteLine("####################################################");

            await this.conflictWorker.RunManualConflictAsync();
            Console.WriteLine("####################################################");
        }
        public async Task RunLWWAsync()
        {
            Console.WriteLine("\n####################################################");
            Console.WriteLine("LWW Conflict Resolution");
            Console.WriteLine("####################################################");

            await this.conflictWorker.RunLWWConflictAsync();
            Console.WriteLine("####################################################");
        }
        public async Task RunUDPAsync()
        {
            Console.WriteLine("\n####################################################");
            Console.WriteLine("UDP Conflict Resolution");
            Console.WriteLine("####################################################");

            await this.conflictWorker.RunUDPAsync();
            Console.WriteLine("####################################################");
        }
    }
}
