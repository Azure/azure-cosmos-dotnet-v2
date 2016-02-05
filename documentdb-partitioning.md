#DocumentDB Partitioning Support Private Preview
Welcome to the private preview of server-side partitioning support in Azure DocumentDB! Accounts created on the private version of the DocumentDB service can be used to create collections with much higher upper limits for storage and throughput. 

Azure DocumentDB is a fully-managed NoSQL document database service that provides automatic indexing, predictable performance and rich SQL querying over schema-less JSON documents. It handles many of the administrative tasks associated with running a production database like configuring replication and high availability and rolling out new software upgrades. DocumentDB provides SSD backed containers for storage and query of schema-less JSON data called collections. DocumentDB collections are logical as well as physical partitions. They have reserved storage of 10 GB and can be configured at one of [three performance levels](https://azure.microsoft.com/en-us/documentation/articles/documentdb-performance-levels/) S1, S2 and S3 with reserved throughput of 250, 1000 and 2500 request units per second. 

## What's changed
The private preview of DocumentDB service has built-in server side support for partitioning. A collection is no longer a single physical partition and it can span multiple partitions. They support order of millisecond reads and writes of JSON documents, automatic indexing, SQL querying, and ACID transactions just like they do today. DocumentDB additionally handles the tasks of partitioning data among multiple servers and routing requests to the right partitions. 

When you create a collection using the DocumentDB preview API or SDKs, you can specify the provisioned throughput in terms of request units per second. Based on this input value, the DocumentDB service will provision a number of partitions for your collection, each with the equivalent performance and reserved storage of a DocumentDB S3 collection. For example, if you create a collection with 10,000 request units per second, DocumentDB will create 4 partitions of 2500 RU/s each and total storage capacity of 40 GB.  

At collection creation time, you must configure a **partition key** property name for your collection. This value of this property within each JSON document or request will be used by DocumentDB to distribute documents among the available partitions. The preview SDKs will also support partition key as an optional parameter within document access methods like read, delete and query. Documents with the same partition key are stored in same partition so that queries get the benefit of data locality, and ACID transactions can be served against documents with the same partition key. 

The private preview accounts are not billed. They are provisioned against a dedicated cluster in the Azure West US region with limited capacity. Accounts are setup with capacity limits of 500 GB of storage and 125K RU/s throughput. If you need higher throughput, please request the Azure DocumentDB team and we'll do our best to accomodate your request. These clusters will be decommissioned after two weeks of the public availability of this preview. We will notify you in advance when this is scheduled. Please let us know if you need help with migration of your date.

##Get Started
In order to start using the preview, you must have the following: 
* An account endpoint provided by the Azure DocumentDB engineering team with the preview feature enabled, along with authorization keys
* A binary drop of the private preview build of the .NET SDK as a local Nuget package. Please see [this thread](http://stackoverflow.com/questions/10240029/how-to-install-a-nuget-package-nupkg-file-locally) for how to use this within Visual Studio.
* Code sample project of the changes involved.

Since this is a preview feature, these accounts must be used only for development and testing, not in production. The Azure DocumentDB team requests your feedback to help improve this functionality.

##What's changed in the DocumentDB REST APIs/SDKs
The private preview is supported using a new API version 2015-12-16. The main change in this API is the introduction of partition keys during collection creation, as well as an argument for CRUD and query operations. 

The choice of the partition key is an important decision that you make at design time. Queries that use the partition key within a filter will be routed to just the partitions corresponding to those keys and will scale efficiently. However, queries that do not filter against the partition key will be fanned out to all partitions. However, you can write queries that are unaware of the physical partitioning scheme. DocumentDB will parse the SQL query text, determine which partitions to route to, and consolidate results for any given query. 

##Code Samples
Here's how you can create a collection with the preview SDK. Please create a new project and add the private private Nuget package as a dependency. Please use Framework versions .NET 4.5.1 and above.

**Create a collection with a Partition Key and 10000 RU/s Throughput**


    DocumentClient client = new DocumentClient(new Uri(endpoint), authKey);
    await client.CreateDatabaseAsync(new Database { Id = "db" });
    
    // Collection for device telemetry. Here the JSON property deviceId will be used as the partition key to 
    // spread across partitions. Configured for 10K RU/s throughput and an indexing policy that supports 
    // sorting against any number or string property.
    DocumentCollection myCollection = new DocumentCollection();
    myCollection.Id = "coll";
    myCollection.PartitionKey.Paths.Add("/deviceId");
    myCollection.IndexingPolicy = new IndexingPolicy(new RangeIndex(DataType.String) { Precision = -1 }); //optional
  
    await client.CreateDocumentCollectionAsync(
        UriFactory.CreateDatabaseUri("db"),
        myCollection,
        new RequestOptions { OfferThroughput = 10000 });

**Create, Read, Update and Delete Documents**

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
    
    // Create a document. Here the partition key is extracted as "XMS-0001" based on the collection definition
    await client.CreateDocumentAsync(
        UriFactory.CreateDocumentCollectionUri("db", "coll"),
        new DeviceReading
        {
            Id = "XMS-001-FE24C",
            DeviceId = "XMS-0001",
            MetricType = "Temperature",
            MetricValue = 105.00,
            Unit = "Fahrenheit",
            ReadingTime = DateTime.UtcNow
        });

    // Read document. Needs the partition key and the ID to be specified
    Document result = await client.ReadDocumentAsync(
      UriFactory.CreateDocumentUri("db", "coll", "XMS-001-FE24C"), 
      new RequestOptions { PartitionKey = new object[] { "XMS-0001" }});
      
    DeviceReading reading = (DeviceReading)(dynamic)result;
  
    // Update the document. Partition key is not required, again extracted from the document
    reading.MetricValue = 104;
    reading.ReadingTime = DateTime.UtcNow;
    
    await client.ReplaceDocumentAsync(
      UriFactory.CreateDocumentUri("db", "coll", "XMS-001-FE24C"), 
      reading);
  
    // Delete document. Needs partition key
    await client.DeleteDocumentAsync(
      UriFactory.CreateDocumentUri("db", "coll", "XMS-001-FE24C"), 
      new RequestOptions { PartitionKey = new object[] { "XMS-001" } });
  

**Query**

    // Query using partition key
    client.CreateDocumentQuery<DeviceReading>(UriFactory.CreateDocumentCollectionUri("db", "coll"))
        .Where(m => m.MetricType == "Temperature" && m.DeviceId == "XMS-0001");
  
    // Query across partition keys
    client.CreateDocumentQuery<DeviceReading>(UriFactory.CreateDocumentCollectionUri("db", "coll"), 
      new FeedOptions { EnableCrossPartitionQuery = true })
        .Where(m => m.MetricType == "Temperature" && m.MetricValue > 100);


##What's coming next
* We will add support for automatic splits and repartitioning of data when the storage size grows or you provision additional throughpu. Collections can start small and grow to any size or throughput. Collections will also be elastic in terms of throughput. You can specify the throughput of a collection in increments of 100 RU/s and scale it up or down based on your application's needs. 
* We're also rolling out pricing changes that offer more flexibility than the current model. Storage will now be metered based on consumed storage, i.e. you pay based on the number of gigabytes used. Throughput will be metered separately based on the provisioned RUs/second in 100 RUs increments on an hourly basis.





