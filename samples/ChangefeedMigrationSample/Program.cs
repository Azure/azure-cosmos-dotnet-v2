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

namespace ChangefeedMigrationSample
{
    using DocumentDB.ChangeFeedProcessor;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    // ------------------------------------------------------------------------------------------------
    // This sample demonstrates using change processor library to read changes from source collection 
    // to destination collection 
    // ------------------------------------------------------------------------------------------------

    public class DeviceReading
    {
        [JsonProperty("id")]
        public string Id;

        [JsonProperty("deviceId")]
        public string DeviceId;

        [JsonConverter(typeof(IsoDateTimeConverter))]
        [JsonProperty("readingTime")]
        public DateTime ReadingTime;

        [JsonProperty("metricType")]
        public string MetricType;

        [JsonProperty("unit")]
        public string Unit;

        [JsonProperty("metricValue")]
        public double MetricValue;
    }

    class DocumentFeedObserver : IChangeFeedObserver
    {
        private static int s_totalDocs = 0;

        public Task OpenAsync(ChangeFeedObserverContext context)
        {
            Console.WriteLine("Worker opened, {0}", context.PartitionKeyRangeId);
            return Task.CompletedTask;  // Requires targeting .NET 4.6+.
        }

        public Task CloseAsync(ChangeFeedObserverContext context, ChangeFeedObserverCloseReason reason)
        {
            Console.WriteLine("Worker closed, {0}", context.PartitionKeyRangeId);
            return Task.CompletedTask;
        }

        public Task ProcessChangesAsync(ChangeFeedObserverContext context, IReadOnlyList<Document> docs)
        {
            DocumentCollectionInfo destCollectionLocation = new DocumentCollectionInfo
            {
                Uri = new Uri("httpsURI"),
                MasterKey = "PrimaryKey",
                DatabaseName = "DestDBname",
                CollectionName = "DestCollName"
            };

            DocumentClient newClient = new DocumentClient(destCollectionLocation.Uri, destCollectionLocation.MasterKey);

            newClient.CreateDatabaseIfNotExistsAsync(new Database { Id = destCollectionLocation.DatabaseName });

            // create dest collection if it does not exist
            DocumentCollection destCollection = new DocumentCollection();
            destCollection.Id = destCollectionLocation.CollectionName;

            //destCollection.PartitionKey.Paths.Add("add partition key if applicable");

            newClient.CreateDocumentCollectionIfNotExistsAsync(
                UriFactory.CreateDatabaseUri(destCollectionLocation.DatabaseName),
                destCollection,
                new RequestOptions { OfferThroughput = 500 });

            Console.WriteLine("Change feed: total {0} doc(s)", Interlocked.Add(ref s_totalDocs, docs.Count));
            foreach (Document doc in docs)
            {
                Console.WriteLine(doc.ToString());
                newClient.UpsertDocumentAsync(
                    UriFactory.CreateDocumentCollectionUri(destCollectionLocation.DatabaseName, destCollectionLocation.CollectionName),
                    doc);
            }
            return Task.CompletedTask;
        }
    }

    class Program
    {
        // Modify EndPointUrl and PrimaryKey to connect to your own subscription
        private string uri = "https://URI";
        private string secretKey = "primaryKey";

        private string dbName = "yourdbName";
        private string monitoredCollectionName = "monitoredCollectionName";
        private string leaseCollectionName = "leaseCollectionName";

        static void Main(string[] args)
        {
            Console.WriteLine("ChangeFeed App");
            Program newApp = new ChangefeedMigrationSample.Program();
            // Thread 1 comment out for thread 2 
            newApp.RunChangeFeedProcessor();
            // Thread 2 comment out for thread 1 
            // UpdateDb(EndPointUrl, PrimaryKey); 
            Console.ReadKey();
        }

        async void RunChangeFeedProcessor()
        {
            // connect client 
            DocumentClient client = new DocumentClient(new Uri(uri), this.secretKey);
            await client.CreateDatabaseIfNotExistsAsync(new Database { Id = this.dbName });

            // create monitor collection if it does not exist 
            // WARNING: CreateDocumentCollectionIfNotExistsAsync will create a new 
            // with reserved through pul which has pricing implications. For details
            // visit: https://azure.microsoft.com/en-us/pricing/details/cosmos-db/
            await client.CreateDocumentCollectionIfNotExistsAsync(
                UriFactory.CreateDatabaseUri(this.dbName),
                new DocumentCollection { Id = this.monitoredCollectionName },
                new RequestOptions { OfferThroughput = 400 });

            // create lease collect if it does not exist
            // WARNING: CreateDocumentCollectionIfNotExistsAsync will create a new 
            // with reserved through pul which has pricing implications. For details
            // visit: https://azure.microsoft.com/en-us/pricing/details/cosmos-db/
            await client.CreateDocumentCollectionIfNotExistsAsync(
            UriFactory.CreateDatabaseUri(this.dbName),
            new DocumentCollection { Id = this.leaseCollectionName },
            new RequestOptions { OfferThroughput = 400 });
            await StartChangeFeedHost();
        }

        async Task StartChangeFeedHost()
        {
            string hostName = Guid.NewGuid().ToString();

            // monitored collection info 
            DocumentCollectionInfo documentCollectionLocation = new DocumentCollectionInfo
            {
                Uri = new Uri(this.uri),
                MasterKey = this.secretKey,
                DatabaseName = this.dbName,
                CollectionName = this.monitoredCollectionName
            };

            // lease collection info 
            DocumentCollectionInfo leaseCollectionLocation = new DocumentCollectionInfo
            {
                Uri = new Uri(this.uri),
                MasterKey = this.secretKey,
                DatabaseName = this.dbName,
                CollectionName = this.leaseCollectionName
            };

            ChangeFeedEventHost host = new ChangeFeedEventHost(hostName, documentCollectionLocation, leaseCollectionLocation);
            await host.RegisterObserverAsync<DocumentFeedObserver>();
            Console.WriteLine("Main program: press Enter to stop...");
            Console.ReadLine();
            await host.UnregisterObserversAsync();
        }

        private async Task UpdateDb()
        {
            // Use this function to update data in monitored collection in seperate thread
            // Returns all documents in the collection.
            Console.WriteLine("Connect client");
            DocumentClient client = new DocumentClient(new Uri(this.uri), this.secretKey);
            Uri collectionUri = UriFactory.CreateDocumentCollectionUri(this.dbName, this.monitoredCollectionName);

            Console.WriteLine("Connect database");
            try
            {
                Database database = await client.ReadDatabaseAsync(UriFactory.CreateDatabaseUri(this.dbName));
            }
            catch (Exception e)
            {
                Console.WriteLine("Connect database failed");
                Console.WriteLine(e.Message);
            }

            // Create new documents 
            System.Console.WriteLine("Create JSON document");
            for (int i = 0; i < 10; i++)
            {
                System.Console.WriteLine("Creating document XMS-0004 {0}", i);
                await client.UpsertDocumentAsync(
                UriFactory.CreateDocumentCollectionUri(this.dbName, this.monitoredCollectionName),
                new DeviceReading
                {
                    Id = String.Join("XMS-005-FE24C_", i.ToString()),
                    DeviceId = "XMS-0005",
                    MetricType = "Temperature",
                    MetricValue = 80.00 + (float)i,
                    Unit = "Fahrenheit",
                    ReadingTime = DateTime.UtcNow
                });
                System.Threading.Thread.Sleep(5000);
            }
        }

    }
}
