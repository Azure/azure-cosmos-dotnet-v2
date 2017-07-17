//--------------------------------------------------------------------------------- 
// <copyright file="Program.cs" company="Microsoft">
// Microsoft (R)  Azure SDK 
// Software Development Kit 
//  
// Copyright (c) Microsoft Corporation. All rights reserved.   
// 
// THIS CODE AND INFORMATION ARE PROVIDED "AS IS" WITHOUT WARRANTY OF ANY KIND,  
// EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE IMPLIED WARRANTIES  
// OF MERCHANTABILITY AND/OR FITNESS FOR A PARTICULAR PURPOSE.  
// </copyright>
//---------------------------------------------------------------------------------

namespace ChangeFeedMigrationSample
{
    using System;
    using System.Configuration;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.ChangeFeedProcessor;
    using Microsoft.Azure.Documents.Client;

    /// ------------------------------------------------------------------------------------------------
    /// This sample demonstrates using change processor library to read changes from source collection 
    /// to destination collection 
    /// ------------------------------------------------------------------------------------------------
    public class Program
    {
        // Modify EndPointUrl and PrimaryKey to connect to your own subscription
        private string monitoredUri = ConfigurationManager.AppSettings["monitoredUri"];
        private string monitoredSecretKey = ConfigurationManager.AppSettings["monitoredSecretKey"];
        private string monitoredDbName = ConfigurationManager.AppSettings["monitoredDbName"];
        private string monitoredCollectionName = ConfigurationManager.AppSettings["monitoredCollectionName"];
        private int monitoredThroughput = int.Parse(ConfigurationManager.AppSettings["monitoredThroughput"]);

        // optional setting to store lease collection on different account
        // set lease Uri, secretKey and DbName to same as monitored if both collections 
        // are on the same account
        private string leaseUri = ConfigurationManager.AppSettings["leaseUri"];
        private string leaseSecretKey = ConfigurationManager.AppSettings["leaseSecretKey"];
        private string leaseDbName = ConfigurationManager.AppSettings["leaseDbName"];
        private string leaseCollectionName = ConfigurationManager.AppSettings["leaseCollectionName"];
        private int leaseThroughput = int.Parse(ConfigurationManager.AppSettings["leaseThroughput"]);

        // destination collection for data movement in this sample  
        // could be same or different account 
        private string destUri = ConfigurationManager.AppSettings["destUri"];
        private string destSecretKey = ConfigurationManager.AppSettings["destSecretKey"];
        private string destDbName = ConfigurationManager.AppSettings["destDbName"];
        private string destCollectionName = ConfigurationManager.AppSettings["destCollectionName"];
        private int destThroughput = int.Parse(ConfigurationManager.AppSettings["destThroughput"]);

        public static void Main(string[] args)
        {
            Console.WriteLine("Change Feed Migration Sample");
            Program newApp = new Program();

            // create collections
            newApp.CreateCollectionAsync(
                newApp.monitoredUri, 
                newApp.monitoredSecretKey, 
                newApp.monitoredDbName, 
                newApp.monitoredCollectionName, 
                newApp.monitoredThroughput).Wait();

            newApp.CreateCollectionAsync(
                newApp.leaseUri, 
                newApp.leaseSecretKey, 
                newApp.leaseDbName, 
                newApp.leaseCollectionName, 
                newApp.leaseThroughput).Wait();

            newApp.CreateCollectionAsync(
                newApp.destUri, 
                newApp.destSecretKey, 
                newApp.destDbName, 
                newApp.destCollectionName, 
                newApp.destThroughput).Wait();

            // run change feed processor
            newApp.RunChangeFeedHostAsync().Wait(); 
        }

        public async Task CreateCollectionAsync(string newUri, string secretKey, string dbName, string collectionName, int throughput)
        {
            // connecting client 
            using (DocumentClient client = new DocumentClient(new Uri(newUri), secretKey))
            {
                await client.CreateDatabaseIfNotExistsAsync(new Database { Id = dbName });

                // create monitor collection if it does not exist 
                // WARNING: CreateDocumentCollectionIfNotExistsAsync will create a new 
                // with reserved through pul which has pricing implications. For details
                // visit: https://azure.microsoft.com/en-us/pricing/details/cosmos-db/
                await client.CreateDocumentCollectionIfNotExistsAsync(
                    UriFactory.CreateDatabaseUri(dbName),
                    new DocumentCollection { Id = collectionName },
                    new RequestOptions { OfferThroughput = throughput });
            }     
        }

        public async Task RunChangeFeedHostAsync()
        {
            string hostName = Guid.NewGuid().ToString();

            // monitored collection info 
            DocumentCollectionInfo documentCollectionLocation = new DocumentCollectionInfo
            {
                Uri = new Uri(this.monitoredUri),
                MasterKey = this.monitoredSecretKey,
                DatabaseName = this.monitoredDbName,
                CollectionName = this.monitoredCollectionName
            };

            // lease collection info 
            DocumentCollectionInfo leaseCollectionLocation = new DocumentCollectionInfo
            {
                Uri = new Uri(this.leaseUri),
                MasterKey = this.leaseSecretKey,
                DatabaseName = this.leaseDbName,
                CollectionName = this.leaseCollectionName
            };

            // destination collection info 
            DocumentCollectionInfo destCollInfo = new DocumentCollectionInfo
            {
                Uri = new Uri(this.destUri),
                MasterKey = this.destSecretKey,
                DatabaseName = this.destDbName,
                CollectionName = this.destCollectionName
            };

            // Customizable change feed option and host options 
            ChangeFeedOptions feedOptions = new ChangeFeedOptions();

            // ie customize StartFromBeginning so change feed reads from beginning
            // can customize MaxItemCount, PartitonKeyRangeId, RequestContinuation, SessionToken and StartFromBeginning
            feedOptions.StartFromBeginning = true;

            ChangeFeedHostOptions feedHostOptions = new ChangeFeedHostOptions();

            // ie. customizing lease renewal interval to 15 seconds
            // can customize LeaseRenewInterval, LeaseAcquireInterval, LeaseExpirationInterval, FeedPollDelay 
            feedHostOptions.LeaseRenewInterval = TimeSpan.FromSeconds(15);

            using (DocumentClient destClient = new DocumentClient(destCollInfo.Uri, destCollInfo.MasterKey))
            {
                DocumentFeedObserverFactory docObserverFactory = new DocumentFeedObserverFactory(destCollInfo, destClient);

                ChangeFeedEventHost host = new ChangeFeedEventHost(hostName, documentCollectionLocation, leaseCollectionLocation, feedOptions, feedHostOptions);

                await host.RegisterObserverFactoryAsync(docObserverFactory);

                Console.WriteLine("Running... Press enter to stop.");
                Console.ReadLine();

                await host.UnregisterObserversAsync();
            }
        }
    }
}
