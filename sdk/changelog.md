## Changes in 1.5.1 : ##
- **[Fixed]** If Select is not the last expression the LINQ Provider assumed no projection and produced SELECT * incorrectly.  [#58](https://github.com/Azure/azure-documentdb-dotnet/issues/58) 

## Changes in 1.5.0 : ##
 - Implemented Upsert, Added UpsertXXXAsync methods
 - Performance improvements for all requests
 - LINQ Provider support for conditional, coalesce and CompareTo methods for strings
 - **[Fixed]** LINQ provider --> Contains method on a List. (now generates the same SQL as on IEnumerable and Array)
 - **[Obsolete]** UriFactory.CreateCollection --> should now use UriFactory.CreateDocumentCollection
 
## Changes in 1.4.1 : ##
 - Fixing localization issues when using non en culture info such as nl-NL etc. 
 
## Changes in 1.4.0 : ##
  - ID Based Routing
    - New UriFactory helper to assist with constructing ID based resource links
    - New overloads on DocumentClient to take in URI
  - Added IsValid() and IsValidDetailed() in LINQ for geospatial
  - LINQ Provider support enhanced
    - **Math** - Abs, Acos, Asin, Atan, Ceiling, Cos, Exp, Floor, Log, Log10, Pow, Round, Sign, Sin, Sqrt, Tan, Truncate
    - **String** - Concat, Contains, EndsWith, IndexOf, Count, ToLower, TrimStart, Replace, Reverse, TrimEnd, StartsWith, SubString, ToUpper
    - **Array** - Concat, Contains, Count
    - **IN** operator

## Changes in 1.3.0 : ##
  - Added support for modifying indexing policies
    - New ReplaceDocumentCollectionAsync method in DocumentClient
    - New IndexTransformationProgress property in ResourceResponse<T> for tracking percent progress of index policy changes
    - DocumentCollection.IndexingPolicy is now mutable
  - Added support for spatial indexing and query
    - New Microsoft.Azure.Documents.Spatial namespace for serializing/deserializing spatial types like Point and Polygon
    - New SpatialIndex class for indexing GeoJSON data stored in DocumentDB
  - Fixed : Incorrect SQL query generated from linq expression [#38](https://github.com/Azure/azure-documentdb-net/issues/38)

## Changes in 1.2.0 : ##
- Dependency on Newtonsoft.Json v5.0.7 
- Changes to support Order By
  - LINQ provider support for OrderBy() or OrderByDescending()
  - IndexingPolicy to support Order By (**NB: Possible breaking change**) 
  
    If you have existing code that provisions collections with a custom indexing policy, then your existing code will need to be updated to support the new IndexingPolicy class. If you have no custom indexing policy, then this change does not affect you.

## Changes in 1.1.0 : ##
- Support for partitioning data by using the new HashPartitionResolver and RangePartitionResolver classes and the IPartitionResolver
- DataContract serialization
- Guid support in LINQ provider
- UDF support in LINQ

## Changes in 1.0.0 : ##
- GA SDK
