namespace DocumentDB.Samples.Queries
{
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Documents.Linq;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Linq;
    using System.Threading.Tasks;

    //------------------------------------------------------------------------------------------------
    // This sample demonstrates the use of LINQ and SQL Query Grammar to query DocumentDB Service
    // For additional examples using the SQL query grammer refer to the SQL Query Tutorial available 
    // at https://azure.microsoft.com/documentation/articles/documentdb-sql-query/.
    // There is also an interactive Query Demo web application where you can try out different 
    // SQL queries available at https://www.documentdb.com/sql/demo.  
    //------------------------------------------------------------------------------------------------

    public class Program
    {
        private static DocumentClient client;

        // Assign an id for your database & collection 
        private static readonly string DatabaseName = "samples";
        private static readonly string CollectionName = "query-samples";

        // Read the DocumentDB endpointUrl and authorizationKeys from config
        // These values are available from the Azure Management Portal on the DocumentDB Account Blade under "Keys"
        // NB > Keep these values in a safe & secure location. Together they provide Administrative access to your DocDB account
        private static readonly string endpointUrl = ConfigurationManager.AppSettings["EndPointUrl"];
        private static readonly string authorizationKey = ConfigurationManager.AppSettings["AuthorizationKey"];

        // Set to true for this sample since it deals with different kinds of queries.
        private static readonly FeedOptions DefaultOptions = new FeedOptions { EnableCrossPartitionQuery = true };

        public static async Task Main(string[] args)
        {
            try
            {
                // DocumentClient should be a singleton
                using (client = new DocumentClient(new Uri(endpointUrl), authorizationKey,
                    new ConnectionPolicy { ConnectionMode = ConnectionMode.Direct, ConnectionProtocol = Protocol.Tcp }))
                {
                    await RunDemoAsync(DatabaseName, CollectionName);
                }
            }
#if !DEBUG
            catch (Exception e)
            {
                LogException(e);
            }
#endif
            finally
            {
                Console.WriteLine("End of demo, press any key to exit.");
                Console.ReadKey();
            }
        }

        private static async Task RunDemoAsync(string databaseId, string collectionId)
        {
            Database database = await client.CreateDatabaseIfNotExistsAsync(new Database { Id = databaseId });
            DocumentCollection collection = await GetOrCreateCollectionAsync(databaseId, collectionId);

            Uri collectionUri = UriFactory.CreateDocumentCollectionUri(databaseId, collectionId);

            await CreateDocuments(collectionUri);

            //--------------------------------------------------------------------------------------------------------
            // There are three ways of writing queries in the .NET SDK for DocumentDB, 
            // using the SQL Query Grammar, using LINQ Provider with Query and with Lambda. 
            // This sample will show each query using all methods. 
            // It is entirely up to you which style of query you write as they result in exactly the same query being 
            // executed on the service. 
            // 
            // There are some occasions when one syntax has advantages over others, but it's your choice which to use when
            //--------------------------------------------------------------------------------------------------------

            // Querying for all documents
            await QueryAllDocumentsAsync(collectionUri);

            // Querying for equality using ==
            await QueryWithEquality(collectionUri);

            // Querying for inequality using != and NOT
            await QueryWithInequality(collectionUri);

            // Querying using range operators like >, <, >=, <=
            await QueryWithRangeOperatorsOnNumbers(collectionUri);

            // Querying using range operators against strings. Needs a different indexing policy or the EnableScanInQuery directive.
            await QueryWithRangeOperatorsOnStrings(collectionUri);

            await QueryWithRangeOperatorsDateTimes(collectionUri);

            // Querying with order by
            await QueryWithOrderBy(collectionUri);

            // Query with aggregate operators - Sum, Min, Max, Average, and Count
            await QueryWithAggregates(collectionUri);

            // Work with subdocuments
            await QueryWithSubdocuments(collectionUri);

            // Query with Intra-document Joins
            await QueryWithJoins(collectionUri);

            // Query with string, math and array operators
            await QueryWithStringMathAndArrayOperators(collectionUri);

            // Query with parameterized SQL using SqlQuerySpec
            await QueryWithSqlQuerySpec(collectionUri);

            // Query with explict Paging
            await QueryWithPagingAsync(collectionUri);

            // Query across multiple partitions in parallel
            await QueryPartitionedCollectionInParallelAsync(collectionUri);

            // Query using order by across multiple partitions
            await QueryWithOrderByForPartitionedCollectionAsync(collectionUri);

            // Uncomment to Cleanup
            // await client.DeleteDatabaseAsync(UriFactory.CreateDatabaseUri(databaseId));
        }

        private static async Task QueryAllDocumentsAsync(Uri collectionUri)
        {
            // LINQ Query
            var families =
                from f in client.CreateDocumentQuery<Family>(collectionUri, DefaultOptions)
                select f;

            int count = await families.CountAsync();
            Assert("Expected two families", count == 2);

            // LINQ Lambda
            IQueryable<Family> allfamiliesQuery = client.CreateDocumentQuery<Family>(collectionUri, DefaultOptions);
            IList<Family> allFamiles = await ExecuteQuery(allfamiliesQuery);
            Assert("Expected two families", allFamiles.Count == 2);

            // SQL
            allfamiliesQuery = client.CreateDocumentQuery<Family>(
                documentCollectionOrDatabaseUri: collectionUri,
                sqlExpression: "SELECT * FROM Families",
                feedOptions: DefaultOptions);

            allFamiles = await ExecuteQuery(allfamiliesQuery);
            Assert("Expected two families", allFamiles.Count == 2);
        }

