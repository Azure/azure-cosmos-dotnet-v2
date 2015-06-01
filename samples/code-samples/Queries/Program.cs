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
    // For additional examples using the SQL query grammer refer to the SQL Query Tutorial - 
    // There is also an interactive Query Demo web application where you can try out different 
    // SQL queries - 
    //------------------------------------------------------------------------------------------------

    public class Program
    {
        private static DocumentClient client;

        //Assign a id for your database & collection 
        private static readonly string databaseId = ConfigurationManager.AppSettings["DatabaseId"];
        private static readonly string collectionId = ConfigurationManager.AppSettings["CollectionId"];

        //Read the DocumentDB endpointUrl and authorisationKeys from config
        //These values are available from the Azure Management Portal on the DocumentDB Account Blade under "Keys"
        //NB > Keep these values in a safe & secure location. Together they provide Administrative access to your DocDB account
        private static readonly string endpointUrl = ConfigurationManager.AppSettings["EndPointUrl"];
        private static readonly string authorizationKey = ConfigurationManager.AppSettings["AuthorizationKey"];

        public static void Main(string[] args)
        {
            try
            {
                //Get a Document client
                using (client = new DocumentClient(new Uri(endpointUrl), authorizationKey))
                {
                    RunDemoAsync(databaseId, collectionId).Wait();
                }
            }
            catch (DocumentClientException de)
            {
                Exception baseException = de.GetBaseException();
                Console.WriteLine("{0} error occurred: {1}, Message: {2}", de.StatusCode, de.Message, baseException.Message);
            }
            catch (Exception e)
            {
                Exception baseException = e.GetBaseException();
                Console.WriteLine("Error: {0}, Message: {1}", e.Message, baseException.Message);
            }
            finally
            {
                Console.WriteLine("End of demo, press any key to exit.");
                Console.ReadKey();
            }
        }

        private static async Task RunDemoAsync(string databaseId, string collectionId)
        {
            //Get, or Create, the Database
            Database database = await GetOrCreateDatabaseAsync(databaseId);

            //Get, or Create, the Document Collection
            DocumentCollection collection = await GetOrCreateCollectionAsync(database.SelfLink, collectionId);
            
            //Create documents needed for query samples
            await CreateDocuments(collection.SelfLink);

            //--------------------------------------------------------------------------------------------------------
            // There are three ways of writing queries in the .NET SDK for DocumentDB, 
            // using the SQL Query Grammar, using LINQ Provider with Query and with Lambda. 
            // This sample will show each query using all methods. 
            // It is entirely up to you which style of query you write as they result in exactly the same query being 
            // executed on the service. 
            // 
            // There are some occasions when one syntax has advantages over others, but it's your choice which to use when
            //--------------------------------------------------------------------------------------------------------
            
            //Querying for all documents
            QueryAllDocuments(collection.SelfLink);

            //Querying for equality using ==
            QueryWithEquality(collection.SelfLink);

            //Querying for inequality using != and NOT
            QueryWithInequality(collection.SelfLink);
            
            //Querying using range operators like >, <, >=, <=
            QueryWithRangeOperators(collection.SelfLink);

            //Work with subdocuments
            QueryWithSubdocuments(collection.SelfLink);

            //Query with Intradocument Joins
            QueryWithJoins(collection.SelfLink);

            //Query with SQlQuerySpec
            QueryWithSqlQuerySpec(collection.SelfLink);
            
            //Query with explict Paging
            await QueryWithPagingAsync(collection.SelfLink);

            //Cleanup
             await client.DeleteDatabaseAsync(database.SelfLink);
        }

        private static void QueryAllDocuments(string colSelfLink)
        {
            string message = "Expecting to see two families here because we didn't filter";

            //LINQ Query
            var families =  from f in client.CreateDocumentQuery<Family>(colSelfLink)
                            select f;

            if (families.ToList().Count != 2) throw new ApplicationException(message);

            //LINQ Lambda
            families = client.CreateDocumentQuery<Family>(colSelfLink);

            if (families.ToList().Count != 2) throw new ApplicationException(message);
            
            //SQL
            families = client.CreateDocumentQuery<Family>(colSelfLink, "SELECT * FROM Families");

            if (families.ToList().Count != 2) throw new ApplicationException(message);
        }

        private static void QueryWithSqlQuerySpec(string colSelfLink)
        {
            string message = "Expecting 1 family because we filtered out the Andersens";

            //----------------------------------------------------------
            //Simple query with a single property equality comparison
            //in SQL with SQL parameterization instead of inlining the 
            //parameter values in the query string
            //LINQ Query -- Id == "value"
            IQueryable<Family> query = client.CreateDocumentQuery<Family>(colSelfLink, new SqlQuerySpec()
                {
                    QueryText = "SELECT * FROM Families f WHERE (f.id = @id)",
                    Parameters = new SqlParameterCollection() { 
                        new SqlParameter("@id", "AndersenFamily")
                    }
                });

            if (query.AsEnumerable<Family>().ToList().Count != 1)
                throw new ApplicationException(message);

            //----------------------------------------------------------
            //Query using two properties within each document
            //WHERE Id == "" AND Address.City == ""
            //notice here how we are doing an equality comparison on the string value of City

            message = "Expecting 1 family because only the Andersens live in Seattle";

            //SQL -- Id == "value" AND City == "value"
            query = client.CreateDocumentQuery<Family>(colSelfLink, new SqlQuerySpec()
                {
                    QueryText = "SELECT * FROM Families f WHERE f.id = @id AND f.Address.City = @city",
                    Parameters = new SqlParameterCollection() {
                        new SqlParameter("@id", "AndersenFamily"), 
                        new SqlParameter("@city", "Seattle")
                    }
                });
            if (query.AsEnumerable<Family>().ToList().Count != 1)
                throw new ApplicationException(message);
        }

        private static void QueryWithEquality(string colSelfLink)
        {
            string message = "Expecting 1 family because we filtered out the Andersens";

            //----------------------------------------------------------
            //Simple query with a single property equality comparison
            //LINQ Query -- Id == "value"
            var families = from f in client.CreateDocumentQuery<Family>(colSelfLink)
                           where f.Id == "AndersenFamily"
                           select f;

            if (families.ToList().Count != 1) throw new ApplicationException(message);

            //LINQ Lambda -- Id == "value"
            families = client.CreateDocumentQuery<Family>(colSelfLink)
                       .Where(f => f.Id == "AndersenFamily");

            if (families.ToList().Count != 1) throw new ApplicationException(message);

            //SQL -- Id == "value"
            families = client.CreateDocumentQuery<Family>(colSelfLink, "SELECT * " + 
                                                                       "FROM Families f " + 
                                                                       "WHERE f.id='AndersenFamily'");

            if (families.ToList().Count != 1) throw new ApplicationException(message);
            

            //----------------------------------------------------------
            //Query using two properties within each document
            //WHERE Id == "" AND Address.City == ""
            //notice here how we are doing an equality comparison on the string value of City

            message = "Expecting 1 family because only the Andersens live in Seattle";

            //LINQ Query -- Id == "value" AND City == "value"
            families = from f in client.CreateDocumentQuery<Family>(colSelfLink)
                       where f.Id == "AndersenFamily" && f.Address.City == "Seattle"
                       select f;

            if (families.ToList().Count != 1) throw new ApplicationException(message);

            //LINQ Lambda -- Id == "value" AND City == "value"
            families = client.CreateDocumentQuery<Family>(colSelfLink)
                       .Where(f => f.Id == "AndersenFamily" && f.Address.City == "Seattle");
            
            if (families.ToList().Count != 1) throw new ApplicationException(message);

            //SQL -- Id == "value" AND City == "value"
            families = client.CreateDocumentQuery<Family>(colSelfLink, "SELECT * " + 
                                                                       "FROM Families f " + 
                                                                       "WHERE f.id='AndersenFamily' AND f.Address.City='Seattle'");

            if (families.ToList().Count != 1) throw new ApplicationException(message);

            //-------------------------------------------------------------------------
            //Query using a filter on two properties and include a custom projection
            //in to a new anonymous type

            //LINQ Query -- Id == "value" OR City == "value"
            var query = from f in client.CreateDocumentQuery<Family>(colSelfLink)
                        where f.Id == "AndersenFamily" || f.Address.City == "NY"
                        select new
                        {
                            Name = f.LastName,
                            City = f.Address.City
                        };

            foreach (var item in query.ToList())
            {
                Console.WriteLine("The {0} family live in {1}", item.Name, item.City);
            }

            //LINQ Lambda -- Id == "value" OR City == "value"
            query = client.CreateDocumentQuery<Family>(colSelfLink)
                       .Where(f => f.Id == "AndersenFamily" || f.Address.City == "NY")
                       .Select(f => new { Name = f.LastName, City = f.Address.City });

            foreach (var item in query.ToList())
            {
                Console.WriteLine("The {0} family live in {1}", item.Name, item.City);
            }

            //SQL -- Id == "value" OR City == "value"
            var q = client.CreateDocumentQuery(colSelfLink,"SELECT f.LastName AS Name, f.Address.City AS City " + 
                                                            "FROM Families f " + 
                                                            "WHERE f.id='AndersenFamily' OR f.Address.City='NY'");

            foreach (var item in q.ToList())
            {
                Console.WriteLine("The {0} family live in {1}", item.Name, item.City);
            }
        }

        private static void QueryWithInequality(string colSelfLink)
        {
            //----------------------------------------------------------
            //Simple query with a single property inequality comparison
            //LINQ Query
            var families = from f in client.CreateDocumentQuery<Family>(colSelfLink)
                           where f.Id != "AndersenFamily"
                           select f;

            if (families.ToList().Count != 1) throw new ApplicationException("Only expecting the Wakefield family");
            
            //LINQ Lambda
            families = client.CreateDocumentQuery<Family>(colSelfLink)
                       .Where(f => f.Id != "AndersenFamily");

            if (families.ToList().Count != 1) throw new ApplicationException("Only expecting the Wakefield family");
            

            //SQL - in SQL you can use <> interchangably with != for "not equals"
            families = client.CreateDocumentQuery<Family>(colSelfLink, "SELECT * FROM Families f WHERE f.id <> 'AndersenFamily'");

            if (families.ToList().Count != 1) throw new ApplicationException("Only expecting the Wakefield family");
            
            //combine equality and inequality
            families = from f in client.CreateDocumentQuery<Family>(colSelfLink)
                           where f.Id == "Wakefield" && f.Address.City != "NY"
                           select f;

            if (families.ToList().Count > 0) throw new ApplicationException("Not expecting any results");
            
            families = client.CreateDocumentQuery<Family>(colSelfLink, "SELECT * FROM Families f WHERE f.id = 'AndersenFamily' AND f.Address.City != 'NY'");

            if (families.ToList().Count != 1) throw new ApplicationException("Only expecting the Andersen family");
        }

        private static void QueryWithRangeOperators(string colSelfLink)
        {
            string message = "Expecting only Wakefield family because only they have Lisa in grade 8";

            //----------------------------------------------------------------------
            //Simple range query against a single property
            //Give me all family records, where the first child is above grade 5
            //
            //NB: notice the use of the EnableScanInQuery directive being used here to enable a range query 
            //    on a collection that only has hash indexes defined (default). This would not be the most performant
            //    way of doing range queries as scans are expensive and we therefore do not recommend wide spread use
            //    of this directive. Consider adding a Range index on paths where you will often perform Range queries
            //    For more information, please refer to the DocumentDB.Samples.IndexManagement sample project
            //    or the Index Management Documentation () 

            //LINQ Query
            var families = from f in client.CreateDocumentQuery<Family>(colSelfLink, new FeedOptions { EnableScanInQuery = true })
                           where f.Children[0].Grade > 5
                           select f;

            if (families.ToList().Count != 1) throw new ApplicationException(message);

            //LINQ Lambda
            families = client.CreateDocumentQuery<Family>(colSelfLink, new FeedOptions { EnableScanInQuery = true })
                       .Where(f => f.Children[0].Grade > 5);

            if (families.ToList().Count != 1) throw new ApplicationException(message);

            //SQL
            families = client.CreateDocumentQuery<Family>(colSelfLink, "SELECT * FROM Families f WHERE f.Children[0].Grade = 5", 
                new FeedOptions { EnableScanInQuery = true });
            
            if (families.ToList().Count != 1) throw new ApplicationException(message);
        }

        private static void QueryWithSubdocuments(string colSelfLink)
        {
            //----------------------------------------------------------------------
            //DocumentDB supports the selection of sub-documents on the server, there
            //is no need to send down the full family record if all you want to display
            //is a single child

            //SQL
            var children = client.CreateDocumentQuery<Child>(colSelfLink,
                "SELECT c " +
                "FROM c IN f.Children").ToList();

            foreach (var child in children)
            {
                Console.WriteLine(child);
            }

            //LINQ Query
           var cc = client.CreateDocumentQuery<Family>(colSelfLink)
                    .SelectMany(family => family.Children
                    .Select(c => c));

            foreach (var item in cc.ToList())
            {
                Console.WriteLine(item);
            }   
        }

        private static void QueryWithJoins(string colSelfLink)
        {
            //----------------------------------------------------------------------
            //DocumentDB supports the notion of a Intradocument Join, or a self-join
            //which will effectively flatten the hierarchy of a document, just like doing 
            //a self JOIN on a SQL table

            //Below are three queries involving JOIN, shown in SQL and in LINQ, each produces the exact same result set

            //simple query with one join
            //SQL
            var aa = client.CreateDocumentQuery(colSelfLink,
                "SELECT f.id " +
                "FROM Families f " +
                "JOIN c IN f.Children");

            foreach (var item in aa.ToList())
            {
                Console.WriteLine(item);
            }

            //LINQ
            var bb = client.CreateDocumentQuery<Family>(colSelfLink)
                    .SelectMany(family => family.Children
                    .Select(c => family.Id));

            foreach (var item in bb.ToList())
            {
                Console.WriteLine(item);
            }

            //now lets add a second level by joining the pets on to children which is joined to family
            //SQL
            var cc = client.CreateDocumentQuery<dynamic>(colSelfLink,
                "SELECT f.id, c.FirstName AS child, p.GivenName AS pet " +
                "FROM Families f " +
                "JOIN c IN f.Children " +
                "JOIN p IN c.Pets ");

            foreach (var item in cc.ToList())
            {
                Console.WriteLine(item);
            }

            //LINQ
            var dd = client.CreateDocumentQuery<Family>(colSelfLink)
                    .SelectMany(family => family.Children
                    .SelectMany(children => children.Pets
                    .Select(pets => new
                        {
                            family = family.Id,
                            child = children.FirstName,
                            pet = pets.GivenName
                        })
                    ));

            foreach (var item in dd.ToList())
            {
                Console.WriteLine(item);
            }

            //now let's add a filter to our JOIN query
            //SQL

            var ee = client.CreateDocumentQuery<dynamic>(colSelfLink,
                    "SELECT f.id, c.FirstName AS child, p.GivenName AS pet " +
                    "FROM Families f " +
                    "JOIN c IN f.Children " +
                    "JOIN p IN c.Pets " +
                    "WHERE p.GivenName = 'Fluffy'");
            
            foreach (var item in ee.ToList())
            {
                Console.WriteLine(item);
            }

            //LINQ
            var ff = client.CreateDocumentQuery<Family>(colSelfLink)
                    .SelectMany(family => family.Children
                    .SelectMany(children => children.Pets
                    .Where(pets => pets.GivenName == "Fluffy")
                    .Select(pets => new
                        {
                            family = family.Id,
                            child = children.FirstName,
                            pet = pets.GivenName
                        }
                    )));

            foreach (var item in ff.ToList())
            {
                Console.WriteLine(item);
            }
        }
        
        private static async Task QueryWithPagingAsync(string colSelfLink)
        {
            //The .NET client automatically iterates through all the pages of query results 
            //Developers can explicitly control paging by creating an IDocumentQueryable 
            //using the IQueryable object, then by reading the ResponseContinuationToken values 
            //and passing them back as RequestContinuationToken in FeedOptions.
            
            List<Family> families = new List<Family>();

            //tell server we only want 1 record
            FeedOptions options = new FeedOptions { MaxItemCount = 1 };

            //using AsDocumentQuery you get access to whether or not the query HasMoreResults
            //If it does, just call ExecuteNextAsync until there are no more results
            //No need to supply a continuation token here as the server keeps track of progress
            var query = client.CreateDocumentQuery<Family>(colSelfLink, options).AsDocumentQuery();
            while (query.HasMoreResults)
            {
                foreach (Family family in await query.ExecuteNextAsync())
                {
                    families.Add(family);
                }
            }

            //The above sample works fine whilst in a loop as above, but 
            //what if you load a page of 1 record and then in a different 
            //session at a later stage want to continue from where you were?
            //well, now you need to capture the continuation token 
            //and use it on subsequent queries
            query = client.CreateDocumentQuery<Family>(colSelfLink, new FeedOptions { MaxItemCount = 1 }).AsDocumentQuery();
            var feedResponse = await query.ExecuteNextAsync<Family>();
            string continuation = feedResponse.ResponseContinuation;
            foreach (var f in feedResponse.AsEnumerable().OrderBy(f => f.Id))
            {
                if (f.Id != "AndersenFamily") throw new ApplicationException("Should only be the first family");   
            } 

            //now the second time around use the contiuation token you got
            //and start the process from that point
            query = client.CreateDocumentQuery<Family>(colSelfLink, new FeedOptions { MaxItemCount = 1, RequestContinuation = continuation }).AsDocumentQuery();
            feedResponse = await query.ExecuteNextAsync<Family>();
            foreach (var f in feedResponse.AsEnumerable().OrderBy(f => f.Id))
            {
                if (f.Id != "WakefieldFamily") throw new ApplicationException("Should only be the second family");
            }
        }

        /// <summary>
        /// Creates the documents used in this Sample
        /// </summary>
        /// <param name="colSelfLink">The selfLink property for the DocumentCollection where documents will be created.</param>
        /// <returns>None</returns>
        private static async Task CreateDocuments(string colSelfLink)
        {
            Family AndersonFamily = new Family
            {
                Id = "AndersenFamily",
                LastName = "Andersen",
                Parents =  new Parent[] {
                    new Parent { FirstName = "Thomas" },
                    new Parent { FirstName = "Mary Kay"}
                },
                Children = new Child[] {
                    new Child
                    { 
                        FirstName = "Henriette Thaulow", 
                        Gender = "female", 
                        Grade = 5, 
                        Pets = new [] {
                            new Pet { GivenName = "Fluffy" } 
                        }
                    } 
                },
                Address = new Address { State = "WA", County = "King", City = "Seattle" },
                IsRegistered = true
            };

            await client.CreateDocumentAsync(colSelfLink, AndersonFamily);

            Family WakefieldFamily = new Family
            {
                Id = "WakefieldFamily",
                LastName = "Wakefield",
                Parents = new [] {
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
                        FamilyName= "Miller", 
                        FirstName= "Lisa", 
                        Gender= "female", 
                        Grade= 1
                    }
                },
                Address = new Address { State = "NY", County = "Manhattan", City = "NY" },
                IsRegistered = false
            };

            await client.CreateDocumentAsync(colSelfLink, WakefieldFamily);
        }

        /// <summary>
        /// Get a DocuemntCollection by id, or create a new one if one with the id provided doesn't exist.
        /// </summary>
        /// <param name="id">The id of the DocumentCollection to search for, or create.</param>
        /// <returns>The matched, or created, DocumentCollection object</returns>
        private static async Task<DocumentCollection> GetOrCreateCollectionAsync(string dbLink, string id)
        {
            DocumentCollection collection = client.CreateDocumentCollectionQuery(dbLink).Where(c => c.Id == id).ToArray().FirstOrDefault();
            if (collection == null)
            {
                collection = await client.CreateDocumentCollectionAsync(dbLink, new DocumentCollection { Id = id });
            }

            return collection;
        }

        /// <summary>
        /// Get a Database by id, or create a new one if one with the id provided doesn't exist.
        /// </summary>
        /// <param name="id">The id of the Database to search for, or create.</param>
        /// <returns>The matched, or created, Database object</returns>
        private static async Task<Database> GetOrCreateDatabaseAsync(string id)
        {
            Database database = client.CreateDatabaseQuery().Where(db => db.Id == id).ToArray().FirstOrDefault();
            if (database == null)
            {
                database = await client.CreateDatabaseAsync(new Database { Id = id });
            }

            return database;
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
            [JsonProperty(PropertyName="id")]
            public string Id { get; set; }
            public string LastName { get; set; }
            public Parent[] Parents { get; set; }
            public Child[] Children { get; set; }
            public Address Address { get; set; }
            public bool IsRegistered { get; set; }
        }
    }
}
