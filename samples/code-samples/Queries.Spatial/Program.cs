namespace DocumentDB.Samples.Queries.Spatial
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Documents.Linq;
    using Microsoft.Azure.Documents.Spatial;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;
    using DocumentDB.Samples.Shared;
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
            Database database = await GetDatabaseAsync(databaseId);

            //Get, or Create, the Document Collection
            DocumentCollection collection = await GetCollectionAsync(database.SelfLink, collectionId);

            Console.WriteLine("Cleaning up");
            foreach (Document d in await client.ReadDocumentFeedAsync(collection.SelfLink))
            {
                await client.DeleteDocumentAsync(d.SelfLink);
            }

            // You are in Africa and you want to catch a Lion!

            Console.WriteLine("Inserting some spatial data");
            Animal you = new Animal { Name = "you", Species = "Human", Location = new Point(31.9, -4.8) };
            Animal lion1 = new Animal { Name = "lion1", Species = "Lion", Location = new Point(31.87, -4.55) };
            Animal lion2 = new Animal { Name = "lion2", Species = "Lion", Location = new Point(32.33, -4.66) };

            // Insert them.
            await client.CreateDocumentAsync(collection.SelfLink, you);
            await client.CreateDocumentAsync(collection.SelfLink, lion1);
            await client.CreateDocumentAsync(collection.SelfLink, lion2);

            // You have gas to travel only 30 km, so you want to find a lion within this radius.
            Console.WriteLine("Performing a DISTANCE proximity query in SQL");
            foreach (Animal animal in client.CreateDocumentQuery<Animal>(collection.SelfLink,
                "SELECT * FROM everything e WHERE e.species ='Lion' AND ST_DISTANCE(e.location, {'type': 'Point', 'coordinates':[31.9, -4.8]}) < 30000"))
            {
                Console.WriteLine("\t" + animal);
            }

            Console.WriteLine();

            Console.WriteLine("Performing a DISTANCE proximity query in LINQ");
            foreach (Animal animal in client.CreateDocumentQuery<Animal>(collection.SelfLink).Where(a => a.Species == "Lion" && a.Location.Distance(you.Location) < 30000))
            {
                Console.WriteLine("\t" + animal);
            }

            Console.WriteLine();

            Console.WriteLine("Performing a DISTANCE proximity query in parameterized SQL");
            foreach (Animal animal in client.CreateDocumentQuery<Animal>(collection.SelfLink,
                new SqlQuerySpec 
                { 
                    QueryText = "SELECT * FROM everything e WHERE e.species ='Lion' AND ST_DISTANCE(e.location, @me) < 30000", 
                    Parameters = new SqlParameterCollection(new [] { new SqlParameter { Name = "@me", Value = you.Location }}) 
                }))
            {
                Console.WriteLine("\t" + animal);
            }

            Console.WriteLine();

            // But your car breaks, what to do? You put yourself in a rectangular cage:
            Console.WriteLine("Performing a WITHIN proximity query in SQL");

            foreach (Animal animal in client.CreateDocumentQuery<Animal>(collection.SelfLink,
                "SELECT * FROM everything e WHERE ST_WITHIN(e.location, {'type':'Polygon', 'coordinates': [[[31.8, -5], [32, -5], [32, -4.7], [31.8, -4.7], [31.8, -5]]]})"))
            {
                Console.WriteLine("\t" + animal);
            }

            Console.WriteLine();
            Console.WriteLine("Performing a WITHIN proximity query in LINQ");

            foreach (Animal animal in client.CreateDocumentQuery<Animal>(collection.SelfLink).Where(a => a.Location.Within(
                new Polygon(
                    new[] 
                    { 
                        new LinearRing(new [] { 
                            new Position(31.8, -5),
                            new Position(32, -5),
                            new Position(32, -4.7),
                            new Position(31.8, -4.7),
                            new Position(31.8, -5)
                        })
                    }))))
            {
                Console.WriteLine("\t" + animal);
            }

            Console.WriteLine();

            // And then you invert the space and now all lions are in the cage and you are free!
            Console.WriteLine("Performing a WITHIN (inverse) proximity query in SQL");

            foreach (Animal animal in client.CreateDocumentQuery<Animal>(collection.SelfLink,
                "SELECT * FROM everything e WHERE ST_WITHIN(e.location, {'type':'Polygon', 'coordinates': [[[31.8, -5], [31.8, -4.7], [32, -4.7],  [32, -5], [31.8, -5]]]})"))
            {
                Console.WriteLine("\t" + animal);
            }

            Console.WriteLine();
            Console.WriteLine("Performing a WITHIN (inverse) proximity query in LINQ");

            foreach (Animal animal in client.CreateDocumentQuery<Animal>(collection.SelfLink).Where(a => a.Location.Within(
                new Polygon(
                    new [] 
                    { 
                        new LinearRing(new [] { 
                            new Position(31.8, -5),
                            new Position(31.8, -4.7),
                            new Position(32, -4.7),
                            new Position(32, -5),
                            new Position(31.8, -5)
                        })
                    }))))
            {
                Console.WriteLine("\t" + animal);
            }

            Console.WriteLine();

            Console.WriteLine("Checking if a point is valid ...");
            dynamic result = client.CreateDocumentQuery(collection.SelfLink, 
                new SqlQuerySpec 
                {
                    QueryText = "SELECT ST_ISVALID(@point), ST_ISVALIDDETAILED(@point)", 
                    Parameters = new SqlParameterCollection(new [] { new SqlParameter { Name = "@point", Value = new Point(31.9, -132.8) } })
                }).AsDocumentQuery().ExecuteNextAsync().Result.First();

            Console.WriteLine(JsonConvert.SerializeObject(result, Formatting.None));

            Console.WriteLine("Checking if a polygon is valid ...");
            result = client.CreateDocumentQuery(collection.SelfLink, 
                new SqlQuerySpec 
                {
                    QueryText = "SELECT ST_ISVALID(@polygon), ST_ISVALIDDETAILED(@polygon)",
                    Parameters = new SqlParameterCollection(new[] { new SqlParameter { 
                        Name = "@polygon", 
                        Value = new Polygon(new [] { new LinearRing(new [] { new Position(31.8, -5), new Position(32, -5), new Position(32, -4.7), new Position(31.8, -4.7) })}) 
                    }})
                }).AsDocumentQuery().ExecuteNextAsync().Result.First();

            Console.WriteLine(JsonConvert.SerializeObject(result, Formatting.None));

        }

        class Animal
        {
            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("species")]
            public string Species { get; set; }

            [JsonProperty("location")]
            public Point Location { get; set; }

            public override string ToString()
            {
                return JsonConvert.SerializeObject(this, Formatting.None);
            }
        }

        /// <summary>
        /// Get a Database for this id. Delete if it already exists.
        /// </summary>
        /// <param id="id">The id of the Database to create.</param>
        /// <returns>The created Database object</returns>
        private static async Task<Database> GetDatabaseAsync(string id)
        {
            Database database = client.CreateDatabaseQuery().Where(c => c.Id == id).ToArray().FirstOrDefault();
            if (database != null)
            {
                return database;
            }

            Console.WriteLine("Creating new database...");
            database = await client.CreateDatabaseAsync(new Database { Id = id });
            return database;
        }

        /// <summary>
        /// Get a DocumentCollection by id, or create a new one if one with the id provided doesn't exist. 
        /// If it exists, update the indexing policy to use string range & spatial indexes.
        /// </summary>
        /// <param name="id">The id of the DocumentCollection to search for, or create.</param>
        /// <returns>The matched, or created, DocumentCollection object</returns>
        private static async Task<DocumentCollection> GetCollectionAsync(string dbLink, string id)
        {
            DocumentCollection collection = client.CreateDocumentCollectionQuery(dbLink).Where(c => c.Id == id).ToArray().FirstOrDefault();

            // Include spatial indexing. If you don't have spatial indexing, you can still run geospatial queries by including RequestOptions.EnableScanInQuery
            IndexingPolicy optimalQueriesIndexingPolicy = new IndexingPolicy();
            optimalQueriesIndexingPolicy.IncludedPaths.Add(new IncludedPath
            {
                Path = "/*",
                Indexes = new System.Collections.ObjectModel.Collection<Index>()
                    {
                        new RangeIndex(DataType.Number) { Precision = -1 },
                        new RangeIndex(DataType.String) { Precision = -1 },
                        new SpatialIndex(DataType.Point)
                    }
            });

            if (collection == null)
            {
                DocumentCollection collectionDefinition = new DocumentCollection { Id = id };
                collectionDefinition.IndexingPolicy = optimalQueriesIndexingPolicy;

                Console.WriteLine("Creating new collection...");
                collection = await client.CreateDocumentCollectionAsync(dbLink, collectionDefinition);
            }
            else
            {
                collection.IndexingPolicy = optimalQueriesIndexingPolicy;

                Console.WriteLine("Updating collection with spatial indexing enabled in indexing policy...");
                await client.ReplaceDocumentCollectionAsync(collection);

                Console.WriteLine("waiting for indexing to complete...");
                long indexTransformationProgress = 0;
                while (indexTransformationProgress < 100)
                {
                    ResourceResponse<DocumentCollection> response = await client.ReadDocumentCollectionAsync(collection.SelfLink);
                    indexTransformationProgress = response.IndexTransformationProgress;

                    await Task.Delay(TimeSpan.FromSeconds(1));
                }
            }

            return collection;
        }
    }
}