        private static async Task QueryWithSqlQuerySpec(Uri collectionUri)
        {
            // Simple query with a single property equality comparison
            // in SQL with SQL parameterization instead of inlining the 
            // parameter values in the query string
            // LINQ Query -- Id == "value"
            var query = client.CreateDocumentQuery<Family>(collectionUri, new SqlQuerySpec()
            {
                QueryText = "SELECT * FROM Families f WHERE (f.id = @id)",
                Parameters = new SqlParameterCollection()
                    {
                        new SqlParameter("@id", "AndersenFamily")
                    }
            }, DefaultOptions);

            IList<Family> families = await ExecuteQuery(query);
            Assert("Expected only 1 family", families.Count == 1);

            // Query using two properties within each document. WHERE Id == "" AND Address.City == ""
            // notice here how we are doing an equality comparison on the string value of City

            query = client.CreateDocumentQuery<Family>(
                collectionUri,
                new SqlQuerySpec()
                {
                    QueryText = "SELECT * FROM Families f WHERE f.id = @id AND f.Address.City = @city",
                    Parameters = new SqlParameterCollection()
                    {
                        new SqlParameter("@id", "AndersenFamily"),
                        new SqlParameter("@city", "Seattle")
                    }
                }, DefaultOptions);

            families = await ExecuteQuery(query);
            Assert("Expected only 1 family", families.Count == 1);
        }

        private static async Task QueryWithEquality(Uri collectionUri)
        {
            // Simple query with a single property equality comparison
            await QueryWithEqualsOnId(collectionUri);

            // Query using two properties within each document (WHERE Id == "" AND Address.City == "")
            // Notice here how we are doing an equality comparison on the string value of City
            await QueryWithAndFilter(collectionUri);

            //Query using a filter on two properties and include a custom projection
            //in to a new anonymous type
            await QueryWithAndFilterAndProjection(collectionUri);
        }

        private static async Task QueryWithAndFilterAndProjection(Uri collectionUri)
        {
            // LINQ Query -- Id == "value" OR City == "value"
            var query =
                (from f in client.CreateDocumentQuery<Family>(collectionUri, DefaultOptions)
                 where f.Id == "AndersenFamily" || f.Address.City == "NY"
                 select new { Name = f.LastName, City = f.Address.City }).AsDocumentQuery();

            while (query.HasMoreResults)
            {
                var items2 = await query.ExecuteNextAsync();
                foreach (var item in items2)
                {
                    Console.WriteLine("The {0} family live in {1}", item.Name, item.City);
                }
            }

            var query2 = client.CreateDocumentQuery<Family>(
                collectionUri,
                new FeedOptions { MaxItemCount = 1, EnableCrossPartitionQuery = true })
                .Where(d => d.LastName == "Andersen")
                .Select(f => new { Name = f.LastName })
                .AsDocumentQuery();

            while (query2.HasMoreResults)
            {
                var items2 = await query2.ExecuteNextAsync();
                foreach (var item in items2)
                {
                    Console.WriteLine("The {0} family.", item.Name);
                }
            }

            // LINQ Lambda -- Id == "value" OR City == "value"
            var query3 = (client.CreateDocumentQuery<Family>(collectionUri, DefaultOptions)
                       .Where(f => f.Id == "AndersenFamily" || f.Address.City == "NY")
                       .Select(f => new { Name = f.LastName, City = f.Address.City })).AsDocumentQuery();

            while (query3.HasMoreResults)
            {
                var items = await query3.ExecuteNextAsync();
                foreach (var item in items)
                {
                    Console.WriteLine("The {0} family live in {1}", item.Name, item.City);
                }
            }

            // SQL -- Id == "value" OR City == "value"
            var query4 = client.CreateDocumentQuery(collectionUri,
                "SELECT f.LastName AS Name, f.Address.City AS City " +
                "FROM Families f " +
                "WHERE f.id='AndersenFamily' OR f.Address.City='NY'", DefaultOptions);

            var items4 = await ExecuteQuery<dynamic>(query4);
            foreach (var item in items4)
            {
                Console.WriteLine("The {0} family live in {1}", item.Name, item.City);
            }
        }

        private static async Task QueryWithAndFilter(Uri collectionUri)
        {
            // LINQ Query
            var query = from f in client.CreateDocumentQuery<Family>(collectionUri, DefaultOptions)
                        where f.Id == "AndersenFamily" && f.Address.City == "Seattle"
                        select f;

            var families = await ExecuteQuery(query);
            Assert("Expected only 1 family", families.Count == 1);

            // LINQ Lambda -- Id == "value" AND City == "value"
            query = client.CreateDocumentQuery<Family>(collectionUri, DefaultOptions)
                .Where(f => f.Id == "AndersenFamily" && f.Address.City == "Seattle");

            families = await ExecuteQuery(query);
            Assert("Expected only 1 family", families.Count == 1);

            // SQL -- Id == "value" AND City == "value"
            query = client.CreateDocumentQuery<Family>(
                collectionUri,
                "SELECT * FROM Families f WHERE f.id='AndersenFamily' AND f.Address.City='Seattle'",
                DefaultOptions);

            families = await ExecuteQuery(query);
            Assert("Expected only 1 family", families.Count == 1);
        }

