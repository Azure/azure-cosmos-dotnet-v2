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
    using Microsoft.Azure.Documents.ChangeFeedProcessor;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using System;
    using System.Threading.Tasks;

    /// ------------------------------------------------------------------------------------------------
    /// This sample demonstrates using change processor library to read changes from source collection 
    /// to destination collection 
    /// ------------------------------------------------------------------------------------------------


    class Program
    {
        // Modify to connect to your own subscription and database for monitored collection 
        private string monitoredUri = "https://URI";
        private string monitoredSecretKey = "authKey";
        private string monitoredDbName = "monitoredDatabaseId";
        private string monitoredCollectionName = "monitoredCollId";

        // Modify to connect to your own subscription and database for lease collection 
        // optional: setting to store lease collection on different account
        // set lease Uri, SecretKey and DBName to same as monitored if both collections 
        private string leaseUri = "https://URI";
        private string leaseSecretKey = "authKey";
        private string leaseDbName = "leaseDatabaseId";
        private string leaseCollectionName = "leaseCollId";

        // destination collection for data movement in this sample  
        // could be same or different account 
        private string destUri = "https://URI";
        private string destSecretKey = "authKey";
        private string destDbName = "leaseDatabaseId";
        private string destCollectionName = "leaseCollId";

        static void Main(string[] args)
        {
            Console.WriteLine("Change Feed Migration Sample");
            Program newApp = new Program();
            newApp.RunChangeFeedProcessorAsync();
            Console.WriteLine("Main Running... Press enter to stop.");
            Console.ReadLine();
        }

        async void RunChangeFeedProcessorAsync()
        {
            // connect monitored client 
            DocumentClient monitoredClient = new DocumentClient(new Uri(monitoredUri), this.monitoredSecretKey);
            await monitoredClient.CreateDatabaseIfNotExistsAsync(new Database { Id = this.monitoredDbName });

            // create monitor collection if it does not exist 
            // WARNING: CreateDocumentCollectionIfNotExistsAsync will create a new 
            // with reserved through pul which has pricing implications. For details
            // visit: https://azure.microsoft.com/en-us/pricing/details/cosmos-db/
            await monitoredClient.CreateDocumentCollectionIfNotExistsAsync(
                UriFactory.CreateDatabaseUri(this.monitoredDbName),
                new DocumentCollection { Id = this.monitoredCollectionName },
                new RequestOptions { OfferThroughput = 400 });

            // connect monitored client 
            DocumentClient leaseClient = new DocumentClient(new Uri(leaseUri), this.leaseSecretKey);
            await leaseClient.CreateDatabaseIfNotExistsAsync(new Database { Id = this.leaseDbName });
            // create lease collect if it does not exist
            // WARNING: CreateDocumentCollectionIfNotExistsAsync will create a new 
            // with reserved through pul which has pricing implications. For details
            // visit: https://azure.microsoft.com/en-us/pricing/details/cosmos-db/
            await leaseClient.CreateDocumentCollectionIfNotExistsAsync(
            UriFactory.CreateDatabaseUri(this.leaseDbName),
            new DocumentCollection { Id = this.leaseCollectionName },
            new RequestOptions { OfferThroughput = 400 });
            await RunChangeFeedHostAsync();
        }

        async Task RunChangeFeedHostAsync()
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

            DocumentFeedObserverFactory docObserverFactory = new DocumentFeedObserverFactory(destCollInfo);

            ChangeFeedEventHost host = new ChangeFeedEventHost(hostName, documentCollectionLocation, leaseCollectionLocation, feedOptions, feedHostOptions);

            await host.RegisterObserverFactoryAsync(docObserverFactory);

            Console.WriteLine("Running... Press enter to stop.");
            Console.ReadLine();

            await host.UnregisterObserversAsync();
      
        }
    }
}
