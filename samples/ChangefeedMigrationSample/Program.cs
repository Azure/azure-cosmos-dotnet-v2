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
        // Modify EndPointUrl and PrimaryKey to connect to your own subscription
        private string monitoredUri = "https://interntest.documents.azure.com:443/";
        private string monitoredSecretKey = "mXBmwssUDqDNL03M0qkmMBYizwpeIrLqCyFNUOGQsGCEeLRRkWJDleEORnVNzfQ13dkiIyxfhgVM4QAQLzQQzg==";
        private string monitoredDbName = "SmartHome";
        private string monitoredCollectionName = "Nest";

        // optional setting to store lease collection on different account
        // set lease Uri, secretKey and DbName to same as monitored if both collections 
        // are on the same account
        private string leaseUri = "https://interntest.documents.azure.com:443/";
        private string leaseSecretKey = "mXBmwssUDqDNL03M0qkmMBYizwpeIrLqCyFNUOGQsGCEeLRRkWJDleEORnVNzfQ13dkiIyxfhgVM4QAQLzQQzg==";
        private string leaseDbName = "SmartHome";
        private string leaseCollectionName = "Lease";

        static void Main(string[] args)
        {
            Console.WriteLine("Change Feed Migration Sample");
            Program newApp = new Program();
            newApp.RunChangeFeedProcessorAsync();
            Console.WriteLine("Running... Press any key to stop.");
            Console.ReadKey();
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

            // Customizable change feed option and host options 
            ChangeFeedOptions feedOptions = new ChangeFeedOptions();
            // ie customize StartFromBeginning so change feed reads from beginning
            // can customize MaxItemCount, PartitonKeyRangeId, RequestContinuation, SessionToken and StartFromBeginning
            feedOptions.StartFromBeginning = true;

            ChangeFeedHostOptions feedHostOptions = new ChangeFeedHostOptions();
            // ie. customizing lease renewal interval to 15 seconds
            // can customize LeaseRenewInterval, LeaseAcquireInterval, LeaseExpirationInterval, FeedPollDelay 
            feedHostOptions.LeaseRenewInterval = TimeSpan.FromSeconds(15);

            ChangeFeedEventHost host = new ChangeFeedEventHost(hostName, documentCollectionLocation, leaseCollectionLocation, feedOptions, feedHostOptions);

            await host.RegisterObserverAsync<DocumentFeedObserver>();
            Console.WriteLine("Main program: press Enter to stop...");
            Console.ReadLine();
            await host.UnregisterObserversAsync();
        }
    }
}