        private static async Task QueryWithEqualsOnId(Uri collectionUri)
        {
            // LINQ Query -- Id == "value"
            var familiesQuery =
                from f in client.CreateDocumentQuery<Family>(collectionUri, DefaultOptions)
                where f.Id == "AndersenFamily"
                select f;

            var families = await ExecuteQuery(familiesQuery);
            Assert("Expected only 1 family", families.Count == 1);

            // LINQ Lambda -- Id == "value"
            familiesQuery = client.CreateDocumentQuery<Family>(collectionUri, DefaultOptions).Where(f => f.Id == "AndersenFamily");
            families = await ExecuteQuery(familiesQuery);
            Assert("Expected only 1 family", families.Count == 1);

            // SQL -- Id == "value"
            familiesQuery = client.CreateDocumentQuery<Family>(
                collectionUri,
                "SELECT * FROM Families f WHERE f.id='AndersenFamily'",
                DefaultOptions);
            families = await ExecuteQuery(familiesQuery);
            Assert("Expected only 1 family", families.Count == 1);
        }

        private static async Task QueryWithInequality(Uri collectionUri)
        {
            // Simple query with a single property inequality comparison
            // LINQ Query
            var query = from f in client.CreateDocumentQuery<Family>(collectionUri, DefaultOptions)
                        where f.Id != "AndersenFamily"
                        select f;

            var families = await ExecuteQuery(query);
            Assert("Expected only 1 family", families.Count == 1);

            // LINQ Lambda
            query = client.CreateDocumentQuery<Family>(collectionUri, DefaultOptions)
                       .Where(f => f.Id != "AndersenFamily");

            families = await ExecuteQuery(query);
            Assert("Expected only 1 family", families.Count == 1);


            // SQL - in SQL you can use <> interchangably with != for "not equals"
            query = client.CreateDocumentQuery<Family>(
                collectionUri,
                "SELECT * FROM Families f WHERE f.id <> 'AndersenFamily'",
                DefaultOptions);

            families = await ExecuteQuery(query);
            Assert("Expected only 1 family", families.ToList().Count == 1);

            //combine equality and inequality
            query =
                from f in client.CreateDocumentQuery<Family>(collectionUri, DefaultOptions)
                where f.Id == "Wakefield" && f.Address.City != "NY"
                select f;

            families = await ExecuteQuery(query);
            Assert("Expected no results", families.Count == 0);

            query = client.CreateDocumentQuery<Family>(
                collectionUri,
                "SELECT * FROM Families f WHERE f.id = 'AndersenFamily' AND f.Address.City != 'NY'",
                DefaultOptions);

            families = await ExecuteQuery(query);
            Assert("Expected only 1 family", families.Count == 1);
        }

        private static async Task QueryWithRangeOperatorsOnNumbers(Uri collectionUri)
        {
            // LINQ Query
            var query = from f in client.CreateDocumentQuery<Family>(collectionUri, DefaultOptions)
                        where f.Children[0].Grade > 5
                        select f;

            var families = await ExecuteQuery(query);
            Assert("Expected only 1 family", families.ToList().Count == 1);

            // LINQ Lambda
            query = client.CreateDocumentQuery<Family>(collectionUri, DefaultOptions)
                       .Where(f => f.Children[0].Grade > 5);

            families = await ExecuteQuery(query);
            Assert("Expected only 1 family", families.Count == 1);

            // SQL
            query = client.CreateDocumentQuery<Family>(collectionUri,
                "SELECT * FROM Families f WHERE f.Children[0].Grade > 5",
                DefaultOptions);

            families = await ExecuteQuery(query);
            Assert("Expected only 1 family", families.Count == 1);
        }

        private static async Task QueryWithRangeOperatorsOnStrings(Uri collectionUri)
        {
            // LINQ
            var query = client.CreateDocumentQuery<Family>(collectionUri, DefaultOptions)
                .Where(f => f.Address.State.CompareTo("NY") > 0);

            var families = await ExecuteQuery(query);
            Assert("Expected only 1 family", families.Count == 1);

            // SQL Query
            query = client.CreateDocumentQuery<Family>(
                collectionUri,
                "SELECT * FROM Families f WHERE f.Address.State > 'NY'",
                DefaultOptions);

            families = await ExecuteQuery(query);
            Assert("Expected only 1 family", families.Count == 1);
        }

        private static async Task QueryWithRangeOperatorsDateTimes(Uri collectionUri)
        {
            var query = client.CreateDocumentQuery<Family>(collectionUri, DefaultOptions)
                .Where(f => f.RegistrationDate >= DateTime.UtcNow.AddDays(-3));

            var families = await ExecuteQuery(query);
            Assert("Expected only 1 family", families.Count == 1);

            query = client.CreateDocumentQuery<Family>(collectionUri,
                string.Format("SELECT * FROM c WHERE c.RegistrationDate >= '{0}'",
                DateTime.UtcNow.AddDays(-3).ToString("o")), DefaultOptions);

            families = await ExecuteQuery(query);
            Assert("Expected only 1 family", families.Count == 1);
        }

