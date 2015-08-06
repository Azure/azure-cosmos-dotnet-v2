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
