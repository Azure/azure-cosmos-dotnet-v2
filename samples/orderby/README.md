#Order By Support in DocumentDB!
In this Github project, you'll find code samples in .NET on how to create and run Order by queries using Azure DocumentDB. We look at:

- How to Query with Order By
- Configure Indexing Policy for Order By
- What's coming next?
- FAQ


##How to Query with Order By
Like in ANSI-SQL, you can now include an optional Order By clause while querying using DocumentDB SQL. The clause can include an optional ASC/DESC argument to specify the order in which results must be retrieved. Here we take a look at an example query to retrieve books in descending order of PublishTimestamp. 

**Find all books ordered by latest published:**
```sql
    SELECT * 
    FROM Books 
    ORDER BY Books.PublishTimestamp DESC
```
You can order using any nested property within documents like Books.ShippingDetails.Weight, and you can specify additional filters in the WHERE clause in combination with Order By like in this example:

**Find all books ordered by shipping weight with price over $4000:**
```sql
    SELECT * 
    FROM Books 
	WHERE Books.SalePrice > 4000
    ORDER BY Books.ShippingDetails.Weight
```
Using the .NET SDK version 1.2.0 and higher, you can also use the OrderBy() or OrderByDescending() clause within LINQ queries like in this example:

**OrderBy using the LINQ Provider for .NET**
```cs
    foreach (Book book in client.CreateDocumentQuery<Book>(booksCollection.SelfLink)
        .OrderBy(b => b.PublishTimestamp)) 
    {
        // Iterate through books
    }
```
Using the native paging support within the DocumentDB SDKs, you can retrieve results one page at a time like in the .NET code snippet shown below. Here we fetch results up to 10 at a time using the FeedOptions.MaxItemCount and the IDocumentQuery interface.

**Ordering with Paging using the .NET SDK**
```cs
    var booksQuery = client.CreateDocumentQuery<Book>(
        booksCollection.SelfLink,
        "SELECT * FROM Books ORDER BY Books.PublishTimestamp DESC"
        new FeedOptions { MaxItemCount = 10 })
      .AsDocumentQuery();
            
    while (booksQuery.HasMoreResults) 
    {
        foreach(Book book in await booksQuery.ExecuteNextAsync<Book>())
        {
            // Iterate through books
        }
    }
```
DocumentDB supports ordering against numeric types (not strings), and only for a single Order By property per query in this preview of the feature. Please see "What's coming next" for more details.

##Configure Indexing Policy for Order By
In order to support Order By queries, we have introduced a special indexing policy configuration for creating range indexes at the maximum precision. During collection creation, you can either index specific paths within your documents with maximum precision, or index all paths recursively for the entire collection in order to sort on any property. 

Max Precision (represented as precision of -1 in JSON config) improves upon the current DocumentDB indexing scheme by utilizing a variable number of bytes depending on the value that's being indexed. For datasets with large numbers e.g., epoch timestamps, Max Precision will have the same indexing storage overhead as an index precision of 7. However, for datasets containing smaller values (enumerations, zeroes, zip codes, ages, etc.), Max Precision indexing will have a lower index storage overhead than fixed precision configurations.

**Indexing for Order By against all numeric properties:**

Here's how you can create a collection with indexing for Order By against any (numeric) property.                                                       
```cs
    booksCollection.IndexingPolicy.IncludedPaths.Add(
        new IndexingPath {
            IndexType = IndexType.Range, 
            Path = "/",
            NumericPrecision = -1 });

    await client.CreateDocumentCollectionAsync(databaseLink, 
        booksCollection);  
```
**Indexing for Order By for a single property**

Here's how you can create a collection with indexing for Order By against just the PublishTimestamp property.                                                       
```cs
    booksCollection.IndexingPolicy.IncludedPaths.Add(
        new IndexingPath {
            IndexType = IndexType.Range,
            Path = "/\"PublishTimestamp\"/?",
            NumericPrecision = -1
        });

    booksCollection.IndexingPolicy.IncludedPaths.Add(
        new IndexingPath {
            Path = "/"
        });
```
Once created, the collection can be used for Order By! 

We are working on some changes that will allow you to dynamically modify the indexing policy of a collection after creation. Please see the "What's coming next" section for more details. 

##What's coming next?

The upcoming service updates will be expanding on Order By support introduced here. We are working on the following additions and will prioritize the release of these improvements based on your feedback:

- Dynamic Indexing Policies: Support to modify indexing policy after collection creation
- String range indexes: Index to support range queries (>, <, >=, <=) against string values. In order to support this, we will be introducing a new richer schema for indexing policies.
- Support for String Order By in DocumentDB query.
- Ability to update indexing policy using the Azure Preview Portal.
- Support for Compound Indexes for more efficient Order By and Order By on multiple properties.


##FAQ

**Which platforms/versions of the SDK support ordering?**

Since Order By is a server-side update, you do not need to download a new version of the SDK to use this feature. All platforms and versions of the SDK, including the server-side JavaScript SDK can use Order By using SQL query strings. If you're using LINQ, you should download version 1.2.0 or newer from Nuget.

**What is the expected Request Units (RU) consumption of Order By queries?**

Since Order By utilizes the DocumentDB index for lookups, the number of request units consumed by Order By queries will be similar to the equivalent queries without Order By. Like any other operation on DocumentDB, the number of request units depends on the sizes/shapes of documents as well as the complexity of the query. 


**What is the expected indexing overhead for Order By?**

The indexing storage overhead will be proportionate to the number of numeric properties. In the worst case scenario, the index overhead will be 100% of the data. There is no difference in throughput (Request Units) overhead between Range/Order By indexing and the default Hash indexing.

**Does this change impact queries without Order By?**

There are no changes introduced in how queries without Order By work today. Prior to the release of this feature, all DocumentDB queries returned results in ResourceId (_rid) order. With Order By, queries will naturally be returned in the specified order of values. In Order By queries, _rid will be used as a secondary sort order when there are multiple documents returned with the same value.

**How do I query my existing data in DocumentDB using Order By?**

This will be supported with the availability of the  Dynamic Indexing Policies improvement mentioned in the "What's Coming Next" section. In order to do this today, you have to export your data and re-import into a new DocumentDB collection created with a Range/Order By Index. The DocumentDB Import Tool can be used to migrate your data between collections. 

**What are the current limitations of Order By?**

Order By can be specified only against a numeric property, and only when it is range indexed with Max Precision (-1) indexing. Order By is supported only against document collections.

You cannot perform the following:
 
- Order By with string properties (coming soon).
- Order By with internal string properties like id, _rid, and _self (coming soon).
- Order By with properties derived from the result of an intra-document join (coming soon).
- Order By multiple properties (coming soon).
- Order By with computed properties e.g. the result of an expression or a UDF/built-in function.
- Order By with queries on databases, collections, users, permissions or attachments.

##References
* [DocumentDB Query Reference](http://azure.microsoft.com/documentation/articles/documentdb-sql-query/)
* [DocumentDB Indexing Policy Reference](https://azure.microsoft.com/documentation/articles/documentdb-indexing-policies/)
* [DocumentDB .NET SDK Documentation](https://msdn.microsoft.com/library/azure/dn948556.aspx)