        private static async Task QueryWithOrderBy(Uri collectionUri)
        {
            // Order by with numbers. Works with default IndexingPolicy
            await QueryWithOrderByNumbers(collectionUri);

            // Order by with strings. Needs custom indexing policy. See GetOrCreateCollectionAsync
            await QueryWithOrderByStrings(collectionUri);
        }

        private static async Task QueryWithOrderByNumbers(Uri collectionUri)
        {
            // LINQ Query
            IQueryable<Family> query =
                from f in client.CreateDocumentQuery<Family>(collectionUri, DefaultOptions)
                where f.LastName == "Andersen"
                orderby f.Children[0].Grade
                select f;

            var families = await ExecuteQuery(query);
            Assert("Expected 1 families", families.Count == 1);

            // LINQ Lambda
            query = client.CreateDocumentQuery<Family>(collectionUri, DefaultOptions)
                .Where(f => f.LastName == "Andersen")
                .OrderBy(f => f.Children[0].Grade);

            families = await ExecuteQuery(query);
            Assert("Expected 1 families", families.Count == 1);

            // SQL
            query = client.CreateDocumentQuery<Family>(
                collectionUri,
                "SELECT * FROM Families f WHERE f.LastName = 'Andersen' ORDER BY f.Children[0].Grade",
                DefaultOptions);

            families = await ExecuteQuery(query);
            Assert("Expected 1 families", families.Count == 1);
        }

        private static async Task QueryWithOrderByStrings(Uri collectionUri)
        {
            // LINQ Query
            IQueryable<Family> query = from f in client.CreateDocumentQuery<Family>(collectionUri, DefaultOptions)
                                       where f.LastName == "Andersen"
                                       orderby f.Address.State descending
                                       select f;

            var families = await ExecuteQuery(query);
            Assert("Expected only 1 family", families.Count == 1);

            // LINQ Lambda
            query = client.CreateDocumentQuery<Family>(collectionUri, DefaultOptions)
                       .Where(f => f.LastName == "Andersen")
                       .OrderByDescending(f => f.Address.State);

            families = await ExecuteQuery(query);
            Assert("Expected only 1 family", families.Count == 1);

            // SQL
            query = client.CreateDocumentQuery<Family>(
                collectionUri,
                "SELECT * FROM Families f WHERE f.LastName = 'Andersen' ORDER BY f.Address.State DESC",
                DefaultOptions);

            families = await ExecuteQuery(query);
            Assert("Expected only 1 family", families.Count == 1);
        }

        private static async Task QueryWithAggregates(Uri collectionUri)
        {
            // SQL
            var query = client.CreateDocumentQuery<int>(
                collectionUri,
                "SELECT VALUE COUNT(f) FROM Families f WHERE f.LastName = 'Andersen'",
                DefaultOptions);

            int count = (await ExecuteQuery(query)).First();
            Assert("Expected only 1 family", count == 1);

            // LINQ
            count = await client.CreateDocumentQuery<Family>(collectionUri, DefaultOptions)
                .Where(f => f.LastName == "Andersen")
                .CountAsync();

            Assert("Expected only 1 family", count == 1);

            // SQL over an array within documents
            query = client.CreateDocumentQuery<int>(
                collectionUri,
                "SELECT VALUE COUNT(child) FROM child IN f.Children",
                DefaultOptions);

            count = (await ExecuteQuery(query)).First();
            Assert("Expected 3 children", count == 3);

            // LINQ
            count = await client.CreateDocumentQuery<Family>(collectionUri, DefaultOptions)
                .SelectMany(f => f.Children)
                .CountAsync();

            Assert("Expected 3 children", count == 3);

            // SQL over an array within documents
            var maxGradeQuery = client.CreateDocumentQuery<int>(
                collectionUri,
                "SELECT VALUE MAX(child.Grade) FROM child IN f.Children",
                DefaultOptions);

            int maxGrade = (await ExecuteQuery(maxGradeQuery)).First();
            Assert("Expected 8th grade", maxGrade == 8);

            maxGrade = await client.CreateDocumentQuery<Family>(collectionUri, DefaultOptions)
                .SelectMany(f => f.Children)
                .Select(c => c.Grade)
                .MaxAsync();

            Assert("Expected 8th grade", maxGrade == 8);
        }

        private static async Task QueryWithSubdocuments(Uri collectionUri)
        {
            // DocumentDB supports the selection of sub-documents on the server, there
            // is no need to send down the full family record if all you want to display
            // is a single child

            // SQL
            var query = client.CreateDocumentQuery<Child>(
                collectionUri,
                "SELECT VALUE c FROM c IN f.Children",
                DefaultOptions);

            var childrenResult = await ExecuteQuery(query);
            foreach (var child in childrenResult)
            {
                Console.WriteLine(JsonConvert.SerializeObject(child));
            }

            // LINQ Query
            query = client.CreateDocumentQuery<Family>(collectionUri, DefaultOptions)
                     .SelectMany(family => family.Children
                     .Select(c => c));

            childrenResult = await ExecuteQuery(query);
            foreach (var child in childrenResult)
            {
                Console.WriteLine(JsonConvert.SerializeObject(child));
            }
        }

