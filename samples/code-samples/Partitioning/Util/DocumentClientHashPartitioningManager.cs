namespace DocumentDB.Samples.Partitioning.Utils
{
    using DocumentDB.Samples.Partitioning.Partitioners;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    /// <summary>
    /// Implements methods to add or remove additional partitions and handle data migration to a HashPartitionResolver. Internally
    /// uses <see cref="TransitionHashPartitionResolver"/> to handle requests during transit.
    /// </summary>
    public class DocumentClientHashPartitioningManager
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DocumentClientHashPartitioningManager"/> class.
        /// </summary>
        /// <param name="partitionKeyExtractor">The partition key extractor function.</param>
        /// <param name="client">The DocumentDB client instance.</param>
        /// <param name="database">The database to partition.</param>
        /// <param name="initialPartitionCount">The number of initial partitions to create.</param>
        /// <param name="readMode">The mode to process requests in during data migrations.</param>
        public DocumentClientHashPartitioningManager(
            Func<object, string> partitionKeyExtractor, 
            DocumentClient client, 
            Database database, 
            int initialPartitionCount,
            TransitionReadMode readMode = TransitionReadMode.ReadBoth)
        {
            this.Client = client;
            this.Database = database;
            this.Client.PartitionResolvers[database.SelfLink] = new ManagedHashPartitionResolver(partitionKeyExtractor, client, database, initialPartitionCount);
            this.ReadMode = readMode;
        }

        /// <summary>
        /// Gets or sets the DocumentDB client instance.
        /// </summary>
        public DocumentClient Client { get; set; }

        /// <summary>
        /// Gets or sets the Database to be re-partitioned.
        /// </summary>
        public Database Database { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="TransitionReadMode"/> for requests during data migration.
        /// </summary>
        public TransitionReadMode ReadMode { get; set; }

        /// <summary>
        /// Add a partition (collection) to the consistent hash ring.
        /// </summary>
        /// <returns>The Task object for the asynchronous execution.</returns>
        public async Task<int> AddPartition()
        {
            ManagedHashPartitionResolver currentResolver = (ManagedHashPartitionResolver)this.Client.PartitionResolvers[this.Database.SelfLink];
            return await this.RepartitionData(currentResolver.PartitionCount + 1);
        }

        /// <summary>
        /// Removes a partition (collection) from the consistent hash ring.
        /// </summary>
        /// <returns>The Task object for the asynchronous execution.</returns>
        public async Task<int> RemovePartition()
        {
            ManagedHashPartitionResolver currentResolver = (ManagedHashPartitionResolver)this.Client.PartitionResolvers[this.Database.SelfLink];
            int numberOfMovedDocuments = await this.RepartitionData(currentResolver.PartitionCount - 1);
            await this.Client.DeleteDocumentCollectionAsync(currentResolver.CollectionLinks.Last());

            return numberOfMovedDocuments;
        }

        /// <summary>
        /// Internal method to rebalance data across a different number of partitions.
        /// </summary>
        /// <param name="newPartitionCount">The target number of partitions.</param>
        /// <returns>The Task object for the asynchronous execution.</returns>
        private async Task<int> RepartitionData(int newPartitionCount)
        {
            // No-op, just delete the last collection.
            if (newPartitionCount == 0)
            {
                return 0;
            }

            ManagedHashPartitionResolver currentResolver = (ManagedHashPartitionResolver)this.Client.PartitionResolvers[this.Database.SelfLink];

            var nextPartitionResolver = new ManagedHashPartitionResolver(
                currentResolver.PartitionKeyExtractor,
                this.Client,
                this.Database,
                newPartitionCount);

            TransitionHashPartitionResolver transitionaryResolver = new TransitionHashPartitionResolver(
                currentResolver, 
                nextPartitionResolver,
                this.ReadMode);
            
            this.Client.PartitionResolvers[this.Database.SelfLink] = transitionaryResolver;

            // Move data between partitions. Here it's one by one, but you can change this to implement inserts
            // in bulk using stored procedures (bulkImport and bulkDelete), or run them in parallel. Another 
            // improvement to this would be push down the check for partitioning function down to the individual
            // collections as a LINQ/SQL query.
            int numberOfMovedDocuments = 0;
            foreach (string collectionLink in currentResolver.CollectionLinks)
            {
                ResourceFeedReader<Document> feedReader = this.Client.CreateDocumentFeedReader(collectionLink);

                while (feedReader.HasMoreResults)
                {
                    foreach (Document document in await feedReader.ExecuteNextAsync())
                    {
                        object partitionKey = nextPartitionResolver.GetPartitionKey(document);
                        string newCollectionLink = nextPartitionResolver.ResolveForCreate(partitionKey);

                        if (newCollectionLink != collectionLink)
                        {
                            numberOfMovedDocuments++;
                            await this.Client.DeleteDocumentAsync(document.SelfLink);
                            await this.Client.CreateDocumentAsync(newCollectionLink, document);
                        }
                    }
                }
            }

            this.Client.PartitionResolvers[this.Database.SelfLink] = nextPartitionResolver;
            return numberOfMovedDocuments;
        }
    }
}
