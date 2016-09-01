**Important:**
You may receive System.NotSupportedException when querying partitioned collections. To avoid this error, uncheck the "Prefer 32-bit" option in your project properties window, on the Build tab.

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


**Note:** 
	There was a change of NuGet package name between preview and GA. We moved from **Microsoft.Azure.Documents.Client** to **Microsoft.Azure.DocumentDB**.


## Changes in 0.9.x-preview : ##
- Preview SDKs. **[Obsolete]**
