using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocumentDB.Samples.AutoScale
{
    using System.Threading;
    using System.Timers;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;

    using Timer = System.Timers.Timer;

    /// <summary>
    /// Throttling handler for a collection
    /// </summary>
    public class CollectionThrottlingHandler
    {
        private static bool CollectionThroughputIncreased = false;
        private static volatile CollectionThrottlingHandler instance;
        private static readonly object Singleton = new Object();
        private static readonly object CollectionLock = new object();
        private static Timer CollectionThroughputIncreaseTimer;
        private static int CurrentThrottledInstances = 0;

        private CollectionThrottlingHandler() { }

        public static CollectionThrottlingHandler Instance
        {
            // Ensure that there is only one instance of this class

            get
            {
                if (instance == null)
                {
                    lock (Singleton)
                    {
                        if (instance == null)
                        {
                            instance = new CollectionThrottlingHandler();
                        }
                    }
                }

                return instance;
            }
        }

        private readonly DocumentClient client;

        private readonly string collectionId;

        private readonly string databaseId;

        private readonly int incrementValue;

        private readonly int maxThroughPut;

        private readonly int throttlingThreshold;

        /// <summary>
        /// Initializes a new instance of the <see cref="CollectionThrottlingHandler"/> class.
        /// </summary>
        /// <param name="accountUri">The account URI.</param>
        /// <param name="authKey">The authentication key.</param>
        /// <param name="collectionId">The collection identifier.</param>
        /// <param name="databaseId">The database identifier.</param>
        /// <param name="settings">The settings.</param>
        public CollectionThrottlingHandler(string accountUri, string authKey, string collectionId, string databaseId, CollectionThrottlingSettings settings)
        {
            this.client = new DocumentClient(new Uri(accountUri), authKey);
            this.collectionId = collectionId;
            this.databaseId = databaseId;
            this.incrementValue = settings.ThroughputIncreaseOnThrottle;
            this.maxThroughPut = settings.MaxCollectionThroughPut;
            this.throttlingThreshold = settings.MinThrottlingInstances;
            CollectionThroughputIncreaseTimer = new Timer(settings.ThrottlingResetTimeInSeconds * 1000);
            CollectionThroughputIncreaseTimer.Elapsed += ResetThrottlingWindow;
        }

        /// <summary>
        /// Handles the DocumentClientException to take action if it is related to throttling
        /// </summary>
        /// <param name="docClientException">The document client exception.</param>
        /// <returns></returns>
        public void HandleDocumentClientException(DocumentClientException docClientException)
        {
            if (docClientException.StatusCode != null && int.Parse(docClientException.StatusCode.Value.ToString()) == 429)
            {
                if (Interlocked.Increment(ref CurrentThrottledInstances) < this.throttlingThreshold)
                {
                    return;
                }

                DocumentCollection collection = this.GetCollection(this.databaseId, this.collectionId);
                if (collection == null)
                {
                    return;
                }

                lock (CollectionLock)
                {
                    //// Ensure that only one thread increases the throughput in case of multi-threaded clients.
                    //// The CollectionThroughputIncreased flag will be reset based on the settings

                    if (!CollectionThroughputIncreased)
                    {
                        Console.WriteLine("Throttling limit reached..");
                        this.IncreaseCollectionThroughPut(this.client, collection, this.incrementValue).GetAwaiter().GetResult();
                        CollectionThroughputIncreased = true;
                        CollectionThroughputIncreaseTimer.Enabled = true;
                    }
                }
            }
        }

        /// <summary>
        /// Increases the collection throughput based on the increment value.
        /// </summary>
        /// <param name="client">The client.</param>
        /// <param name="collection">The collection.</param>
        /// <param name="incrementValue">The increment value.</param>
        private async Task IncreaseCollectionThroughPut(DocumentClient client, DocumentCollection collection, int incrementValue)
        {
            Offer offer = this.client.CreateOfferQuery().Where(o => o.ResourceLink == collection.SelfLink).AsEnumerable().Single();
            int currentOfferThroughPut = ((OfferV2)offer).Content.OfferThroughput;
           
            if (currentOfferThroughPut == this.maxThroughPut)
            {
                return;
            }

            int replacementThroughPut = currentOfferThroughPut + incrementValue;
            if (replacementThroughPut > this.maxThroughPut)
            {
                replacementThroughPut = this.maxThroughPut;
            }

            await this.client.ReplaceOfferAsync(new OfferV2(offer, replacementThroughPut));
            offer = this.client.CreateOfferQuery().Where(o => o.ResourceLink == collection.SelfLink).AsEnumerable().Single();
            int replacedOfferThroughPut = ((OfferV2)offer).Content.OfferThroughput;
            Console.WriteLine("Increased the offer throughput to {0}", replacedOfferThroughPut);
        }

        /// <summary>
        /// Gets the document collection.
        /// </summary>
        /// <param name="databaseName">Name of the database.</param>
        /// <param name="collectionName">Name of the collection.</param>
        /// <returns></returns>
        private DocumentCollection GetCollection(string databaseName, string collectionName)
        {
          return this.client.CreateDocumentCollectionQuery(UriFactory.CreateDatabaseUri(databaseName))
                .Where(c => c.Id == collectionName).AsEnumerable().FirstOrDefault();
        }

        /// <summary>
        /// Resets the throttling window.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="ElapsedEventArgs"/> instance containing the event data.</param>
        private static void ResetThrottlingWindow(object sender, ElapsedEventArgs e)
        {
            CollectionThroughputIncreased = false;
            CurrentThrottledInstances = 0;
        }
    }
}