        private static async Task QueryWithJoins(Uri collectionUri)
        {
            // DocumentDB supports the notion of a Intradocument Join, or a self-join
            // which will effectively flatten the hierarchy of a document, just like doing 
            // a self JOIN on a SQL table

            // Below are three queries involving JOIN, shown in SQL and in LINQ, each produces the exact same result set
            await QueryWithSingleJoin(collectionUri);

            //now lets add a second level by joining the pets on to children which is joined to family
            await QueryWithTwoJoins(collectionUri);

            // Now let's add a filter to our JOIN query
            await QueryWithTwoJoinsAndFilter(collectionUri);
        }

        private static async Task QueryWithTwoJoinsAndFilter(Uri collectionUri)
        {
            var query = client.CreateDocumentQuery<dynamic>(collectionUri,
                    "SELECT f.id as family, c.FirstName AS child, p.GivenName AS pet " +
                    "FROM Families f " +
                    "JOIN c IN f.Children " +
                    "JOIN p IN c.Pets " +
                    "WHERE p.GivenName = 'Fluffy'",
                    DefaultOptions);

            var items = await ExecuteQuery(query);
            foreach (var item in items)
            {
                Console.WriteLine(item);
            }

            // LINQ
            var familiesChildrenAndPetsQuery = client.CreateDocumentQuery<Family>(collectionUri, DefaultOptions)
                    .SelectMany(family => family.Children
                    .SelectMany(child => child.Pets
                    .Where(pet => pet.GivenName == "Fluffy")
                    .Select(pet => new
                    {
                        family = family.Id,
                        child = child.FirstName,
                        pet = pet.GivenName
                    }
                    ))).AsDocumentQuery();

            while (familiesChildrenAndPetsQuery.HasMoreResults)
            {
                var familiesChildrenAndPets = await familiesChildrenAndPetsQuery.ExecuteNextAsync();
                foreach (var pet in familiesChildrenAndPets)
                {
                    Console.WriteLine(pet);
                }
            }
        }

        private static async Task QueryWithTwoJoins(Uri collectionUri)
        {
            // SQL
            var familiesChildrenAndPetsQuery = client.CreateDocumentQuery<dynamic>(
                collectionUri,
                "SELECT f.id as family, c.FirstName AS child, p.GivenName AS pet " +
                "FROM Families f " +
                "JOIN c IN f.Children " +
                "JOIN p IN c.Pets ",
                DefaultOptions);

            var familiesChildrenAndPets = await ExecuteQuery(familiesChildrenAndPetsQuery);
            foreach (var item in familiesChildrenAndPets)
            {
                Console.WriteLine(item);
            }

            // LINQ
            var query = client.CreateDocumentQuery<Family>(collectionUri, DefaultOptions)
                    .SelectMany(family => family.Children
                    .SelectMany(child => child.Pets
                    .Select(pet => new
                    {
                        family = family.Id,
                        child = child.FirstName,
                        pet = pet.GivenName
                    }
                    ))).AsDocumentQuery();

            while (query.HasMoreResults)
            {
                var items = await query.ExecuteNextAsync();
                foreach (var item in items)
                {
                    Console.WriteLine(item);
                }
            }

        }

        private static async Task QueryWithSingleJoin(Uri collectionUri)
        {

            // SQL
            var query = client.CreateDocumentQuery(collectionUri,
                "SELECT f.id " +
                "FROM Families f " +
                "JOIN c IN f.Children", DefaultOptions);

            var items = await ExecuteQuery(query);
            foreach (var item in items)
            {
                Console.WriteLine(JsonConvert.SerializeObject(item));
            }

            // LINQ
            var familiesAndChildrenQuery = client.CreateDocumentQuery<Family>(collectionUri, DefaultOptions)
                    .SelectMany(family => family.Children
                    .Select(c => family.Id));

            var familiesAndChildren = await ExecuteQuery(familiesAndChildrenQuery);
            foreach (var item in familiesAndChildren)
            {
                Console.WriteLine(JsonConvert.SerializeObject(item));
            }
        }

