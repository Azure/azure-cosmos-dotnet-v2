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

namespace ChangeFeedProcessor
{
    using System;
    using System.Configuration;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.ChangeFeedProcessor;
    using Microsoft.Azure.Documents.Client;

    /// ------------------------------------------------------------------------------------------------
    /// <summary> This sample demonstrates using change processor library to read changes from source collection 
    /// to destination collection </summary> 
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

        /// <summary>
        ///  Main program function; called when program runs
        /// </summary>
        /// <param name="args">Command line parameters (not used)</param>
        public static void Main(string[] args)
        {
            Console.WriteLine("Change Feed Processor client Started at: " + DateTime.Now.ToShortTimeString());
            Program newApp = new Program();
            newApp.MainAsync().Wait();
        }

        /// <summary>
        /// Main Async function; checks for or creates monitored/lease collections and runs
        /// Change Feed Host (RunChangeFeedHostAsync)
        /// </summary>
        /// <returns>A Task to allow asynchronous execution</returns>
        private async Task MainAsync()
        {
            await this.CreateCollectionIfNotExistsAsync(
                this.monitoredUri,
                this.monitoredSecretKey,
                this.monitoredDbName,
                this.monitoredCollectionName,
                this.monitoredThroughput);

            await this.CreateCollectionIfNotExistsAsync(
                this.leaseUri,
                this.leaseSecretKey,
                this.leaseDbName,
                this.leaseCollectionName,
                this.leaseThroughput);

            await this.CreateCollectionIfNotExistsAsync(
                this.destUri,
                this.destSecretKey,
                this.destDbName,
                this.destCollectionName,
                this.destThroughput);

            await this.RunChangeFeedHostAsync();
        }

        /// <summary>
        /// Checks whether collections exists. Creates new collection if collection does not exist 
        /// WARNING: CreateCollectionIfNotExistsAsync will create a new 
        /// with reserved throughput which has pricing implications. For details
        /// visit: https://azure.microsoft.com/en-us/pricing/details/cosmos-db/
        /// </summary>
        /// <param name="endPointUri">End point URI for account </param>
        /// <param name="secretKey">Primary key to access the account </param>
        /// <param name="databaseName">Name of database </param>
        /// <param name="collectionName">Name of collection</param>
        /// <param name="throughput">Amount of throughput to provision</param>
        /// <returns>A Task to allow asynchronous execution</returns>
        public async Task CreateCollectionIfNotExistsAsync(string endPointUri, string secretKey, string databaseName, string collectionName, int throughput)
        {
            // connecting client 
            using (DocumentClient client = new DocumentClient(new Uri(endPointUri), secretKey))
            {
                await client.CreateDatabaseIfNotExistsAsync(new Database { Id = databaseName });

                // create collection if it does not exist 
                // WARNING: CreateDocumentCollectionIfNotExistsAsync will create a new 
                // with reserved throughput which has pricing implications. For details
                // visit: https://azure.microsoft.com/en-us/pricing/details/cosmos-db/
                await client.CreateDocumentCollectionIfNotExistsAsync(
                    UriFactory.CreateDatabaseUri(databaseName),
                    new DocumentCollection { Id = collectionName },
                    new RequestOptions { OfferThroughput = throughput });
            }
        }

        /// <summary>
        /// Registers change feed observer to update changes read on change feed to destination 
        /// collection. Deregisters change feed observer and closes process when enter key is pressed
        /// </summary>
        /// <returns>A Task to allow asynchronous execution</returns>
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
                DocumentFeedObserverFactory docObserverFactory = new DocumentFeedObserverFactory(destClient, destCollInfo);

                ChangeFeedEventHost host = new ChangeFeedEventHost(hostName, documentCollectionLocation, leaseCollectionLocation, feedOptions, feedHostOptions);

                await host.RegisterObserverFactoryAsync(docObserverFactory);

                Console.WriteLine("Running... Press enter to stop.");
                Console.ReadLine();

                await host.UnregisterObserversAsync();
            }
        }
    }
}
