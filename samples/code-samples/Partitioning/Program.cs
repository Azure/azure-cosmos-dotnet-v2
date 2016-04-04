namespace DocumentDB.Samples.Partitioning
{
    using DocumentDB.Samples.Partitioning.DataModels;
    using DocumentDB.Samples.Partitioning.Partitioners;
    using DocumentDB.Samples.Partitioning.Utils;
    using DocumentDB.Samples.Shared.Util;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Documents.Linq;
    using Microsoft.Azure.Documents.Partitioning;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Linq;
    using System.Threading.Tasks;

    /// <summary>
    /// This sample demonstrates basic CRUD operations on a Database resource for Azure DocumentDB.
    /// </summary>
    public class Program
    {
        /// <summary>
        /// The DocumentDB endpoint read from config. 
        /// These values are available from the Azure Management Portal on the DocumentDB Account Blade under "Keys".
        /// NB Keep these values in a safe and secure location. Together they provide Administrative access to your DocDB account.
        /// </summary>
        private static readonly string EndpointUrl = ConfigurationManager.AppSettings["EndPointUrl"];

        /// <summary>
        /// The DocumentDB authorization key.
        /// These values are available from the Azure Management Portal on the DocumentDB Account Blade under "Keys"
        /// </summary>
        private static readonly string AuthorizationKey = ConfigurationManager.AppSettings["AuthorizationKey"];

        /// <summary>
        /// Database Id used for these samples.
        /// </summary>
        private static readonly string DatabaseId = ConfigurationManager.AppSettings["DatabaseId"];

        /// <summary>
        /// The ConnectionPolicy for these samples. Sets custom user-agent.
        /// </summary>
        private static readonly ConnectionPolicy ConnectionPolicy = new ConnectionPolicy { UserAgentSuffix = " samples-net-partitioning/1" };

        /// <summary>
        /// The DocumentDB client instance.
        /// </summary>
        private DocumentClient client;

        /// <summary>
        /// Initializes a new instance of the <see cref="Program"/> class.
        /// </summary>
        /// <param name="client">The DocumentDB client instance.</param>
        private Program(DocumentClient client)
        {
            this.client = client;
        }

        /// <summary>
        /// Main method for the sample.
        /// </summary>
        /// <param name="args">command line arguments.</param>
        public static void Main(string[] args)
        {
            try
            {
                using (var client = new DocumentClient(new Uri(EndpointUrl), AuthorizationKey, ConnectionPolicy))
                {
                    var program = new Program(client);
                    program.RunAsync().Wait();
                    Console.WriteLine("Samples completed successfully.");
                }
            }
#if !DEBUG
            catch (Exception e)
            {
                // If the Exception is a DocumentClientException, the "StatusCode" value might help identity 
                // the source of the problem. 
                Console.WriteLine("Samples failed with exception:{0}", e);
            }
#endif
            finally
            {
                Console.WriteLine("End of samples, press any key to exit.");
                Console.ReadKey();
            }
        }

        /// <summary>
        /// Run samples for Database create, read, update and delete.
        /// </summary>
        /// <returns>a Task object.</returns>
        private async Task RunAsync()
        {
            // Let's see how a to use a HashPartitionResolver.
            Console.WriteLine("Running samples with the default hash partition resolver ...");
            Database database = await DocumentClientHelper.GetNewDatabaseAsync(this.client, DatabaseId);
            HashPartitionResolver hashResolver = await this.InitializeHashResolver(database);
            await this.RunCrudAndQuerySample(database, hashResolver);

            // Let's see how to use a RangePartitionResolver.
            Console.WriteLine("Running samples with the default range partition resolver ...");
            database = await DocumentClientHelper.GetNewDatabaseAsync(this.client, DatabaseId);
            RangePartitionResolver<string> rangeResolver = await this.InitializeRangeResolver(database);
            await this.RunCrudAndQuerySample(database, rangeResolver);

            // Now let's take a look at an example of how to derive from one of the supported partition resolvers.
            // Here we implement a generic hash resolver that takes an arbirary lambda to extract partition keys.
            database = await DocumentClientHelper.GetNewDatabaseAsync(this.client, DatabaseId);
            HashPartitionResolver customHashResolver = await this.InitializeCustomHashResolver(database);
            await this.RunCrudAndQuerySample(database, customHashResolver);

            // Let's take a look at a example of a LookupPartitionResolver, i.e., use a simple lookup table.
            Console.WriteLine("Running samples with a lookup partition resolver ...");
            database = await DocumentClientHelper.GetNewDatabaseAsync(this.client, DatabaseId);
            LookupPartitionResolver<string> lookupResolver = await this.InitializeLookupPartitionResolver(database);
            await this.RunCrudAndQuerySample(database, lookupResolver);

            // Now, a "managed" HashPartitionResolver that creates collections, while cloning their attributes like 
            // IndexingPolicy and OfferType.
            Console.WriteLine("Running samples with a custom hash partition resolver that automates creating collections ...");
            database = await DocumentClientHelper.GetNewDatabaseAsync(this.client, DatabaseId);
            ManagedHashPartitionResolver managedHashResolver = this.InitializeManagedHashResolver(database);
            await this.RunCrudAndQuerySample(database, managedHashResolver);

            // Now, a PartitionResolver that starts with one collection and spills over to new ones as the collections
            // get filled up.
            Console.WriteLine("Running samples with a custom \"spillover\" partition resolver");
            database = await DocumentClientHelper.GetNewDatabaseAsync(this.client, DatabaseId);
            SpilloverPartitionResolver spilloverResolver = new SpilloverPartitionResolver(this.client, database);
            this.client.PartitionResolvers[database.SelfLink] = spilloverResolver;
            await this.RunCrudAndQuerySample(database, spilloverResolver);

            // Now let's see how to persist the settings for a PartitionResolver, and how to bootstrap from those settings.
            this.RunSerializeDeserializeSample(hashResolver);

            // Now let's take a look at how to add and remove partitions to a HashPartitionResolver.
            database = await DocumentClientHelper.GetNewDatabaseAsync(this.client, DatabaseId);
            await this.RepartitionDataSample(database);
        }

        /// <summary>
        /// Initialize a HashPartitionResolver.
        /// </summary>
        /// <param name="database">The database to run the samples on.</param>
        /// <returns>The created HashPartitionResolver.</returns>
        private async Task<HashPartitionResolver> InitializeHashResolver(Database database)
        {
            // Create some collections to partition data.
            DocumentCollection collection1 = await DocumentClientHelper.GetOrCreateCollectionAsync(this.client, database.Id, "Collection.HashBucket0");
            DocumentCollection collection2 = await DocumentClientHelper.GetOrCreateCollectionAsync(this.client, database.Id, "Collection.HashBucket1");

            // Initialize a partition resolver that users hashing, and register with DocumentClient. 
            HashPartitionResolver hashResolver = new HashPartitionResolver("UserId", new[] { collection1.SelfLink, collection2.SelfLink });
            this.client.PartitionResolvers[database.SelfLink] = hashResolver;

            return hashResolver;
        }

        /// <summary>
        /// Initialize a RangePartitionResolver.
        /// </summary>
        /// <param name="database">The database to run the samples on.</param>
        /// <returns>The created RangePartitionResolver.</returns>
        private async Task<RangePartitionResolver<string>> InitializeRangeResolver(Database database)
        {
            // Create some collections to partition data.
            DocumentCollection collection1 = await DocumentClientHelper.GetOrCreateCollectionAsync(this.client, database.Id, "Collection.A-M");
            DocumentCollection collection2 = await DocumentClientHelper.GetOrCreateCollectionAsync(this.client, database.Id, "Collection.N-Z");

            // Initialize a partition resolver that assigns users (A-M) -> collection1, and (N-Z) -> collection2
            // and register with DocumentClient. 
            // Note: \uffff is the largest UTF8 value, so M\ufff includes all strings that start with M.
            RangePartitionResolver<string> rangeResolver = new RangePartitionResolver<string>(
                "UserId",
                new Dictionary<Range<string>, string>() 
                { 
                    { new Range<string>("A", "M\uffff"), collection1.SelfLink },
                    { new Range<string>("N", "Z\uffff"), collection2.SelfLink },
                });

            this.client.PartitionResolvers[database.SelfLink] = rangeResolver;
            return rangeResolver;
        }

        /// <summary>
        /// Initialize a HashPartitionResolver that uses a custom function to extract the partition key.
        /// </summary>
        /// <param name="database">The database to run the samples on.</param>
        /// <returns>The created HashPartitionResolver.</returns>
        private async Task<HashPartitionResolver> InitializeCustomHashResolver(Database database)
        {
            DocumentCollection collection1 = await DocumentClientHelper.GetOrCreateCollectionAsync(this.client, database.Id, "Collection.HashBucket0");
            DocumentCollection collection2 = await DocumentClientHelper.GetOrCreateCollectionAsync(this.client, database.Id, "Collection.HashBucket1");

            var hashResolver = new HashPartitionResolver(
                u => ((UserProfile)u).UserId,
                new[] { collection1.SelfLink, collection2.SelfLink });

            this.client.PartitionResolvers[database.SelfLink] = hashResolver;
            return hashResolver;
        }

        /// <summary>
        /// Initialize a LookupPartitionResolver.
        /// </summary>
        /// <param name="database">The database to run the samples on.</param>
        /// <returns>The created HashPartitionResolver.</returns>
        private async Task<LookupPartitionResolver<string>> InitializeLookupPartitionResolver(Database database)
        {
            DocumentCollection collectionUS = await DocumentClientHelper.GetOrCreateCollectionAsync(this.client, database.Id, "Collection.US");
            DocumentCollection collectionEU = await DocumentClientHelper.GetOrCreateCollectionAsync(this.client, database.Id, "Collection.Europe");
            DocumentCollection collectionOther = await DocumentClientHelper.GetOrCreateCollectionAsync(this.client, database.Id, "Collection.Other");

            // This implementation takes strings as input. If you'd like to implement a strongly typed LookupPartitionResolver, 
            // take a look at EnumLookupPartitionResolver for an example.
            var lookupResolver = new LookupPartitionResolver<string>(
                "PrimaryRegion", 
                new Dictionary<string, string>() 
                { 
                    { Region.UnitedStatesEast.ToString(), collectionUS.SelfLink },
                    { Region.UnitedStatesWest.ToString(), collectionUS.SelfLink },
                    { Region.Europe.ToString(), collectionEU.SelfLink },
                    { Region.AsiaPacific.ToString(), collectionOther.SelfLink },
                    { Region.Other.ToString(), collectionOther.SelfLink },
                });

            this.client.PartitionResolvers[database.SelfLink] = lookupResolver;
            return lookupResolver;
        }

        /// <summary>
        /// Initialize a "managed" HashPartitionResolver that also takes care of creating collections, and cloning collection properties like
        /// stored procedures, offer type and indexing policy.
        /// </summary>
        /// <param name="database">The database to run the samples on.</param>
        /// <returns>The created HashPartitionResolver.</returns>
        private ManagedHashPartitionResolver InitializeManagedHashResolver(Database database)
        {
            var hashResolver = new ManagedHashPartitionResolver(u => ((UserProfile)u).UserId, this.client, database, 3, null, new DocumentCollectionSpec { OfferType = "S2" });
            this.client.PartitionResolvers[database.SelfLink] = hashResolver;
            return hashResolver;
        }

        /// <summary>
        /// Run examples of how to perform create, read, update, delete and query documents against a partitioned database. 
        /// </summary>
        /// <param name="database">The database to run the samples on.</param>
        /// <param name="partitionResolver">The PartitionResolver instance to use.</param>
        /// <returns>The Task for asynchronous execution of these samples.</returns>
        private async Task RunCrudAndQuerySample(Database database, IPartitionResolver partitionResolver)
        {
            // Create some documents. Note that creates use the database's self link instead of a specific collection's self link. 
            // The hash resolver will compute the hash of UserId in order to route the create to either of the collections.
            Document johnDocument = await this.client.CreateDocumentAsync(database.SelfLink, new UserProfile("J1", "@John", Region.UnitedStatesEast));
            Document ryanDocument = await this.client.CreateDocumentAsync(database.SelfLink, new UserProfile("U4", "@Ryan", Region.AsiaPacific, UserStatus.AppearAway));

            // Delete document using self link (as usual).
            await this.client.DeleteDocumentAsync(ryanDocument.SelfLink);

            // Read, then update status using self link (as usual).
            Document latestJohnDocument = await this.client.ReadDocumentAsync(johnDocument.SelfLink);
            UserProfile johnProfile = (UserProfile)(dynamic)latestJohnDocument;
            johnProfile.Status = UserStatus.Busy;

            await this.client.ReplaceDocumentAsync(latestJohnDocument.SelfLink, johnProfile);

            // Query for John's document by ID. We can use the PartitionResolver to restrict the query to just the partition containing @John
            // Again the query uses the database self link, and relies on the hash resolver to route the appropriate collection.
            var query = this.client.CreateDocumentQuery<UserProfile>(database.SelfLink, null, partitionResolver.GetPartitionKey(johnProfile))
                .Where(u => u.UserName == "@John");
            johnProfile = query.AsEnumerable().FirstOrDefault();

            // Query for all Busy users. Here since there is no partition key, the query is serially executed across each partition/collection. 
            query = this.client.CreateDocumentQuery<UserProfile>(database.SelfLink).Where(u => u.Status == UserStatus.Busy);
            foreach (UserProfile busyUser in query)
            {
                Console.WriteLine(busyUser);
            }
             
            // Find the collections where a document exists in. It's uncommon to do this, but can be useful if for example to execute a 
            // stored procedure against a specific set of partitions.
            IPartitionResolver resolver = this.client.PartitionResolvers[database.SelfLink];
            List<string> collectionLinks = resolver.ResolveForRead(resolver.GetPartitionKey(johnProfile)).ToList();

            // Find the collections where a document will be created in.
            string collectionLink = resolver.ResolveForCreate(resolver.GetPartitionKey(johnProfile));

            // Cleanup.
            await this.client.DeleteDatabaseAsync(database.SelfLink);
        }

        /// <summary>
        /// Illustrate how to load and save the serializer state. Uses HashPartitionResolver as example.
        /// </summary>
        private void RunSerializeDeserializeSample(HashPartitionResolver hashResolver)
        {
            // Store the partition resolver's configuration as JSON.
            string resolverStateAsString = JsonConvert.SerializeObject(hashResolver);

            // Read properties and extract individual values to clone the resolver.
            // ISSUE: fix deserialization of IHashGenerator
            dynamic parsedResolver = JsonConvert.DeserializeObject(resolverStateAsString);
            string partitionKeyPropertyName = parsedResolver.PartitionKeyPropertyName;
            JArray collections = parsedResolver.CollectionLinks;
            List<string> collectionLinks = collections.Select(c => (string)c).ToList();

            HashPartitionResolver clonedHashResolver = new HashPartitionResolver(partitionKeyPropertyName, collectionLinks);
        }

        /// <summary>
        /// Show how to repartition a hash resolver by adding/removing collections.
        /// </summary>
        /// <param name="database">The database to run the samples on.</param>
        /// <returns>The Task for the asynchronous execution.</returns>
        private async Task RepartitionDataSample(Database database)
        {
            var manager = new DocumentClientHashPartitioningManager(u => ((UserProfile)(dynamic)u).UserId, this.client, database, 3);
            for (int i = 0; i < 1000; i++)
            {
                await this.client.CreateDocumentAsync(database.SelfLink, new UserProfile("J" + (i + 1), "@John", Region.UnitedStatesEast));
            }

            Console.WriteLine("Distribution of documents across collections:");
            await this.LogDocumentCountsPerCollection(database);
            Console.WriteLine();

            // Add another partition.
            Console.WriteLine("Adding one partition.");
            int numberOfDocumentsMoved = await manager.AddPartition();
            Console.WriteLine("Moved {0} documents between partitions.", numberOfDocumentsMoved);
            Console.WriteLine();

            Console.WriteLine("Distribution of documents across collections:");
            await this.LogDocumentCountsPerCollection(database);
            Console.WriteLine();

            // Add another partition.
            Console.WriteLine("Removing one partition.");
            numberOfDocumentsMoved = await manager.RemovePartition();
            Console.WriteLine("Moved {0} documents between partitions.", numberOfDocumentsMoved);
            await this.LogDocumentCountsPerCollection(database);
        }

        /// <summary>
        /// Log the number of documents in each collection within the database to the console.
        /// </summary>
        /// <param name="database">The database to enumerate.</param>
        /// <returns>The Task for the asynchronous execution.</returns>
        private async Task LogDocumentCountsPerCollection(Database database)
        {
            foreach (DocumentCollection collection in await this.client.ReadDocumentCollectionFeedAsync(database.SelfLink))
            {
                int numDocuments = 0;
                foreach (int document in this.client.CreateDocumentQuery<int>(collection.SelfLink, "SELECT VALUE 1 FROM ROOT"))
                {
                    numDocuments++;
                }

                Console.WriteLine("Collection {0}: {1} documents", collection.Id, numDocuments);
            }
        }
    }
}