        private static async Task QueryWithStringMathAndArrayOperators(Uri collectionUri)
        {
            // Find all families where the lastName starts with "An" -> should return the Andersens
            IQueryable<Family> query = client.CreateDocumentQuery<Family>(
                collectionUri,
                "SELECT * FROM family WHERE STARTSWITH(family.LastName, 'An')",
                DefaultOptions);

            var results = await ExecuteQuery(query);
            Assert("Expected only 1 family", results.Count == 1);

            // Same query in LINQ. You can also use other operators like string.Contains(), string.EndsWith(), string.Trim(), etc.
            query = client.CreateDocumentQuery<Family>(collectionUri, DefaultOptions)
                .Where(family => family.LastName.StartsWith("An"));
            results = await ExecuteQuery(query);
            Assert("Expected only 1 family", results.Count == 1);

            // Round down numbers using FLOOR
            IQueryable<int> numericQuery = client.CreateDocumentQuery<int>(
                collectionUri,
                "SELECT VALUE FLOOR(family.Children[0].Grade) FROM family",
                DefaultOptions);
            var numericResults = await ExecuteQuery(numericQuery);
            Assert("Expected grades [5, 2]", numericResults.SequenceEqual(new[] { 5, 8 }));

            // Same query in LINQ. You can also use other Math operators
            numericQuery = client.CreateDocumentQuery<Family>(collectionUri, DefaultOptions)
                .Select(family => (int)Math.Round((double)family.Children[0].Grade));
            numericResults = await ExecuteQuery(numericQuery);
            Assert("Expected grades [5, 2]", numericResults.SequenceEqual(new[] { 5, 8 }));

            // Get number of children using ARRAY_LENGTH
            numericQuery = client.CreateDocumentQuery<int>(
                collectionUri,
                "SELECT VALUE ARRAY_LENGTH(family.Children) FROM family",
                DefaultOptions);
            numericResults = await ExecuteQuery(numericQuery);
            Assert("Expected children count [1, 2]", numericResults.SequenceEqual(new[] { 1, 2 }));

            // Same query in LINQ
            numericQuery = client.CreateDocumentQuery<Family>(collectionUri, DefaultOptions)
                .Select(family => family.Children.Count());
            numericResults = await ExecuteQuery(numericQuery);
            Assert("Expected children count [1, 2]", numericResults.SequenceEqual(new[] { 1, 2 }));
        }

        private static async Task QueryWithPagingAsync(Uri collectionUri)
        {
            // The .NET client automatically iterates through all the pages of query results 
            // Developers can explicitly control paging by creating an IDocumentQueryable 
            // using the IQueryable object, then by reading the ResponseContinuationToken values 
            // and passing them back as RequestContinuationToken in FeedOptions.

            List<Family> families = new List<Family>();

            // tell server we only want 1 record
            FeedOptions options = new FeedOptions { MaxItemCount = 1, EnableCrossPartitionQuery = true };

            // using AsDocumentQuery you get access to whether or not the query HasMoreResults
            // If it does, just call ExecuteNextAsync until there are no more results
            // No need to supply a continuation token here as the server keeps track of progress
            var query = client.CreateDocumentQuery<Family>(collectionUri, options).AsDocumentQuery();
            while (query.HasMoreResults)
            {
                foreach (Family family in await query.ExecuteNextAsync())
                {
                    families.Add(family);
                }
            }

            // The above sample works fine whilst in a loop as above, but 
            // what if you load a page of 1 record and then in a different 
            // Session at a later stage want to continue from where you were?
            // well, now you need to capture the continuation token 
            // and use it on subsequent queries

            query = client.CreateDocumentQuery<Family>(
                collectionUri,
                new FeedOptions { MaxItemCount = 1, EnableCrossPartitionQuery = true }).AsDocumentQuery();

            var feedResponse = await query.ExecuteNextAsync<Family>();
            string continuation = feedResponse.ResponseContinuation;

            foreach (var f in feedResponse.AsEnumerable().OrderBy(f => f.Id))
            {
                if (f.Id != "AndersenFamily") throw new ApplicationException("Should only be the first family");
            }

            // Now the second time around use the contiuation token you got
            // and start the process from that point
            query = client.CreateDocumentQuery<Family>(
                collectionUri,
                new FeedOptions
                {
                    MaxItemCount = 1,
                    RequestContinuation = continuation,
                    EnableCrossPartitionQuery = true
                }).AsDocumentQuery();

            feedResponse = await query.ExecuteNextAsync<Family>();

            foreach (var f in feedResponse.OrderBy(f => f.Id))
            {
                if (f.Id != "WakefieldFamily") throw new ApplicationException("Should only be the second family");
            }
        }

