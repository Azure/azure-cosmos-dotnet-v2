## Changes in [1.22.0](https://www.nuget.org/packages/Microsoft.Azure.DocumentDB/1.21.1) : ##

* Added ConsistencyLevel Property to FeedOptions.
* Added JsonSerializerSettings to RequestOptions and FeedOptions.
* Added EnableReadRequestsFallback to ConnectionPolicy.

## Changes in [1.21.1](https://www.nuget.org/packages/Microsoft.Azure.DocumentDB/1.21.1) : ##

* Fixed KeyNotFoundException for cross partition order by queries in corner cases.
* Fixed bug where JsonPropery attribute in select clause for LINQ queries was not being honored.

## Changes in [1.20.2](https://www.nuget.org/packages/Microsoft.Azure.DocumentDB/1.20.2) : ##

* Fixed bug that is hit under certain race conditions, that results in intermittent "Microsoft.Azure.Documents.NotFoundException: The read session is not available for the input session token" errors when using Session consistency level.

## Changes in [1.20.1](https://www.nuget.org/packages/Microsoft.Azure.DocumentDB/1.20.1) : ##

* Improved cross partition query performance when the MaxDegreeOfParallelism property is set to -1 in FeedOptions.
* Added a new ToString() function to QueryMetrics.
* Exposed partition statistics on reading collections.
* Added PartitionKey property to ChangeFeedOptions.
* Minor bug fixes.

## Changes in [1.19.1](https://www.nuget.org/packages/Microsoft.Azure.DocumentDB/1.19.1) : ##

* Adds the ability to specify unique indexes for the documents by using UniqueKeyPolicy property on the DocumentCollection.
* Fixed a bug in which the custom JsonSerializer settings were not being honored for some queries and stored procedure execution.
 
## Changes in [1.19.0](https://www.nuget.org/packages/Microsoft.Azure.DocumentDB/1.19.0) : ##

* Branding change from Azure DocumentDB to Azure Cosmos DB in the API Reference documentation, metadata information in assemblies, and the NuGet package. 
* Expose diagnostic information and latency from the response of requests sent with direct connectivity mode. The property names are RequestDiagnosticsString and RequestLatency on ResourceResponse class.
* This SDK version requires the latest version of Azure Cosmos DB Emulator available for download from https://aka.ms/cosmosdb-emulator.

## Changes in [1.18.1](https://www.nuget.org/packages/Microsoft.Azure.DocumentDB/1.18.1) : ##

* Internal changes for Microsoft friends assemblies.

## Changes in [1.18.0](https://www.nuget.org/packages/Microsoft.Azure.DocumentDB/1.19.0) : ##

* Added several reliability fixes and improvements.

## Changes in [1.17.0](https://www.nuget.org/packages/Microsoft.Azure.DocumentDB/1.17.0) : ##

* Added support for PartitionKeyRangeId as a FeedOption for scoping query results to a specific partition key range value. 
* Added support for StartTime as a ChangeFeedOption to start looking for the changes after that time. 

## Changes in [1.16.1](https://www.nuget.org/packages/Microsoft.Azure.DocumentDB/1.16.1) : ##
- Fixed an issue in the JsonSerializable class that may cause a stack overflow exception. 

## Changes in [1.16.0](https://www.nuget.org/packages/Microsoft.Azure.DocumentDB/1.16.0) : ##
- Fixed an issue that required recompiling of the application due to the introduction of JsonSerializerSettings as an optional parameter in the DocumentClient constructor.
- Marked the DocumentClient constructor obsolete that required JsonSerializerSettings as the last parameter to allow for default values of ConnectionPolicy and ConsistencyLevel parameters when passing in JsonSerializerSettings parameter.

## Changes in [1.15.0](https://www.nuget.org/packages/Microsoft.Azure.DocumentDB/1.15.0) : ##
- Added support for specifying custom JsonSerializerSettings while instantiating DocumentClient.

## Changes in 1.14.1 : ##
- Fixed an issue that affected x64 machines that don’t support SSE4 instruction and throw an SEHException when running Azure Cosmos DB DocumentDB API queries.

## Changes in 1.14.0 : ##
- Added support for Request Unit per Minute (RU/m) feature.
- Added support for a new consistency level called ConsistentPrefix.
- Added support for query metrics for individual partitions.
- Added support for limiting the size of the continuation token for queries.
- Added support for more detailed tracing for failed requests.
- Made some performance improvements in the SDK.

## Changes in 1.13.4 : ##
- Functionally same as 1.13.3. Made some internal changes.

## Changes in 1.13.3 : ##
- Functionally same as 1.13.2. Made some internal changes.

## Changes in 1.13.2 : ##
- Fixed an issue that ignored the PartitionKey value provided in FeedOptions for aggregate queries.
- Fixed an issue in transparent handling of partition management during mid-flight cross-partition Order By query execution.

## Changes in 1.13.1 : ##
- Fixed an issue which caused deadlocks in some of the async APIs when used inside ASP.NET context.

