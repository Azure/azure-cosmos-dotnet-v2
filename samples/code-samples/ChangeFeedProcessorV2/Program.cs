﻿//---------------------------------------------------------------------------------
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
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.ChangeFeedProcessor;
    using Microsoft.Azure.Documents.ChangeFeedProcessor.Logging;
    using Microsoft.Azure.Documents.Client;

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

        private static string hostName = Environment.MachineName + "@" + DateTime.Now.Ticks.ToString();

        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        /// <summary>
        ///  Main program function; called when program runs
        /// </summary>
        /// <param name="args">Command line parameters (not used)</param>
        ///
        public static void Main(string[] args)
        {
            if (args.Length == 1)
            {
                hostName = args[0];
            }
            Console.WriteLine("Change Feed Processor client Started at:{0} for HostName: {1} ", DateTime.Now.ToShortTimeString(), hostName);

            //Setting up Logging
            var tracelogProvider = new TraceLogProvider();
            using (tracelogProvider.OpenNestedContext(hostName))
            {
                LogProvider.SetCurrentLogProvider(tracelogProvider);
                // After this, create IChangeFeedProcessor instance and
                // start/stop it.
            }
            Program newApp = new Program();
            newApp.MainAsync().Wait();
        }

        /// <summary>
        /// Main Async function; checks for or creates monitored/lease
        /// collections and runs Change Feed Host
        /// (<see cref="RunChangeFeedHostAsync" />)
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

            await this.RunChangeFeedHostAsync();

        }


        /// <summary>
        /// Registers a change feed observer to update changes read on
        /// change feed to destination collection. Deregisters change feed
        /// observer and closes process when enter key is pressed
        /// </summary>
        /// <returns>A Task to allow asynchronous execution</returns>

        public async Task RunChangeFeedHostAsync()
        {
            // monitored collection info
            DocumentCollectionInfo documentCollectionInfo = new DocumentCollectionInfo
            {
                Uri = new Uri(this.monitoredUri),
                MasterKey = this.monitoredSecretKey,
                DatabaseName = this.monitoredDbName,
                CollectionName = this.monitoredCollectionName
            };

            DocumentCollectionInfo leaseCollectionInfo = new DocumentCollectionInfo
            {
                Uri = new Uri(this.leaseUri),
                MasterKey = this.leaseSecretKey,
                DatabaseName = this.leaseDbName,
                CollectionName = this.leaseCollectionName
            };
            DocumentFeedObserverFactory docObserverFactory = new DocumentFeedObserverFactory();
            ChangeFeedProcessorOptions feedProcessorOptions = new ChangeFeedProcessorOptions();

            // ie. customizing lease renewal interval to 15 seconds
            // can customize LeaseRenewInterval, LeaseAcquireInterval, LeaseExpirationInterval, FeedPollDelay
            feedProcessorOptions.LeaseRenewInterval = TimeSpan.FromSeconds(15);
            feedProcessorOptions.StartFromBeginning = true;
            ChangeFeedProcessorBuilder builder = new ChangeFeedProcessorBuilder();
            builder
                .WithHostName(hostName)
                .WithFeedCollection(documentCollectionInfo)
                .WithLeaseCollection(leaseCollectionInfo)
                .WithProcessorOptions(feedProcessorOptions)
                .WithObserverFactory(new DocumentFeedObserverFactory());

            //    .WithObserver<DocumentFeedObserver>();  or just pass a observer

            var result = await builder.BuildAsync();
            await result.StartAsync();
            Console.Read();
            await result.StopAsync();
        }

        /// <summary>
        /// Checks whether a collections exists. Creates a new collection if
        /// the collection does not exist.
        /// <para>WARNING: CreateCollectionIfNotExistsAsync will create a
        /// new collection with reserved throughput which has pricing
        /// implications. For details visit:
        /// https://azure.microsoft.com/en-us/pricing/details/cosmos-db/
        /// </para>
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
                // WARNING: CreateDocumentCollectionIfNotExistsAsync will
                // create a new collection with reserved throughput which
                // has pricing implications. For details visit:
                // https://azure.microsoft.com/en-us/pricing/details/cosmos-db/
                await client.CreateDocumentCollectionIfNotExistsAsync(
                    UriFactory.CreateDatabaseUri(databaseName),
                    new DocumentCollection { Id = collectionName },
                    new RequestOptions { OfferThroughput = throughput });
            }
        }
    }
}