        private static async Task QueryPartitionedCollectionInParallelAsync(Uri collectionUri)
        {
            // The .NET client automatically iterates through all the pages of query results 
            // Developers can explicitly control paging by creating an IDocumentQueryable 
            // using the IQueryable object, then by reading the ResponseContinuationToken values 
            // and passing them back as RequestContinuationToken in FeedOptions.

            List<Family> familiesSerial = new List<Family>();
            String queryText = "SELECT * FROM Families";

            // 0 maximum parallel tasks, effectively serial execution
            FeedOptions options = new FeedOptions
            {
                MaxDegreeOfParallelism = 0,
                MaxBufferedItemCount = 100,
                EnableCrossPartitionQuery = true
            };

            var query = client.CreateDocumentQuery<Family>(collectionUri, queryText, options).AsDocumentQuery();
            while (query.HasMoreResults)
            {
                FeedResponse<Family> response = await query.ExecuteNextAsync<Family>();
                familiesSerial.AddRange(response);
            }

            Assert("Parallel Query expected two families", familiesSerial.Count == 2);

            // 1 maximum parallel tasks, 1 dedicated asynchrousnous task to continuously make REST calls
            List<Family> familiesParallel1 = new List<Family>();
            options = new FeedOptions
            {
                MaxDegreeOfParallelism = 1,
                MaxBufferedItemCount = 100,
                EnableCrossPartitionQuery = true
            };

            query = client.CreateDocumentQuery<Family>(collectionUri, options).AsDocumentQuery();
            while (query.HasMoreResults)
            {
                FeedResponse<Family> response = await query.ExecuteNextAsync<Family>();
                familiesParallel1.AddRange(response);
            }

            Assert("Parallel Query expected two families", familiesParallel1.Count == 2);
            AssertSequenceEqual("Parallel query returns result out of order compared to serial execution", familiesSerial, familiesParallel1);


            // 10 maximum parallel tasks, a maximum of 10 dedicated asynchrousnous tasks to continuously make REST calls
            List<Family> familiesParallel10 = new List<Family>();
            options = new FeedOptions
            {
                MaxDegreeOfParallelism = 10,
                MaxBufferedItemCount = 100,
                EnableCrossPartitionQuery = true
            };

            query = client.CreateDocumentQuery<Family>(collectionUri, options).AsDocumentQuery();
            while (query.HasMoreResults)
            {
                FeedResponse<Family> response = await query.ExecuteNextAsync<Family>();
                familiesParallel10.AddRange(response);
            }

            Assert("Parallel Query expected two families", familiesParallel10.ToList().Count == 2);
            AssertSequenceEqual("Parallel query returns result out of order compared to serial execution", familiesSerial, familiesParallel10);
        }

        private static async Task QueryWithOrderByForPartitionedCollectionAsync(Uri collectionUri)
        {
            // The .NET client automatically iterates through all the pages of query results 
            // Developers can explicitly control paging by creating an IDocumentQueryable 
            // using the IQueryable object, then by reading the ResponseContinuationToken values 
            // and passing them back as RequestContinuationToken in FeedOptions.

            List<Family> familiesSerial = new List<Family>();
            String queryText = "SELECT * FROM Families order by Families.LastName";

            // 0 maximum parallel tasks, effectively serial execution
            FeedOptions options = new FeedOptions
            {
                MaxDegreeOfParallelism = 0,
                MaxBufferedItemCount = 100,
                EnableCrossPartitionQuery = true
            };

            IDocumentQuery<Family> query = client.CreateDocumentQuery<Family>(collectionUri, queryText, options).AsDocumentQuery();
            while (query.HasMoreResults)
            {
                FeedResponse<Family> response = await query.ExecuteNextAsync<Family>();
                familiesSerial.AddRange(response);
            }

            Assert("Order By Query expected two families", familiesSerial.ToList().Count == 2);

            // 1 maximum parallel tasks, 1 dedicated asynchrousnous task to continuously make REST calls
            List<Family> familiesParallel1 = new List<Family>();
            options = new FeedOptions
            {
                MaxDegreeOfParallelism = 1,
                MaxBufferedItemCount = 100,
                EnableCrossPartitionQuery = true
            };

            // using AsDocumentQuery you get access to whether or not the query HasMoreResults
            // If it does, just call ExecuteNextAsync until there are no more results
            // No need to supply a continuation token here as the server keeps track of progress
            query = client.CreateDocumentQuery<Family>(collectionUri, queryText, options).AsDocumentQuery();
            while (query.HasMoreResults)
            {
                FeedResponse<Family> response = await query.ExecuteNextAsync<Family>();
                familiesParallel1.AddRange(response);
            }

            Assert("Order By Query expected two families", familiesParallel1.ToList().Count == 2);
            AssertSequenceEqual("Parallel query returns result out of order compared to serial execution", familiesSerial, familiesParallel1);

            // 10 maximum parallel tasks, a maximum of 10 dedicated asynchrousnous tasks to continuously make REST calls
            List<Family> familiesParallel10 = new List<Family>();
            options = new FeedOptions
            {
                MaxDegreeOfParallelism = 10,
                MaxBufferedItemCount = 100,
                EnableCrossPartitionQuery = true
            };

            query = client.CreateDocumentQuery<Family>(collectionUri, queryText, options).AsDocumentQuery();
            while (query.HasMoreResults)
            {
                FeedResponse<Family> response = await query.ExecuteNextAsync<Family>();
                familiesParallel10.AddRange(response);
            }

            Assert("Order By Query expected two families", familiesParallel10.ToList().Count == 2);
            AssertSequenceEqual("Parallel query returns result out of order compared to serial execution", familiesSerial, familiesParallel10);
        }

        /// <summary>
        /// Helper function to cast it from IQueryable to IDocumentQuery
        /// </summary>
        private static Task<IList<T>> ExecuteQuery<T>(IQueryable<T> query)
        {
            return ExecuteQuery<T>(query.AsDocumentQuery<T>());
        }

        /// <summary>
        /// Helper function to fully drain the query in asynchonous way.
        /// </summary>
        /// <remarks>
        /// Do NOT use ToList() and/or AsEnumerable(). These are blocking calls which
        /// can lead to dead locks and latency issues.
        /// </remarks>
        /// <returns></returns>
        private static async Task<IList<T>> ExecuteQuery<T>(IDocumentQuery<T> query)
        {
            using (query)
            {
                List<T> results = new List<T>();
                while (query.HasMoreResults)
                {
                    FeedResponse<T> queryResult = await query.ExecuteNextAsync<T>();
                    results.AddRange(queryResult);
                }

                return results;
            }
        }

