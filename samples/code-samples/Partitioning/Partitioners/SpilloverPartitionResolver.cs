namespace DocumentDB.Samples.Partitioning.Partitioners
{
    using DocumentDB.Samples.Shared.Util;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Creates a PartitionResolver that automatically creates collections as they fill up.
    /// </summary>
    public class SpilloverPartitionResolver : IPartitionResolver
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SpilloverPartitionResolver" /> class.
        /// </summary>
        /// <param name="client">The DocumentDB client instance.</param>
        /// <param name="database">The database to use.</param>
        /// <param name="collectionSpec">The specification/template to create collections from.</param>
        /// <param name="collectionIdPrefix">The prefix to use for collections.</param>
        /// <param name="fillFactor">The fill factor for spilling over collections.</param>
        /// <param name="checkIntervalSeconds">The interval between collection size checks.</param>
        public SpilloverPartitionResolver(
            DocumentClient client,
            Database database,
            DocumentCollectionSpec collectionSpec = null,
            string collectionIdPrefix = "Collection.",
            double fillFactor = 0.90,
            double checkIntervalSeconds = 3600)
        {
            this.Client = client;
            this.Database = database;
            this.CollectionTemplate = collectionSpec;
            this.CollectionLinks = GetCollections(client, database, collectionIdPrefix, collectionSpec);
            this.CollectionIdPrefix = collectionIdPrefix;
            this.FillFactor = fillFactor;
            this.CheckInterval = TimeSpan.FromSeconds(checkIntervalSeconds);
        }

        /// <summary>
        /// Gets the DocumentDB client.
        /// </summary>
        public DocumentClient Client { get; private set; }

        /// <summary>
        /// Gets the Database to use.
        /// </summary>
        public Database Database { get; private set; }

        /// <summary>
        /// Gets or sets the list of collections in use.
        /// </summary>
        public List<string> CollectionLinks { get; set; }

        /// <summary>
        /// Gets the Collection Id prefix to use.
        /// </summary>
        public string CollectionIdPrefix { get; private set; }

        /// <summary>
        /// Gets the collection specification/template to use.
        /// </summary>
        public DocumentCollectionSpec CollectionTemplate { get; private set; }

        /// <summary>
        /// Gets the collection fill factor to spill over.
        /// </summary>
        public double FillFactor { get; private set; }

        /// <summary>
        /// Gets the time interval to check the usage of collections.
        /// </summary>
        public TimeSpan CheckInterval { get; private set; }

        /// <summary>
        /// Gets the last time the collection size was checked.
        /// </summary>
        public DateTime LastCheckTimeUtc { get; private set; }

        int NextCollectionNumber { get; set; }

        /// <summary>
        /// Returns the collections to read for a document. Here we return all collections.
        /// </summary>
        /// <param name="partitionKey">The partition key for the read.</param>
        /// <returns>The list of collections.</returns>
        public IEnumerable<string> ResolveForRead(object partitionKey)
        {
            this.CreateCollectionIfRequired();
            return this.CollectionLinks;
        }

        /// <summary>
        /// Returns the collection to create this document. Returns the last collection.
        /// </summary>
        /// <param name="partitionKey">The partition key for the create.</param>
        /// <returns>The collection to create in.</returns>
        public string ResolveForCreate(object partitionKey)
        {
            this.CreateCollectionIfRequired();
            return this.CollectionLinks.Last();
        }

        /// <summary>
        /// Returns the partition key for the document. Bypass by returning null.
        /// </summary>
        /// <param name="document">The document to locate.</param>
        /// <returns>The partition key.</returns>
        public object GetPartitionKey(object document)
        {
            return null;
        }

        /// <summary>
        /// Gets or creates the collections for the hash resolver.
        /// </summary>
        /// <param name="client">The DocumentDB client instance.</param>
        /// <param name="database">The database to use.</param>
        /// <param name="collectionIdPrefix">The prefix to use while creating collections.</param>
        /// <param name="spec">The specification/template to use to create collections.</param>
        /// <returns>The list of collection self links.</returns>
        private List<string> GetCollections(
            DocumentClient client,
            Database database,
            string collectionIdPrefix,
            DocumentCollectionSpec spec)
        {
            var collections = new Dictionary<int, string>();
            foreach (DocumentCollection collection in client.ReadDocumentCollectionFeedAsync(database.SelfLink).Result)
            {
                if (collection.Id.StartsWith(collectionIdPrefix))
                {
                    int collectionNumber = int.Parse(collection.Id.Replace(collectionIdPrefix, string.Empty));
                    collections[collectionNumber] = collection.SelfLink;
                }
            }
            if (collections.Any())
            {
                NextCollectionNumber = collections.Keys.Max() + 1;
            }
            else
            {
                NextCollectionNumber = 0;
            }
            // Return selflinks in ID order
            return collections.OrderBy(kvp => kvp.Key).Select(kvp => kvp.Value).ToList();
        }

        private void CreateCollectionIfRequired()
        {
            if (this.ShouldCreateCollection())
            {
                try
                {
                    string collectionId = string.Format("{0}{1}", this.CollectionIdPrefix, NextCollectionNumber);
                    var createdCollection = DocumentClientHelper.GetOrCreateCollectionAsync(this.Client, this.Database.Id, collectionId, this.CollectionTemplate).Result;
                    this.CollectionLinks.Add(createdCollection.SelfLink);
                }
                catch
                {
                    this.CollectionLinks = GetCollections(this.Client, this.Database, this.CollectionIdPrefix,
                        this.CollectionTemplate);
                }
            }
        }


        /// <summary>
        /// Check if a spillover has to be scheduled.
        /// </summary>
        /// <returns>Should a new collection be created.</returns>
        private bool ShouldCreateCollection()
        {
            if (this.CollectionLinks.Count == 0)
            {
                return true;
            }

            string lastCollectionLink = this.CollectionLinks.Last();
            if (this.LastCheckTimeUtc == null || DateTime.UtcNow >= this.LastCheckTimeUtc.Add(this.CheckInterval))
            {
                ResourceResponse<DocumentCollection> response = this.Client.ReadDocumentCollectionAsync(lastCollectionLink).Result;
                this.LastCheckTimeUtc = DateTime.UtcNow;
                if (response.CollectionSizeUsage >= response.CollectionSizeQuota * this.FillFactor)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
