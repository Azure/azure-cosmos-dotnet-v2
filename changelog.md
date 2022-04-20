## <a name="recommended-version"></a> Recommended version (Deprecated)

The **minimum recommended version is [2.16.2](#2.16.2)**.

**Note:**
Because version 3 of the Azure Cosmos DB .NET SDK includes updated features and improved performance, we’ll retire version 2.x of this SDK on 31 August 2024.  As a result, you’ll need to update your SDK to version 3 by that date. We recommend following the [instructions](https://docs.microsoft.com/azure/cosmos-db/sql/migrate-dotnet-v3?tabs=dotnet-v3) to migrate to Azure Cosmos DB .NET SDK version 3.

## Release notes
### <a name="2.18.0"></a> 2.18.0
* Fixed regression introduced in 2.17.0 causing unobserved exception ("System.ObjectDisposedException: The semaphore has been disposed.")
* Removed noisy session token parse trace message
* Improved availabilty by using nonblocking cache for partition key ranges
* Fixed DocumentClient initialization to keep retrying. Previously it would keep returning same cached exception. 

### <a name="2.17.0"></a> 2.17.0 - Unlisted from a regression on broken connection causing unobserved exception. "System.ObjectDisposedException: The semaphore has been disposed."

> :warning: 2.17.0 removes the DefaultTraceListener from the SDK TraceSource for [performance reasons](https://docs.microsoft.com/azure/cosmos-db/sql/performance-tips?tabs=trace-net-core#logging-and-tracing) by default when not running in Debug mode.


* Fixed request diagnostics to reset thread starvation flag once starvation is detected
* Fixed request diagnostics to correctly include split responses
* Fixed session token when the target partition was not in the local cache, the Global Session Token would be sent and could cause BadRequest with large header errors
* Fixed issue where if cancellation token was equal to or less than request timeout the address cache would not get updated
* Fixed a bug where SDK would use preferred regions even if EndpointDiscovery is disabled
* Improved TCP performance by reducing allocated buffer size for requests
* Improved Service Unavailable errors to include substatus codes for different known scenarios for easier troubleshooting
* Improved request diagnostics to include service endpoint information
* Improved client availability by enabling the account information refresh upon client creation, which helps detect regional changes without a required failure happening
* Improved availability by avoiding retries on replicas that previously failed for that request
* Improved availability from avoiding replica during cache refreshes
* Added substatus code to all 503(Service Unavailable) exceptions

### <a name="2.16.2"></a> 2.16.2
* Fixed memory leak in query for systems running on Windows x64 using the ServiceInterop.dll

### <a name="2.16.1"></a> 2.16.1
* Improved availability for Direct + TCP mode by setting EnableTcpConnectionEndpointRediscovery to `true` by default.
* Improved RetryWith(HTTP 449) retry mechanics by having it retry sooner when multiple 449s are hit for the same request.
* Fixed queries going to gateway when SDK is configured with Direct mode. Only impacts queries without aggregates or no MaxDegreeOfParallelism is set with the ServiceInterop.dll not being available. This can fix header too large issues for the specified scenario.
* Fixed InvalidOperationException thrown with stack trace containing StoreClient.UpdateResponseHeader
* Fixed Memory and CPU usage calculation for Linux and Windows environment in RequestDiagnosticsString

### <a name="2.16.0"></a> 2.16.0
* Added memory, thread starvation, and response body size to RequestDiagnosticsString
* Fixed CVE-2017-0247 issue by bumping WinHttpHandler to 4.5.4
* Fixed ServiceInterop.dll to be BinSkim compliant by adding [/guard](https://docs.microsoft.com/cpp/build/reference/guard-enable-control-flow-guard?view=msvc-160&preserve-view=true) [/Qspectre](https://docs.microsoft.com/cpp/build/reference/guard-enable-control-flow-guard?view=msvc-160&preserve-view=true) flags
* Fixes failover mechanic that could prevent the detection of a region going offline during GetDatabaseAccount operations

### <a name="2.15.0"></a> 2.15.0

* Added Direct + TCP transport pipeline diagnostics to RequestDiagnosticsString
* Added optimization to reduce header size by only sending session token for the specific partition
* Added cold start optimization to go to `ConnectionPolicy.PreferredLocations` in parallel instead of waiting for each region to timeout/fail in serial if primary region is down.
* Added ConnectionPolicy.QueryPlanGenerationMode that can skip or require the Windows Native x64(ServiceInterop.dll) query plan generation
* Added DocumentClientOperationCanceledException additional context to Message instead of ToString()
* Fixed query plan generation if an unexpected exception happens when loading the Windows Native x64(ServiceInterop.dll). Now correctly falls back to gateway.
* Fixed availability issue that could block failover scenarios when CancellationToken was cancelling during the failover attempt.

### <a name="2.14.1"></a> 2.14.1

* Added PopulateIndexMetrics to FeedOptions which allows users to get index usage metrics during testing to improve query performance.

### <a name="2.14.0"></a> 2.14.0

* Added backend request duration in milliseconds (BELatencyMs) to RequestDiagnosticsString
* Improved allocations on Direct/TCP
* Improved latency for Gateway mode users on .NET Framework by disabling Nagle algorithm
* Fixed race condition in direct + tcp mode causing SDK generated internal server errors and invalid operation exceptions 
* Fixed race condition in direct + tcp mode causing unncessary connections to be created by concurrent requests 

### <a name="2.13.1"></a> 2.13.1

* Fixed an issue where Continuation header was sent even when it was absent.

### <a name="2.13.0"></a> 2.13.0

* Improved Session token parsing performance
* Improved Direct + TCP response header performance
* Improved performance by caching some environment variables
* Fixed nuspec to include System.ValueTuple dependency 
* Improved session token mismatch retry policy by extending maximum timeout from 50ms to 500ms. This will reduce failures returned to applications.
* Fixed a regression introduced in 2.10.0 that causes "Microsoft.Azure.Documents.NotFoundException: The read session is not available for the input session token" exceptions to be returned to users.

### <a name="2.12.0"></a> 2.12.0

* Improved detection of regional outages for query operations that require obtaining a query plan.
* Improved CPU utilization during connectivity events when using ConnectionPolicy.EnableTcpConnectionEndpointRediscovery.
* Added RegexMatch system function for queries.
* Improved performance for Direct + TCP connection by adding pooling to frequently allocated objects

### <a name="2.11.6"></a> 2.11.6

* Fixes "request headers is too long" for CRUD operations on stored procedures, triggers, and user defined functions

### <a name="2.11.5"></a> 2.11.5

* Fixed a bug that caused the following ConnectionPolicy options to be ignored. `IdleTcpConnectionTimeout`, `OpenTcpConnectionTimeout`, `MaxRequestsPerTcpConnection`, `MaxTcpPartitionCount`, `MaxTcpConnectionsPerEndpoint`, `MaxTcpConnectionsPerEndpoint`, `PortReuseMode`, `EnableTcpConnectionEndpointRediscovery`.
* Improved detection of regional failover scenarios during query operations.
* Improved resiliency during transient ReadSessionNotAvailable scenarios.
* Added a internal DocumentClientOperationCanceledException that wraps OperationCanceledException to include diagnostic information in the ToString() to help root cause issues. 

### <a name="2.11.4"></a> 2.11.4

* Fix PortReuseMode setting in connection policy to be honored by DocumentClient.

### <a name="2.11.3"></a> 2.11.3

* Added a cross-region retry mechanism for requests with transient connectivity issues to a particular region. This retry mechanism uses (and requires) the user's defined `ConnectionPolicy.PreferredLocations` preference list.
* Added diagnostics information to operation canceled exceptions.

### <a name="2.11.2"></a> 2.11.2

* Fix user session token in query FeedOption for request level session consistency

### <a name="2.11.1"></a> 2.11.1

* Fix CPU issues related to usage of ConnectionPolicy.EnableTcpConnectionEndpointRediscovery
* Fix PartitionKey not being correctly passed on GROUP BY queries
* Fix possible StackOverflowException in retry scenario by adding Task.Yeild. Related [fix for .NET Core 3](https://github.com/dotnet/coreclr/pull/23152)

### <a name="2.11.0"></a> 2.11.0

* Add RequestDiagnosticsString property to the StoredProcedureResponse
* Fix to improve the accurancy of heuristics applied to ConnectionPolicy.SetCurrentLocation
* Connectivity errors on .NET core using Direct + TCP mode now includes CPU usage history.

### <a name="2.10.3"></a> 2.10.3

* Fix socket exception thrown from TCP connection closure under edge cases for closed connection

### <a name="2.10.1"></a> 2.10.1

* Fix null reference exceptions when doing a query. This is fixes a bug in the fallback logic when the Microsoft.Azure.Documents.ServiceInterop.dll is not found.

### <a name="2.10.0"></a> 2.10.0

* Add support for creating geometry collections
* Add support to specify bounding box for geometry spatial index
* Add EnableTcpConnectionEndpointRediscovery to ConnectionPolicy which enables invalidation of addresses based upon connection close notifications
* Fixed aggregate query RuntimeBinderException 'Cannot convert type'
* Fixed permission serialization to include the Token
* Improve retry logic for transient region failures where the SDK cannot connect to a specific endpoint and gets HttpException.
* Improve latency by reducing default RequestTimeout from 60 seconds to 10 seconds

### <a name="2.9.4"></a> 2.9.4

* Fixed partition key not being honored for non windows x64 clients

### <a name="2.9.3"></a> 2.9.3

* Fixed timer pool leak in Direct TCP mode
* Fixed broken links in documentation
* Too large of header now traces the header sizes
* Reduced header size by excluding session token in get query plan calls

### <a name="2.9.2"></a> 2.9.2

* Fixed non ascii character in order by continuation token

### <a name="2.9.1"></a> 2.9.1

* Fix  Microsoft.Azure.Documents.ServiceInterop.dll graceful fallback bug [Issue #750](https://github.com/Azure/azure-cosmos-dotnet-v2/issues/750)

### <a name="2.9.0"></a> 2.9.0

* Add support for [GROUP BY](/azure/cosmos-db/sql-query-aggregate-functions) queries
* Query now retrieves query plan before execution in order to ensure consistent behavior between single partition and cross partition queries.

### <a name="2.8.1"></a> 2.8.1

* Added RequestDiagnosticsString to FeedResponse
* Fixed serialization settings for upsert and replace document

### <a name="2.7.0"></a> 2.7.0

* Added support for arrays and objects in order by queries
* Handle effective partition key collisions
* Added LINQ support for multiple OrderBy operators with ThenBy operator
* Fixed AysncCache deadlock issue so that it will work with a single-threaded task scheduler

### <a name="2.6.0"></a> 2.6.0

* Added PortReusePolicy to ConnectionPolicy
* Fixed ntdll!RtlGetVersion TypeLoadException issue when SDK is used in a UWP app

### <a name="2.5.1"></a> 2.5.1

* SDK’s System.Net.Http version now matches what is defined in the NuGet package
* Allow write requests to fallback to a different region if the original one fails
* Add session retry policy for write request

### <a name="2.4.4"></a> 2.4.2

* Made assembly version and file version same as nuget package version.

### <a name="2.4.1"></a> 2.4.1

* Fixes tracing race condition for queries which caused empty pages

### <a name="2.4.0"></a> 2.4.0

* Increased decimal precision size for LINQ queries.
* Added new classes CompositePath, CompositePathSortOrder, SpatialSpec, SpatialType and PartitionKeyDefinitionVersion
* Added TimeToLivePropertyPath to DocumentCollection
* Added CompositeIndexes and SpatialIndexes to IndexPolicy
* Added Version to PartitionKeyDefinition
* Added None to PartitionKey
* Fix a bug to properly handle non-JSON payload that would cause JsonReaderException

### <a name="2.3.0"></a> 2.3.0

* Added IdleTcpConnectionTimeout, OpenTcpConnectionTimeout, MaxRequestsPerTcpConnection and MaxTcpConnectionsPerEndpoint to ConnectionPolicy.

### <a name="2.2.3"></a> 2.2.3

* Diagnostics improvements

### <a name="2.2.2"></a> 2.2.2

* Added environment variable setting “POCOSerializationOnly”.
* Removed DocumentDB.Spatial.Sql.dll and now included in Microsoft.Azure.Documents.ServiceInterop.dll

### <a name="2.2.1"></a> 2.2.1

* Improvement in retry logic during failover for StoredProcedure execute calls.

* Made DocumentClientEventSource singleton. 

* Fix GatewayAddressCache timeout not honoring ConnectionPolicy RequestTimeout.

### <a name="2.2.0"></a> 2.2.0

* For direct/TCP transport diagnostics, added TransportException, an internal exception type of the SDK. When present in exception messages, this type prints additional information for troubleshooting client connectivity problems.

* Added new constuctor overload which takes a HttpMessageHandler, a HTTP handler stack to use for sending HttpClient requests (e.g., HttpClientHandler).

* Fix bug where header with null values were not being handled properly.

* Improved collection cache validation.

### <a name="2.1.3"></a> 2.1.3

* Updated System.Net.Security to 4.3.2.

### <a name="2.1.2"></a> 2.1.2

* Diagnostic tracing improvements.

### <a name="2.1.1"></a> 2.1.1

* Added more resilience to Multi-region request transient failures.   

### <a name="2.1.0"></a> 2.1.0

* Added Multi-region write support.
* Cross partition query performance improvements with TOP.
* Fixed bug where MaxBufferedItemCount was not being honored causing out of memory issues.

### <a name="2.0.0"></a> 2.0.0

* Added request cancellation support.
* Added SetCurrentLocation to ConnectionPolicy, which automatically populates the preferred locations based on the region.
* Fixed Bug in Cross Partition Queries with Min/Max and a filter that matches no documents on an individual partition.
* DocumentClient methods now have parity with IDocumentClient.
* Updated direct TCP transport stack to reduce the number of connections established.
* Added support for Direct Mode TCP for non-Windows clients.

### <a name="2.0.0-preview2"></a> 2.0.0-preview2

* Added request cancellation support.
* Added SetCurrentLocation to ConnectionPolicy, which automatically populates the preferred locations based on the region.
* Fixed Bug in Cross Partition Queries with Min/Max and a filter that matches no documents on an individual partition.

### <a name="2.0.0-preview"></a> 2.0.0-preview

* DocumentClient methods now have parity with IDocumentClient.
* Updated direct TCP transport stack to reduce the number of connections established.
* Added support for Direct Mode TCP for non-Windows clients.

### <a name="1.22.0"></a> 1.22.0

* Added ConsistencyLevel Property to FeedOptions.
* Added JsonSerializerSettings to RequestOptions and FeedOptions.
* Added EnableReadRequestsFallback to ConnectionPolicy.

### <a name="1.21.1"></a> 1.21.1

* Fixed KeyNotFoundException for cross partition order by queries in corner cases.
* Fixed bug where JsonPropery attribute in select clause for LINQ queries was not being honored.

### <a name="1.20.2"></a> 1.20.2

* Fixed bug that is hit under certain race conditions, that results in intermittent "Microsoft.Azure.Documents.NotFoundException: The read session is not available for the input session token" errors when using Session consistency level.

### <a name="1.20.1"></a> 1.20.1

* Improved cross partition query performance when the MaxDegreeOfParallelism property is set to -1 in FeedOptions.
* Added a new ToString() function to QueryMetrics.
* Exposed partition statistics on reading collections.
* Added PartitionKey property to ChangeFeedOptions.
* Minor bug fixes.

### <a name="1.19.1"></a> 1.19.1

* Adds the ability to specify unique indexes for the documents by using UniqueKeyPolicy property on the DocumentCollection.
* Fixed a bug in which the custom JsonSerializer settings were not being honored for some queries and stored procedure execution.
 
### <a name="1.19.0"></a> 1.19.0

* Branding change from Azure DocumentDB to Azure Cosmos DB in the API Reference documentation, metadata information in assemblies, and the NuGet package. 
* Expose diagnostic information and latency from the response of requests sent with direct connectivity mode. The property names are RequestDiagnosticsString and RequestLatency on ResourceResponse class.
* This SDK version requires the latest version of Azure Cosmos DB Emulator available for download from https://aka.ms/cosmosdb-emulator.

### <a name="1.18.1"></a> 1.18.1

* Internal changes for Microsoft friends assemblies.

### <a name="1.18.0"></a> 1.18.0

* Added several reliability fixes and improvements.

### <a name="1.17.0"></a> 1.17.0

* Added support for PartitionKeyRangeId as a FeedOption for scoping query results to a specific partition key range value. 
* Added support for StartTime as a ChangeFeedOption to start looking for the changes after that time. 

### <a name="1.16.1"></a> 1.16.1
- Fixed an issue in the JsonSerializable class that may cause a stack overflow exception. 

### <a name="1.16.0"></a> 1.16.0
- Fixed an issue that required recompiling of the application due to the introduction of JsonSerializerSettings as an optional parameter in the DocumentClient constructor.
- Marked the DocumentClient constructor obsolete that required JsonSerializerSettings as the last parameter to allow for default values of ConnectionPolicy and ConsistencyLevel parameters when passing in JsonSerializerSettings parameter.

### <a name="1.15.0"></a> 1.15.0
- Added support for specifying custom JsonSerializerSettings while instantiating DocumentClient.

### <a name="1.14.1"></a> 1.14.1
- Fixed an issue that affected x64 machines that don’t support SSE4 instruction and throw an SEHException when running Azure Cosmos DB DocumentDB API queries.

### <a name="1.14.0"></a> 1.14.0
- Added support for Request Unit per Minute (RU/m) feature.
- Added support for a new consistency level called ConsistentPrefix.
- Added support for query metrics for individual partitions.
- Added support for limiting the size of the continuation token for queries.
- Added support for more detailed tracing for failed requests.
- Made some performance improvements in the SDK.

### <a name="1.13.4"></a> 1.13.4
- Functionally same as 1.13.3. Made some internal changes.

### <a name="1.13.3"></a> 1.13.3
- Functionally same as 1.13.2. Made some internal changes.

### <a name="1.13.2"></a> 1.13.2
- Fixed an issue that ignored the PartitionKey value provided in FeedOptions for aggregate queries.
- Fixed an issue in transparent handling of partition management during mid-flight cross-partition Order By query execution.

### <a name="1.13.1"></a> 1.13.1
- Fixed an issue which caused deadlocks in some of the async APIs when used inside ASP.NET context.

### <a name="1.13.0"></a> 1.13.0
- Fixes to make SDK more resilient to automatic failover under certain conditions.

### <a name="1.15.0"></a> 1.15.0
- Fix for an issue that occasionally causes a WebException: The remote name could not be resolved.
- Added the support for directly reading a typed document by adding new overloads to ReadDocumentAsync API.

### <a name="1.12.1"></a> 1.12.1
- Added LINQ support for aggregation queries (COUNT, MIN, MAX, SUM, and AVG).
- Fix for a memory leak issue for the ConnectionPolicy object caused by the use of event handler.
- Fix for an issue wherein UpsertAttachmentAsync was not working when ETag was used.
- Fix for an issue wherein cross partition order-by query continuation was not working when sorting on string field.

### <a name="1.12.0"></a> 1.12.0
- Added support for aggregation queries (COUNT, MIN, MAX, SUM, and AVG). See [Aggregation support](/azure/cosmos-db/sql-query-aggregates).
- Lowered minimum throughput on partitioned collections from 10,100 RU/s to 2500 RU/s.

### <a name="1.11.4"></a> 1.11.4

- Fix for an issue wherein some of the cross-partition queries were failing in the 32-bit host process.
- Fix for an issue wherein the session container was not being updated with the token for failed requests in Gateway mode.
- Fix for an issue wherein a query with UDF calls in projection was failing in some cases.

### <a name="1.11.3"></a> 1.11.3

- Fix for an issue wherein the session container was not being updated with the token for failed requests. 
- Added support for the SDK to work in a 32-bit host process. Note that if you use cross partition queries, 64-bit host processing is recommended for improved performance.
- Improved performance for scenarios involving queries with a large number of partition key values in an IN expression.

### <a name="1.11.1"></a> 1.11.1

- Minor performance fix for the CreateDocumentCollectionIfNotExistsAsync API introduced in 1.11.0. 
- Peformance fix in the SDK for scenarios that involve high degree of concurrent requests.

### <a name="1.11.0"></a> 1.11.0

- Support for new classes and methods to process the [change feed](/azure/cosmos-db/change-feed) of documents within a collection. 
- Support for cross-partition query continuation and some perf improvements for cross-partition queries.
- Addition of CreateDatabaseIfNotExistsAsync and CreateDocumentCollectionIfNotExistsAsync methods.
- LINQ support for system functions: IsDefined, IsNull and IsPrimitive.
- Fix for automatic binplacing of Microsoft.Azure.Documents.ServiceInterop.dll and DocumentDB.Spatial.Sql.dll assemblies to application’s bin folder when using the Nuget package with projects that have project.json tooling.
- Support for emitting client side ETW traces which could be helpful in debugging scenarios.

### <a name="1.10.0"></a> 1.10.0

- Added direct connectivity support for partitioned collections.
- Improved performance for the Bounded Staleness consistency level.
- Added LINQ support for StringEnumConverter, IsoDateTimeConverter and UnixDateTimeConverter while translating predicates.
- Various SDK bug fixes.

### <a name="1.9.5"></a> 1.9.5

- Fixed an issue that caused the following NotFoundException: The read session is not available for the input session token. This exception occurred in some cases when querying for the read-region of a geo-distributed account.
- Exposed the ResponseStream property in the ResourceResponse class, which enables direct access to the underlying stream from a response. 

### <a name="1.9.4"></a> 1.9.4

- Modified the ResourceResponse, FeedResponse, StoredProcedureResponse and MediaResponse classes to implement the corresponding public interface so that they can be mocked for test driven deployment (TDD).
- Fixed an issue that caused a malformed partition key header when using a custom JsonSerializerSettings object for serializing data.

### <a name="1.9.3"></a> 1.9.3

- Fixed an issue that caused long running queries to fail with error: Authorization token is not valid at the current time.
- Fixed an issue that removed the original SqlParameterCollection from cross partition top/order-by queries.

### <a name="1.9.2"></a> 1.9.2

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


### <a name="1.8.0"></a> 1.8.0

- Added the support for multi-region database accounts.
- Added support for retry on throttled requests.User can customize the number of retries and the max wait time by configuring the ConnectionPolicy.RetryOptions property.
- Added a new IDocumentClient interface that defines the signatures of all DocumenClient properties and methods. As part of this change, also changed extension methods that create IQueryable and IOrderedQueryable to methods on the DocumentClient class itself.
- Added configuration option to set the ServicePoint.ConnectionLimit for a given DocumentDB endpoint Uri. Use ConnectionPolicy.MaxConnectionLimit to change the default value, which is set to 50.
- Deprecated IPartitionResolver and its implementation. Support for IPartitionResolver is now obsolete. It's recommended that you use Partitioned Collections for higher storage and throughput.


### <a name="1.7.1"></a> 1.7.1

- Added an overload to Uri based ExecuteStoredProcedureAsync method that takes RequestOptions as a parameter.

### <a name="1.7.0"></a> 1.7.0

- Added time to live (TTL) support for documents.

### <a name="1.6.3"></a> 1.6.3

- Fixed a bug in Nuget packaging of .NET SDK for packaging it as part of a Azure Cloud Service solution.

### <a name="1.6.2"></a> 1.6.2

- Implemented [partitioned collections](/azure/cosmos-db/partitioning-overview) and user-defined performance levels. 

### <a name="1.5.3"></a> 1.5.3

- **[Fixed]** Querying DocumentDB endpoint throws: 'System.Net.Http.HttpRequestException: Error while copying content to a stream.

### <a name="1.5.2"></a> 1.5.2

- Expanded LINQ support including new operators for paging, conditional expressions and range comparison. 
	- Take operator to enable SELECT TOP behavior in LINQ.
	- CompareTo operator to enable string range comparisons.
	- Conditional (?) and coalesce operators (??).

- **[Fixed]** ArgumentOutOfRangeException when combining Model projection with Where-In in linq query. [#81](https://github.com/Azure/azure-documentdb-dotnet/issues/81)

### <a name="1.5.1"></a> 1.5.1

- **[Fixed]** If Select is not the last expression the LINQ Provider assumed no projection and produced SELECT * incorrectly. [#58](https://github.com/Azure/azure-documentdb-dotnet/issues/58)

### <a name="1.5.0"></a> 1.5.0

- Implemented Upsert, Added UpsertXXXAsync methods.
- Performance improvements for all requests.
- LINQ Provider support for conditional, coalesce and CompareTo methods for strings.
- **[Fixed]** LINQ provider --> Implement Contains method on List to generate the same SQL as on IEnumerable and Array.
- **[Fixed]** BackoffRetryUtility uses the same HttpRequestMessage again instead of creating a new one on retry.
- **[Obsolete]** UriFactory.CreateCollection --> should now use UriFactory.CreateDocumentCollection.

### <a name="1.4.1"></a> 1.4.1

- **[Fixed]** Localization issues when using non en culture info such as nl-NL etc. 

### <a name="1.4.0"></a> 1.4.0
- ID Based Routing 
	- New UriFactory helper to assist with constructing ID based resource links.
	- New overloads on DocumentClient to take in URI.

- Added IsValid() and IsValidDetailed() in LINQ for geospatial.
- LINQ Provider support enhanced.
	- **Math** - Abs, Acos, Asin, Atan, Ceiling, Cos, Exp, Floor, Log, Log10, Pow, Round, Sign, Sin, Sqrt, Tan, Truncate.
	- **String** - Concat, Contains, EndsWith, IndexOf, Count, ToLower, TrimStart, Replace, Reverse, TrimEnd, StartsWith, SubString, ToUpper.
	- **Array** - Concat, Contains, Count.
	- **IN** operator.


### <a name="1.3.0"></a> 1.3.0
- Added support for modifying indexing policies.
	- New ReplaceDocumentCollectionAsync method in DocumentClient.
	- New IndexTransformationProgress property in ResourceResponse for tracking percent progress of index policy changes.
	- DocumentCollection.IndexingPolicy is now mutable.

- Added support for spatial indexing and query 
	- New Microsoft.Azure.Documents.Spatial namespace for serializing/deserializing spatial types like Point and Polygon.
	- New SpatialIndex class for indexing GeoJSON data stored in DocumentDB.

- **[Fixed]** : Incorrect SQL query generated from linq expression. [#38](https://github.com/Azure/azure-documentdb-dotnet/issues/38)

### <a name="1.2.0"></a> 1.2.0
- Dependency on Newtonsoft.Json v5.0.7.

- Changes to support Order By:
	- LINQ provider support for OrderBy() or OrderByDescending().
	- IndexingPolicy to support Order By.

	**NB: Possible breaking change** 

	If you have existing code that provisions collections with a custom indexing policy, then your existing code will need to be updated to support the new IndexingPolicy class. If you have no custom indexing policy, then this change does not affect you.

### <a name="1.1.0"></a> 1.1.0
- Support for partitioning data by using the new HashPartitionResolver and RangePartitionResolver classes and the IPartitionResolver.
- DataContract serialization.
- Guid support in LINQ provider.
- UDF support in LINQ.

### <a name="1.0.0"></a> 1.0.0
- GA SDK.

## <a name="known-issues"></a> Known issues

Below is a list of any know issues affecting the [recommended minimum version](#recommended-version):

| Issue | Impact | Mitigation | Tracking link |
| --- | --- | --- | --- |


## Release & Retirement dates

Microsoft provides notification at least **12 months** in advance of retiring an SDK in order to smooth the transition to a newer/supported version. New features and functionality and optimizations are only added to the current SDK, as such it is recommended that you always upgrade to the latest SDK version as early as possible. 

Azure Cosmos DB will no longer make bug fixes, add new features, and provide support to versions 1.x and 2.x of the Azure Cosmos DB .NET or .NET Core SDK for SQL API. If you prefer not to upgrade, requests sent from version 1.x or 2.x of the SDK will continue to be served by the Azure Cosmos DB service.  We recommend following the [instructions](https://docs.microsoft.com/azure/cosmos-db/sql/migrate-dotnet-v3?tabs=dotnet-v3) to migrate to the latest version of the Azure Cosmos DB .NET SDK.


| Version | Release Date | Retirement Date |
| --- | --- | --- |
| [2.18.0](#2.18.0) |April 15, 2022 | August 31, 2024 |
| [2.17.0](#2.17.0) |March 3, 2022 | August 31, 2024 |
| [2.16.2](#2.16.2) |October 26, 2021 | August 31, 2024 |
| [2.16.1](#2.16.1) |September 25, 2021 | August 31, 2024 |
| [2.16.0](#2.16.0) |August 27, 2021 | August 31, 2024 |
| [2.15.0](#2.15.0) |June 21, 2021 | August 31, 2024 |
| [2.14.1](#2.14.1) |May 10, 2021 | August 31, 2024 |
| [2.14.0](#2.14.0) |April 16, 2021 | August 31, 2024 |
| [2.12.0](#2.12.0) |October 7, 2020 | August 31, 2024 |
| [2.11.6](#2.11.6) |August 12, 2020 | August 31, 2024 |
| [2.11.5](#2.11.5) |August 4, 2020 | August 31, 2024 |
| [2.11.4](#2.11.4) |July 30, 2020 | August 31, 2024 |
| [2.11.3](#2.11.3) |July 29, 2020 | August 31, 2024 |
| [2.11.2](#2.11.2) |July 14, 2020 | August 31, 2024 |
| [2.11.1](#2.11.1) |July 1, 2020 | August 31, 2024 |
| [2.9.2](#2.9.2) |November 15, 2019 | August 31, 2024 |
| [2.9.1](#2.9.1) |November 13, 2019 | August 31, 2024 |
| [2.9.0](#2.9.0) |October 30, 2019 | August 31, 2024 |
| [2.8.1](#2.8.1) |October 11, 2019 | August 31, 2024 |
| [2.7.0](#2.7.0) |September 23, 2019 | August 31, 2024 |
| [2.6.0](#2.6.0) |August 30, 2019 | August 31, 2024 |
| [2.5.1](#2.5.1) |July  02, 2019 | August 31, 2024 |
| [2.4.1](#2.4.1) |June  20, 2019 | August 31, 2024 |
| [2.4.0](#2.4.0) |May  05, 2019 | August 31, 2024 |
| [2.3.0](#2.3.0) |April  04, 2019 | August 31, 2024 |
| [2.2.3](#2.2.3) |February 11, 2019 | August 31, 2024 |
| [2.2.2](#2.2.2) |February 06, 2019 | August 31, 2024 |
| [2.2.1](#2.2.1) |December 24, 2018 | August 31, 2024 |
| [2.2.0](#2.2.0) |December 07, 2018 | August 31, 2024 |
| [2.1.3](#2.1.3) |October 15, 2018 | August 31, 2024 |
| [2.1.2](#2.1.2) |October 04, 2018 | August 31, 2024 |
| [2.1.1](#2.1.1) |September 27, 2018 | August 31, 2024 |
| [2.1.0](#2.1.0) |September 21, 2018 | August 31, 2024 |
| [2.0.0](#2.0.0) |September 07, 2018 | August 31, 2024 |
| [1.22.0](#1.22.0) |April 19, 2018 | August 31, 2022 |
| [1.21.1](#1.20.1) |March 09, 2018 | August 31, 2022  |
| [1.20.2](#1.20.1) |February 21, 2018 | August 31, 2022  |
| [1.20.1](#1.20.1) |February 05, 2018 | August 31, 2022  |
| [1.19.1](#1.19.1) |November 16, 2017 | August 31, 2022  |
| [1.19.0](#1.19.0) |November 10, 2017 | August 31, 2022  |
| [1.18.1](#1.18.1) |November 07, 2017 | August 31, 2022  |
| [1.18.0](#1.18.0) |October 17, 2017 | August 31, 2022  |
| [1.17.0](#1.17.0) |August 10, 2017 | August 31, 2022  |
| [1.16.1](#1.16.1) |August 07, 2017 | August 31, 2022  |
| [1.16.0](#1.16.0) |August 02, 2017 | August 31, 2022  |
| [1.15.0](#1.15.0) |June 30, 2017 | August 31, 2022 |
| [1.14.1](#1.14.1) |May 23, 2017 | August 31, 2022  |
| [1.14.0](#1.14.0) |May 10, 2017 | August 31, 2022  |
| [1.13.4](#1.13.4) |May 09, 2017 | August 31, 2022  |
| [1.13.3](#1.13.3) |May 06, 2017 | August 31, 2022  |
| [1.13.2](#1.13.2) |April 19, 2017 | August 31, 2022  |
| [1.13.1](#1.13.1) |March 29, 2017 | August 31, 2022  |
| [1.13.0](#1.13.0) |March 24, 2017 | August 31, 2022  |
| [1.12.1](#1.12.1) |March 14, 2017 | August 31, 2022  |
| [1.12.0](#1.12.0) |February 15, 2017 | August 31, 2022  |
| [1.11.4](#1.11.4) |February 06, 2017 | August 31, 2022  |
| [1.11.3](#1.11.3) |January 26, 2017 | August 31, 2022  |
| [1.11.1](#1.11.1) |December 21, 2016 | August 31, 2022  |
| [1.11.0](#1.11.0) |December 08, 2016 | August 31, 2022  |
| [1.10.0](#1.10.0) |September 27, 2016 | August 31, 2022  |
| [1.9.5](#1.9.5) |September 01, 2016 | August 31, 2022  |
| [1.9.4](#1.9.4) |August 24, 2016 | August 31, 2022  |
| [1.9.3](#1.9.3) |August 15, 2016 | August 31, 2022 |
| [1.9.2](#1.9.2) |July 23, 2016 | August 31, 2022  |
| [1.8.0](#1.8.0) |June 14, 2016 | August 31, 2022  |
| [1.7.1](#1.7.1) |May 06, 2016 | August 31, 2022  |
| [1.7.0](#1.7.0) |April 26, 2016 | August 31, 2022  |
| [1.6.3](#1.6.3) |April 08, 2016 | August 31, 2022  |
| [1.6.2](#1.6.2) |March 29, 2016 | August 31, 2022  |
| [1.5.3](#1.5.3) |February 19, 2016 | August 31, 2022 |
| [1.5.2](#1.5.2) |December 14, 2015 | August 31, 2022  |
| [1.5.1](#1.5.1) |November 23, 2015 | August 31, 2022  |
| [1.5.0](#1.5.0) |October 05, 2015 | August 31, 2022  |
| [1.4.1](#1.4.1) |August 25, 2015 | August 31, 2022  |
| [1.4.0](#1.4.0) |August 13, 2015 | August 31, 2022  |
| [1.3.0](#1.3.0) |August 05, 2015 | August 31, 2022 |
| [1.2.0](#1.2.0) |July 06, 2015 | August 31, 2022  |
| [1.1.0](#1.1.0) |April 30, 2015 | August 31, 2022  |
| [1.0.0](#1.0.0) |April 08, 2015 |  August 31, 2022  |