        /// <summary>
        /// Creates the documents used in this Sample
        /// </summary>
        /// <param name="collectionUri">The selfLink property for the DocumentCollection where documents will be created.</param>
        /// <returns>None</returns>
        private static async Task CreateDocuments(Uri collectionUri)
        {
            Family AndersonFamily = new Family
            {
                Id = "AndersenFamily",
                LastName = "Andersen",
                Parents = new Parent[]
                {
                    new Parent { FirstName = "Thomas" },
                    new Parent { FirstName = "Mary Kay"}
                },
                Children = new Child[]
                {
                    new Child
                    {
                        FirstName = "Henriette Thaulow",
                        Gender = "female",
                        Grade = 5,
                        Pets = new []
                        {
                            new Pet { GivenName = "Fluffy" }
                        }
                    }
                },
                Address = new Address { State = "WA", County = "King", City = "Seattle" },
                IsRegistered = true,
                RegistrationDate = DateTime.UtcNow.AddDays(-1)
            };

            await client.UpsertDocumentAsync(collectionUri, AndersonFamily);

            Family WakefieldFamily = new Family
            {
                Id = "WakefieldFamily",
                LastName = "Wakefield",
                Parents = new[] {
                    new Parent { FamilyName= "Wakefield", FirstName= "Robin" },
                    new Parent { FamilyName= "Miller", FirstName= "Ben" }
                },
                Children = new Child[] {
                    new Child
                    {
                        FamilyName= "Merriam",
                        FirstName= "Jesse",
                        Gender= "female",
                        Grade= 8,
                        Pets= new Pet[] {
                            new Pet { GivenName= "Goofy" },
                            new Pet { GivenName= "Shadow" }
                        }
                    },
                    new Child
                    {
                        FirstName= "Lisa",
                        Gender= "female",
                        Grade= 1
                    }
                },
                Address = new Address { State = "NY", County = "Manhattan", City = "NY" },
                IsRegistered = false,
                RegistrationDate = DateTime.UtcNow.AddDays(-30)
            };

            await client.UpsertDocumentAsync(collectionUri, WakefieldFamily);
        }

        /// <summary>
        /// Get a DocuemntCollection by id, or create a new one if one with the id provided doesn't exist.
        /// </summary>
        /// <param name="id">The id of the DocumentCollection to search for, or create.</param>
        /// <returns>The matched, or created, DocumentCollection object</returns>
        private static async Task<DocumentCollection> GetOrCreateCollectionAsync(string databaseId, string collectionId)
        {
            DocumentCollection collectionDefinition = new DocumentCollection();
            collectionDefinition.Id = collectionId;
            collectionDefinition.IndexingPolicy = new IndexingPolicy(new RangeIndex(DataType.String) { Precision = -1 });
            collectionDefinition.PartitionKey.Paths.Add("/LastName");

            return await client.CreateDocumentCollectionIfNotExistsAsync(
                UriFactory.CreateDatabaseUri(databaseId),
                collectionDefinition,
                new RequestOptions { OfferThroughput = 400 });
        }

        /// <summary>
        /// Log exception error message to the console
        /// </summary>
        /// <param name="e">The caught exception.</param>
        private static void LogException(Exception e)
        {
            ConsoleColor color = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;

            Exception baseException = e.GetBaseException();
            if (e is DocumentClientException)
            {
                DocumentClientException de = (DocumentClientException)e;
                Console.WriteLine("{0} error occurred: {1}, Message: {2}", de.StatusCode, de.Message, baseException.Message);
            }
            else
            {
                Console.WriteLine("Error: {0}, Message: {1}", e.Message, baseException.Message);
            }

            Console.ForegroundColor = color;
        }

        private static void Assert(string message, bool condition)
        {
            if (!condition)
            {
                throw new ApplicationException(message);
            }
        }

        private static void AssertSequenceEqual(string message, List<Family> list1, List<Family> list2)
        {
            if (!string.Join(",", list1.Select(family => family.Id).ToArray()).Equals(
                string.Join(",", list1.Select(family => family.Id).ToArray())))
            {
                throw new ApplicationException(message);
            }
        }

        internal sealed class Parent
        {
            public string FamilyName { get; set; }
            public string FirstName { get; set; }
        }

        internal sealed class Child
        {
            public string FamilyName { get; set; }
            public string FirstName { get; set; }
            public string Gender { get; set; }
            public int Grade { get; set; }
            public Pet[] Pets { get; set; }
        }

        internal sealed class Pet
        {
            public string GivenName { get; set; }
        }

        internal sealed class Address
        {
            public string State { get; set; }
            public string County { get; set; }
            public string City { get; set; }
        }

        internal sealed class Family
        {
            [JsonProperty(PropertyName = "id")]
            public string Id { get; set; }

            public string LastName { get; set; }

            public Parent[] Parents { get; set; }

            public Child[] Children { get; set; }

            public Address Address { get; set; }

            public bool IsRegistered { get; set; }

            public DateTime RegistrationDate { get; set; }
        }
    }
}