## Changes in 1.13.0 : ##
- Fixes to make SDK more resilient to automatic failover under certain conditions.

## Changes in 1.12.2 : ##
- Fix for an issue that occasionally causes a WebException: The remote name could not be resolved.
- Added the support for directly reading a typed document by adding new overloads to ReadDocumentAsync API.

## Changes in 1.12.1 : ##
- Added LINQ support for aggregation queries (COUNT, MIN, MAX, SUM, and AVG).
- Fix for a memory leak issue for the ConnectionPolicy object caused by the use of event handler.
- Fix for an issue wherein UpsertAttachmentAsync was not working when ETag was used.
- Fix for an issue wherein cross partition order-by query continuation was not working when sorting on string field.

## Changes in 1.12.0 : ##
- Added support for aggregation queries (COUNT, MIN, MAX, SUM, and AVG). See [Aggregation support](https://docs.microsoft.com/en-us/azure/documentdb/documentdb-sql-query#Aggregates).
- Lowered minimum throughput on partitioned collections from 10,100 RU/s to 2500 RU/s.

## Changes in 1.11.4 : ##

- Fix for an issue wherein some of the cross-partition queries were failing in the 32-bit host process.
- Fix for an issue wherein the session container was not being updated with the token for failed requests in Gateway mode.
- Fix for an issue wherein a query with UDF calls in projection was failing in some cases.

## Changes in 1.11.3 : ##

- Fix for an issue wherein the session container was not being updated with the token for failed requests. 
- Added support for the SDK to work in a 32-bit host process. Note that if you use cross partition queries, 64-bit host processing is recommended for improved performance.
- Improved performance for scenarios involving queries with a large number of partition key values in an IN expression.

## Changes in 1.11.1 : ##

- Minor performance fix for the CreateDocumentCollectionIfNotExistsAsync API introduced in 1.11.0. 
- Peformance fix in the SDK for scenarios that involve high degree of concurrent requests.

## Changes in 1.11.0 : ##

- Support for new classes and methods to process the [change feed](https://docs.microsoft.com/en-us/azure/documentdb/documentdb-change-feed) of documents within a collection. 
- Support for cross-partition query continuation and some perf improvements for cross-partition queries.
- Addition of CreateDatabaseIfNotExistsAsync and CreateDocumentCollectionIfNotExistsAsync methods.
- LINQ support for system functions: IsDefined, IsNull and IsPrimitive.
- Fix for automatic binplacing of Microsoft.Azure.Documents.ServiceInterop.dll and DocumentDB.Spatial.Sql.dll assemblies to application’s bin folder when using the Nuget package with projects that have project.json tooling.
- Support for emitting client side ETW traces which could be helpful in debugging scenarios.

## Changes in 1.10.0 : ##

- Added direct connectivity support for partitioned collections.
- Improved performance for the Bounded Staleness consistency level.
- Added LINQ support for StringEnumConverter, IsoDateTimeConverter and UnixDateTimeConverter while translating predicates.
- Various SDK bug fixes.

## Changes in 1.9.5 : ##

- Fixed an issue that caused the following NotFoundException: The read session is not available for the input session token. This exception occurred in some cases when querying for the read-region of a geo-distributed account.
- Exposed the ResponseStream property in the ResourceResponse class, which enables direct access to the underlying stream from a response. 

## Changes in 1.9.4 : ##

- Modified the ResourceResponse, FeedResponse, StoredProcedureResponse and MediaResponse classes to implement the corresponding public interface so that they can be mocked for test driven deployment (TDD).
- Fixed an issue that caused a malformed partition key header when using a custom JsonSerializerSettings object for serializing data.

## Changes in 1.9.3 : ##

- Fixed an issue that caused long running queries to fail with error: Authorization token is not valid at the current time.
- Fixed an issue that removed the original SqlParameterCollection from cross partition top/order-by queries.

## Changes in 1.9.2 : ##

- Added support for parallel queries for partitioned collections.
- Added support for cross partition ORDER BY and TOP queries for partitioned collections.
- Fixed the missing references to DocumentDB.Spatial.Sql.dll and Microsoft.Azure.Documents.ServiceInterop.dll that are required when referencing a DocumentDB project with a reference to the DocumentDB Nuget package.
- Fixed the ability to use parameters of different types when using user defined functions in LINQ. 
- Fixed a bug for globally replicated accounts where Upsert calls were being directed to read locations instead of write locations.
- Added methods to the IDocumentClient interface that were missing: 
	- UpsertAttachmentAsync method that takes mediaStream and options as parameters.
    - CreateAttachmentAsync method that takes options as a parameter.
    - CreateOfferQuery method that takes querySpec as a parameter.

- Unsealed public classes that are exposed in the IDocumentClient interface.


## Changes in 1.8.0 : ##

- Added the support for multi-region database accounts.
- Added support for retry on throttled requests.User can customize the number of retries and the max wait time by configuring the ConnectionPolicy.RetryOptions property.
- Added a new IDocumentClient interface that defines the signatures of all DocumenClient properties and methods. As part of this change, also changed extension methods that create IQueryable and IOrderedQueryable to methods on the DocumentClient class itself.
- Added configuration option to set the ServicePoint.ConnectionLimit for a given DocumentDB endpoint Uri. Use ConnectionPolicy.MaxConnectionLimit to change the default value, which is set to 50.
- Deprecated IPartitionResolver and its implementation. Support for IPartitionResolver is now obsolete. It's recommended that you use Partitioned Collections for higher storage and throughput.


## Changes in 1.7.1 : ##

- Added an overload to Uri based ExecuteStoredProcedureAsync method that takes RequestOptions as a parameter.

## Changes in 1.7.0 : ##

- Added time to live (TTL) support for documents.

## Changes in 1.6.3 : ##

- Fixed a bug in Nuget packaging of .NET SDK for packaging it as part of a Azure Cloud Service solution.

## Changes in 1.6.2 : ##

- Implemented [partitioned collections](https://github.com/Azure/azure-content-pr/blob/master/articles/documentdb/documentdb-partition-data.md) and [user-defined performance levels](https://github.com/Azure/azure-content-pr/blob/master/articles/documentdb/documentdb-performance-levels.md). 

## Changes in 1.5.3 : ##

- **[Fixed]** Querying DocumentDB endpoint throws: 'System.Net.Http.HttpRequestException: Error while copying content to a stream.

## Changes in 1.5.2 : ##

- Expanded LINQ support including new operators for paging, conditional expressions and range comparison. 
	- Take operator to enable SELECT TOP behavior in LINQ.
	- CompareTo operator to enable string range comparisons.
	- Conditional (?) and coalesce operators (??).

- **[Fixed]** ArgumentOutOfRangeException when combining Model projection with Where-In in linq query. [#81](https://github.com/Azure/azure-documentdb-dotnet/issues/81)

## Changes in 1.5.1 : ##

- **[Fixed]** If Select is not the last expression the LINQ Provider assumed no projection and produced SELECT * incorrectly. [#58](https://github.com/Azure/azure-documentdb-dotnet/issues/58)

## Changes in 1.5.0 : ##

- Implemented Upsert, Added UpsertXXXAsync methods.
- Performance improvements for all requests.
- LINQ Provider support for conditional, coalesce and CompareTo methods for strings.
- **[Fixed]** LINQ provider --> Implement Contains method on List to generate the same SQL as on IEnumerable and Array.
- **[Fixed]** BackoffRetryUtility uses the same HttpRequestMessage again instead of creating a new one on retry.
- **[Obsolete]** UriFactory.CreateCollection --> should now use UriFactory.CreateDocumentCollection.

## Changes in 1.4.1 : ##

- **[Fixed]** Localization issues when using non en culture info such as nl-NL etc. 

## Changes in 1.4.0 : ##
- ID Based Routing 
	- New UriFactory helper to assist with constructing ID based resource links.
	- New overloads on DocumentClient to take in URI.

- Added IsValid() and IsValidDetailed() in LINQ for geospatial.
- LINQ Provider support enhanced.
	- **Math** - Abs, Acos, Asin, Atan, Ceiling, Cos, Exp, Floor, Log, Log10, Pow, Round, Sign, Sin, Sqrt, Tan, Truncate.
	- **String** - Concat, Contains, EndsWith, IndexOf, Count, ToLower, TrimStart, Replace, Reverse, TrimEnd, StartsWith, SubString, ToUpper.
	- **Array** - Concat, Contains, Count.
	- **IN** operator.


## Changes in 1.3.0 : ##
- Added support for modifying indexing policies.
	- New ReplaceDocumentCollectionAsync method in DocumentClient.
	- New IndexTransformationProgress property in ResourceResponse for tracking percent progress of index policy changes.
	- DocumentCollection.IndexingPolicy is now mutable.

- Added support for spatial indexing and query 
	- New Microsoft.Azure.Documents.Spatial namespace for serializing/deserializing spatial types like Point and Polygon.
	- New SpatialIndex class for indexing GeoJSON data stored in DocumentDB.

- **[Fixed]** : Incorrect SQL query generated from linq expression. [#38](https://github.com/Azure/azure-documentdb-dotnet/issues/38)

## Changes in 1.2.0 : ##
- Dependency on Newtonsoft.Json v5.0.7.

- Changes to support Order By:
	- LINQ provider support for OrderBy() or OrderByDescending().
	- IndexingPolicy to support Order By.

	**NB: Possible breaking change** 

	If you have existing code that provisions collections with a custom indexing policy, then your existing code will need to be updated to support the new IndexingPolicy class. If you have no custom indexing policy, then this change does not affect you.

## Changes in 1.1.0 : ##
- Support for partitioning data by using the new HashPartitionResolver and RangePartitionResolver classes and the IPartitionResolver.
- DataContract serialization.
- Guid support in LINQ provider.
- UDF support in LINQ.

## Changes in 1.0.0 : ##
- GA SDK.
