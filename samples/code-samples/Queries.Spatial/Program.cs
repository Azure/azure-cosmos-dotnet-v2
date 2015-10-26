﻿namespace DocumentDB.Samples.Queries.Spatial
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using DocumentDB.Samples.Shared;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Documents.Linq;
    using Microsoft.Azure.Documents.Spatial;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    /// <summary>
    /// This sample demonstrates the use of geospatial indexing and querying with Azure DocumentDB. We 
    /// look at how to store Points using the classes in the Microsoft.Azure.Documents.Spatial namespace,
    /// how to enable a collection for geospatial indexing, and how to query for WITHIN and DISTANCE 
    /// using SQL and LINQ.
    /// </summary>
    public class Program
    {
        /// <summary>
        /// Gets the database ID to use for the demo.
        /// </summary>
        private static readonly string DatabaseId = ConfigurationManager.AppSettings["DatabaseId"];

        /// <summary>
        /// Gets the collection ID to use for the demo.
        /// </summary>
        private static readonly string CollectionId = ConfigurationManager.AppSettings["CollectionId"];

        /// <summary>
        /// Gets the DocumentDB endpoint to use for the demo.
        /// </summary>
        private static readonly string EndpointUrl = ConfigurationManager.AppSettings["EndPointUrl"];

        /// <summary>
        /// Gets the DocumentDB authorization key to use for the demo.
        /// </summary>
        private static readonly string AuthorizationKey = ConfigurationManager.AppSettings["AuthorizationKey"];

        /// <summary>
        /// Gets an indexing policy with spatial enabled. You can also configure just certain paths for spatial indexing, e.g. Path = "/location/?"
        /// </summary>
        private static readonly IndexingPolicy IndexingPolicyWithSpatialEnabled = new IndexingPolicy
        {
            IncludedPaths = new System.Collections.ObjectModel.Collection<IncludedPath>()
            {
                new IncludedPath 
                {
                    Path = "/*",
                    Indexes = new System.Collections.ObjectModel.Collection<Index>()
                    {
                        new SpatialIndex(DataType.Point),
                        new RangeIndex(DataType.Number) { Precision = -1 },
                        new RangeIndex(DataType.String) { Precision = -1 }
                    }
                }
            }
        };

        /// <summary>
        /// Gets the client to use.
        /// </summary>
        private static DocumentClient client;

        /// <summary>
        /// The main method to use.
        /// </summary>
        /// <param name="args">The command line arguments.</param>
        public static void Main(string[] args)
        {
            try
            {
                // Get a Document client
                using (client = new DocumentClient(new Uri(EndpointUrl), AuthorizationKey))
                {
                    RunDemoAsync(DatabaseId, CollectionId).Wait();
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

        /// <summary>
        /// Run the geospatial demo.
        /// </summary>
        /// <param name="databaseId">The database Id.</param>
        /// <param name="collectionId">The collection Id.</param>
        /// <returns>The Task for asynchronous execution.</returns>
        private static async Task RunDemoAsync(string databaseId, string collectionId)
        {
            Database database = await GetDatabaseAsync(databaseId);

            // Create a new collection, or modify an existing one to enable spatial indexing.
            DocumentCollection collection = await GetCollectionWithSpatialIndexingAsync(database.SelfLink, collectionId);

            await Cleanup(collection);

            // NOTE: In GeoJSON, longitude comes before latitude.
            // DocumentDB uses the WGS-84 coordinate reference standard. Longitudes are between -180 and 180 degrees, and latitudes between -90 and 90 degrees.
            Console.WriteLine("Inserting some spatial data");
            Animal you = new Animal { Name = "you", Species = "Human", Location = new Point(31.9, -4.8) };
            Animal dragon1 = new Animal { Name = "dragon1", Species = "Dragon", Location = new Point(31.87, -4.55) };
            Animal dragon2 = new Animal { Name = "dragon2", Species = "Dragon", Location = new Point(32.33, -4.66) };

            // Insert documents with GeoJSON spatial data.
            await client.CreateDocumentAsync(collection.SelfLink, you);
            await client.CreateDocumentAsync(collection.SelfLink, dragon1);
            await client.CreateDocumentAsync(collection.SelfLink, dragon2);

            // Check for points within a circle/radius relative to another point. Common for "What's near me?" queries.
            RunDistanceQuery(collection, you.Location);

            // Check for points within a polygon. Cities/states/natural formations are all commonly represented as polygons.
            RunWithinPolygonQuery(collection);

            // How to check for valid geospatial objects. Checks for valid latitude/longtiudes and if polygons are well-formed, etc.
            CheckIfPointOrPolygonIsValid(collection);
        }

        /// <summary>
        /// Cleanup data from previous runs.
        /// </summary>
        /// <param name="collection">The DocumentDB collection.</param>
        /// <returns>The Task for asynchronous execution.</returns>
        private static async Task Cleanup(DocumentCollection collection)
        {
            Console.WriteLine("Cleaning up");
            foreach (Document d in await client.ReadDocumentFeedAsync(collection.SelfLink))
            {
                await client.DeleteDocumentAsync(d.SelfLink);
            }
        }

        /// <summary>
        /// Run a distance query using SQL, LINQ and parameterized SQL.
        /// </summary>
        /// <param name="collection">The DocumentDB collection.</param>
        /// <param name="from">The position to measure distance from.</param>
        private static void RunDistanceQuery(DocumentCollection collection, Point from)
        {
            Console.WriteLine("Performing a ST_DISTANCE proximity query in SQL");

            // DocumentDB uses the WGS-84 coordinate reference system (CRS). In this reference system, distance is measured in meters. So 30km = 3000m.
            // There are several built-in SQL functions that follow the OGC naming standards and start with the "ST_" prefix for "spatial type".
            foreach (Animal animal in client.CreateDocumentQuery<Animal>(
                collection.SelfLink,
                "SELECT * FROM everything e WHERE e.species ='Dragon' AND ST_DISTANCE(e.location, {'type': 'Point', 'coordinates':[31.9, -4.8]}) < 30000"))
            {
                Console.WriteLine("\t" + animal);
            }

            Console.WriteLine();

            // Geometry.Distance is a stub method in the DocumentDB SDK that can be used within LINQ expressions to build spatial queries.
            Console.WriteLine("Performing a ST_DISTANCE proximity query in LINQ");
            foreach (Animal animal in client.CreateDocumentQuery<Animal>(collection.SelfLink).Where(a => a.Species == "Dragon" && a.Location.Distance(from) < 30000))
            {
                Console.WriteLine("\t" + animal);
            }

            Console.WriteLine();

            Console.WriteLine("Performing a ST_DISTANCE proximity query in parameterized SQL");
            foreach (Animal animal in client.CreateDocumentQuery<Animal>(
                collection.SelfLink,
                new SqlQuerySpec
                {
                    QueryText = "SELECT * FROM everything e WHERE e.species ='Dragon' AND ST_DISTANCE(e.location, @me) < 30000",
                    Parameters = new SqlParameterCollection(new[] { new SqlParameter { Name = "@me", Value = from } })
                }))
            {
                Console.WriteLine("\t" + animal);
            }

            Console.WriteLine();
        }

        /// <summary>
        /// Run a within query (get points within a box/polygon) using SQL and LINQ.
        /// </summary>
        /// <param name="collection">The DocumentDB collection.</param>
        private static void RunWithinPolygonQuery(DocumentCollection collection)
        {
            Console.WriteLine("Performing a ST_WITHIN proximity query in SQL");

            foreach (Animal animal in client.CreateDocumentQuery<Animal>(
                collection.SelfLink,
                "SELECT * FROM everything e WHERE ST_WITHIN(e.location, {'type':'Polygon', 'coordinates': [[[31.8, -5], [32, -5], [32, -4.7], [31.8, -4.7], [31.8, -5]]]})"))
            {
                Console.WriteLine("\t" + animal);
            }

            Console.WriteLine();
            Console.WriteLine("Performing a ST_WITHIN proximity query in LINQ");

            foreach (Animal animal in client.CreateDocumentQuery<Animal>(collection.SelfLink)
                .Where(a => a.Location.Within(new Polygon(new[] { new LinearRing(new[] { new Position(31.8, -5), new Position(32, -5), new Position(32, -4.7), new Position(31.8, -4.7), new Position(31.8, -5) }) }))))
            {
                Console.WriteLine("\t" + animal);
            }

            Console.WriteLine();
        }

        /// <summary>
        /// Check if a point or polygon is valid using built-in functions. An important thing to note is that since DocumentDB's query is designed to handle heterogeneous types, 
        /// bad input parameters will evaluate to "undefined" and get skipped over instead of returning an error. For debugging and fixing malformed geospatial objects, please 
        /// use the built-in functions shown below.
        /// </summary>
        /// <param name="collection">The DocumentDB collection.</param>
        private static void CheckIfPointOrPolygonIsValid(DocumentCollection collection)
        {
            Console.WriteLine("Checking if a point is valid ...");

            // Here we pass a latitude that's invalid (they can be only between -90 and 90 degrees).
            QueryScalar(
                collection.SelfLink,
                new SqlQuerySpec
                {
                    QueryText = "SELECT ST_ISVALID(@point), ST_ISVALIDDETAILED(@point)",
                    Parameters = new SqlParameterCollection(new[] { new SqlParameter { Name = "@point", Value = new Point(31.9, -132.8) } })
                });

            // Here we pass a polygon that's not closed. GeoJSON and DocumentDB require that polygons must include the first point repeated at the end.
            // DocumentDB does not support polygons with holes within queries, so a polygon used in a query must have only a single LinearRing.
            Console.WriteLine("Checking if a polygon is valid ...");
            QueryScalar(
                collection.SelfLink,
                new SqlQuerySpec
                {
                    QueryText = "SELECT ST_ISVALID(@polygon), ST_ISVALIDDETAILED(@polygon)",
                    Parameters = new SqlParameterCollection(new[] 
                     { 
                         new SqlParameter 
                         { 
                             Name = "@polygon", 
                             Value = new Polygon(new[] 
                             {
                                 new LinearRing(new[] 
                                 {
                                     new Position(31.8, -5), new Position(32, -5), new Position(32, -4.7), new Position(31.8, -4.7) 
                                 })
                             })
                         }
                     })
                });
        }

        /// <summary>
        /// Get a Database for this id. Delete if it already exists.
        /// </summary>
        /// <param name="id">The id of the Database to create.</param>
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
        /// If it exists, update the indexing policy to use string range and spatial indexes.
        /// </summary>
        /// <param name="databaseLink">The database self-link to use.</param>
        /// <param name="id">The id of the DocumentCollection to search for, or create.</param>
        /// <returns>The matched, or created, DocumentCollection object</returns>
        private static async Task<DocumentCollection> GetCollectionWithSpatialIndexingAsync(string databaseLink, string id)
        {
            DocumentCollection collection = client.CreateDocumentCollectionQuery(databaseLink).Where(c => c.Id == id).ToArray().FirstOrDefault();

            if (collection == null)
            {
                DocumentCollection collectionDefinition = new DocumentCollection { Id = id };
                collectionDefinition.IndexingPolicy = IndexingPolicyWithSpatialEnabled;

                Console.WriteLine("Creating new collection...");
                collection = await client.CreateDocumentCollectionAsync(databaseLink, collectionDefinition);
            }
            else
            {
                await ModifyCollectionWithSpatialIndexing(collection, IndexingPolicyWithSpatialEnabled);
            }

            return collection;
        }

        /// <summary>
        /// Modify a collection to use spatial indexing policy and wait for it to complete.
        /// </summary>
        /// <param name="collection">The DocumentDB collection.</param>
        /// <param name="indexingPolicy">The indexing policy to use.</param>
        /// <returns>The Task for asynchronous execution.</returns>
        private static async Task ModifyCollectionWithSpatialIndexing(DocumentCollection collection, IndexingPolicy indexingPolicy)
        {
            Console.WriteLine("Updating collection with spatial indexing enabled in indexing policy...");

            collection.IndexingPolicy = indexingPolicy;
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

        /// <summary>
        /// Run a query that returns a single document, and display it
        /// </summary>
        /// <param name="collectionLink">The collection self-link</param>
        /// <param name="query">The query to run</param>
        private static void QueryScalar(string collectionLink, SqlQuerySpec query)
        {
            dynamic result = client.CreateDocumentQuery(collectionLink, query).AsDocumentQuery().ExecuteNextAsync().Result.First();
            Console.WriteLine(JsonConvert.SerializeObject(result, Formatting.None));
        }

        /// <summary>
        /// Register a user defined function to extend geospatial functionality, e.g. introduce ST_AREA for calculating the area of a polygon.
        /// </summary>
        /// <param name="collection">The DocumentDB collection.</param>
        /// <returns>The Task for asynchronous execution.</returns>
        private static async Task RegisterAreaUserDefinedFunction(DocumentCollection collection)
        {
            string areaJavaScriptBody = File.ReadAllText(@"STArea.js");
         
            UserDefinedFunction areaUserDefinedFunction = client.CreateUserDefinedFunctionQuery(collection.SelfLink).Where(u => u.Id == "ST_AREA").AsEnumerable().FirstOrDefault();

            if (areaUserDefinedFunction == null)
            {
                await client.CreateUserDefinedFunctionAsync(
                    collection.SelfLink,
                    new UserDefinedFunction
                    {
                        Id = "ST_AREA",
                        Body = areaJavaScriptBody
                    });
            }
            else 
            {
                areaUserDefinedFunction.Body = areaJavaScriptBody;
                await client.ReplaceUserDefinedFunctionAsync(areaUserDefinedFunction);
            }
        }

        /// <summary>
        /// Describes an animal.
        /// </summary>
        internal class Animal
        {
            /// <summary>
            /// Gets or sets the name of the animal.
            /// </summary>
            [JsonProperty("name")]
            public string Name { get; set; }

            /// <summary>
            /// Gets or sets the species of the animal.
            /// </summary>
            [JsonProperty("species")]
            public string Species { get; set; }

            /// <summary>
            /// Gets or sets the location of the animal.
            /// </summary>
            [JsonProperty("location")]
            public Point Location { get; set; }

            /// <summary>
            /// Returns the JSON string representation.
            /// </summary>
            /// <returns>The string representation.</returns>
            public override string ToString()
            {
                return JsonConvert.SerializeObject(this, Formatting.None);
            }
        }
    }
}