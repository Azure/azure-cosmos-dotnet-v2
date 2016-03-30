namespace DocumentDB.Samples.Queries
{
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Documents.Linq;
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;


    //------------------------------------------------------------------------------------------------
    // This sample demonstrates the use of DocumentDB's server side JavaScript capabilities
    // including Stored Procedures, Pre & Post Triggers and User Defined Functions
    //------------------------------------------------------------------------------------------------

    public class Program
    {
        private static DocumentClient client;

        //Assign a id for your database & collection 
        private static readonly string DatabaseName = ConfigurationManager.AppSettings["DatabaseId"];
        private static readonly string CollectionName = ConfigurationManager.AppSettings["CollectionId"];

        //Read the DocumentDB endpointUrl and authorisationKeys from config
        //These values are available from the Azure Management Portal on the DocumentDB Account Blade under "Keys"
        //NB > Keep these values in a safe & secure location. Together they provide Administrative access to your DocDB account
        private static readonly string EndpointUrl = ConfigurationManager.AppSettings["EndPointUrl"];
        private static readonly string AuthorizationKey = ConfigurationManager.AppSettings["AuthorizationKey"];

        public static void Main(string[] args)
        {
            try
            {
                using (client = new DocumentClient(new Uri(EndpointUrl), AuthorizationKey))
                {
                    RunDemoAsync(DatabaseName, CollectionName).Wait();
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
            //Get, or Create, the Database
            Database database = await GetNewDatabaseAsync(databaseId);

            //Get, or Create, the Document Collection
            DocumentCollection collection = await CreateCollectionAsync(database.SelfLink, collectionId);
            
            //Run a simple script
            await RunSimpleScript(collection.SelfLink);

            //Run Bulk Import
            await RunBulkImport(collection.SelfLink);

            //Run OrderBy
            await RunOrderBy(collection.SelfLink);

            //Run Pre-Trigger
            await RunPreTrigger(collection.SelfLink);

            //Run Post-Trigger
            await RunPostTrigger(collection.SelfLink);

            //Run UDF
            await RunUDF(collection.SelfLink);

            //Cleanup
            await client.DeleteDatabaseAsync(database.SelfLink);
        }

        /// <summary>
        /// Runs a simple script which just does a server side query
        /// </summary>
        private static async Task RunSimpleScript(string collectionLink)
        {
            // 1. Create stored procedure for script.
            string scriptFileName = @"js\SimpleScript.js";
            string scriptId = Path.GetFileNameWithoutExtension(scriptFileName);

            var sproc = new StoredProcedure
            {
                Id = scriptId,
                Body = File.ReadAllText(scriptFileName)
            };

            await TryDeleteStoredProcedure(collectionLink, sproc.Id);

            sproc = await client.CreateStoredProcedureAsync(collectionLink, sproc);

            // 2. Create a document.
            var doc = new
            {
                LastName = "Estel",
                Headquarters = "Russia",
                Locations = new[] { new { Country = "Russia", City = "Novosibirsk" } },
                Income = 50000
            };

            Document created = await client.CreateDocumentAsync(collectionLink, doc);

            // 3. Run the script. Pass "Hello, " as parameter. 
            // The script will take the 1st document and echo: Hello, <document as json>.
            var response = await client.ExecuteStoredProcedureAsync<string>(
                sproc.SelfLink, 
                new RequestOptions { PartitionKey = new PartitionKey("Estel") },
                "Hello, ");

            Console.WriteLine("Result from script: {0}\r\n", response.Response);

            await client.DeleteDocumentAsync(created.SelfLink, new RequestOptions { PartitionKey = new PartitionKey("Estel") });
        }
        
        /// <summary>
        /// Import many documents using stored procedure.
        /// </summary>
        private static async Task RunBulkImport(string collectionLink)
        {
            string inputDirectory = @".\Data\";
            string inputFileMask = "*.json";
            int maxFiles = 2000;
            int maxScriptSize = 50000;

            // 1. Get the files.
            string[] fileNames = Directory.GetFiles(inputDirectory, inputFileMask);
            DirectoryInfo di = new DirectoryInfo(inputDirectory);
            FileInfo[] fileInfos = di.GetFiles(inputFileMask);

            // 2. Prepare for import.
            int currentCount = 0;
            int fileCount = maxFiles != 0 ? Math.Min(maxFiles, fileNames.Length) : fileNames.Length;

            // 3. Create stored procedure for this script.
            string body = File.ReadAllText(@".\JS\BulkImport.js");
            StoredProcedure sproc = new StoredProcedure
            {
                Id = "BulkImport",
                Body = body
            };

            await TryDeleteStoredProcedure(collectionLink, sproc.Id);
            sproc = await client.CreateStoredProcedureAsync(collectionLink, sproc);

            // 4. Create a batch of docs (MAX is limited by request size (2M) and to script for execution.           
            // We send batches of documents to create to script.
            // Each batch size is determined by MaxScriptSize.
            // MaxScriptSize should be so that:
            // -- it fits into one request (MAX reqest size is 16Kb).
            // -- it doesn't cause the script to time out.
            // -- it is possible to experiment with MaxScriptSize to get best perf given number of throttles, etc.
            while (currentCount < fileCount)
            {
                // 5. Create args for current batch.
                //    Note that we could send a string with serialized JSON and JSON.parse it on the script side,
                //    but that would cause script to run longer. Since script has timeout, unload the script as much
                //    as we can and do the parsing by client and framework. The script will get JavaScript objects.
                string argsJson = CreateBulkInsertScriptArguments(fileNames, currentCount, fileCount, maxScriptSize);
                var args = new dynamic[] { JsonConvert.DeserializeObject<dynamic>(argsJson) };

                // 6. execute the batch.
                StoredProcedureResponse<int> scriptResult = await client.ExecuteStoredProcedureAsync<int>(
                    sproc.SelfLink,
                    new RequestOptions { PartitionKey = new PartitionKey("Andersen") },
                    args);

                // 7. Prepare for next batch.
                int currentlyInserted = scriptResult.Response;
                currentCount += currentlyInserted;
            }

            // 8. Validate
            int numDocs = 0;
            string continuation = string.Empty;            
            do
            {
                // Read document feed and count the number of documents.
                FeedResponse<dynamic> response = await client.ReadDocumentFeedAsync(collectionLink, new FeedOptions { RequestContinuation = continuation });
                numDocs += response.Count;

                // Get the continuation so that we know when to stop.
                continuation = response.ResponseContinuation;
            }
            while (!string.IsNullOrEmpty(continuation));

            Console.WriteLine("Found {0} documents in the collection. There were originally {1} files in the Data directory\r\n", numDocs, fileCount);
        }

        /// <summary>
        /// Get documents ordered by some doc property. This is done using OrderBy stored procedure.
        /// </summary>
        private static async Task RunOrderBy(string collectionLink)
        {
            // 1. Create or get the stored procedure.
            string body = File.ReadAllText(@"js\OrderBy.js");
            StoredProcedure sproc = new StoredProcedure 
            { 
                Id = "OrderBy", 
                Body = body 
            };

            await TryDeleteStoredProcedure(collectionLink, sproc.Id);
            sproc = await client.CreateStoredProcedureAsync(collectionLink, sproc);

            // 2. Prepare to run stored procedure. 
            string orderByFieldName = "FamilyId";
            var filterQuery = string.Format(CultureInfo.InvariantCulture, "SELECT r.FamilyId FROM root r WHERE r.{0} > 10", orderByFieldName);
            // Note: in order to do a range query (> 10) on this field, the collection must have a range index set for this path (see ReadOrCreateCollection).

            int? continuationToken = null;
            int batchCount = 0;
            do
            {
                // 3. Run the stored procedure.
                var response = await client.ExecuteStoredProcedureAsync<OrderByResult>(
                    sproc.SelfLink,
                    new RequestOptions { PartitionKey = new PartitionKey("Andersen") },
                    filterQuery, 
                    orderByFieldName, 
                    continuationToken);

                // 4. Process stored procedure response.
                continuationToken = response.Response.Continuation;

                Console.WriteLine("Printing documents filtered/ordered by '{0}' and ordered by '{1}', batch #{2}:", filterQuery, orderByFieldName, batchCount++);
                foreach (var doc in response.Response.Result)
                {
                    Console.WriteLine(doc.ToString());
                }
            } while (continuationToken != null);
            // 5. To take care of big response, loop until Response.continuation token is null (see OrderBy.js for details).
        }

        /// <summary>
        /// Create a pre-trigger that updates the document by the following for each doc:
        /// - Validate and canonicalize the weekday name.
        /// - Auto-create createdTime field.
        /// </summary>
        private static async Task RunPreTrigger(string collectionLink)
        {
            // 1. Create a trigger.
            string triggerId = "CanonicalizeSchedule";
            string body = File.ReadAllText(@"JS\CanonicalizeSchedule.js");
            Trigger trigger = new Trigger
            {
                Id =  triggerId,
                Body = body,
                TriggerOperation = TriggerOperation.Create,
                TriggerType = TriggerType.Pre
            };

            await TryDeleteTrigger(collectionLink, trigger.Id);
            await client.CreateTriggerAsync(collectionLink, trigger);

            // 2. Create a few documents with the trigger.
            var requestOptions = new RequestOptions { PreTriggerInclude = new List<string> { triggerId } };
            
            await client.CreateDocumentAsync(collectionLink, new 
                {
                    type = "Schedule",
                    name = "Music",
                    weekday = "mon",
                    startTime = DateTime.Parse("18:00", CultureInfo.InvariantCulture),
                    endTime = DateTime.Parse("19:00", CultureInfo.InvariantCulture)
                }, requestOptions);

            await client.CreateDocumentAsync(collectionLink, new 
                {
                    type = "Schedule",
                    name = "Judo",
                    weekday = "tues",
                    startTime = DateTime.Parse("17:30", CultureInfo.InvariantCulture),
                    endTime = DateTime.Parse("19:00", CultureInfo.InvariantCulture)
                }, requestOptions);

            await client.CreateDocumentAsync(collectionLink, new 
                {
                    type = "Schedule",
                    name = "Swimming",
                    weekday = "FRIDAY",
                    startTime = DateTime.Parse("19:00", CultureInfo.InvariantCulture),
                    endTime = DateTime.Parse("20:00", CultureInfo.InvariantCulture)
                }, requestOptions);

            // 3. Read the documents from the store. 
            var results = client.CreateDocumentQuery<Document>(
                collectionLink, 
                "SELECT * FROM root r WHERE r.type='Schedule'", 
                new FeedOptions { EnableCrossPartitionQuery = true });

            // 4. Prints the results: see what the trigger did.
            Console.WriteLine("Weekly schedule of classes:");
            foreach (var result in results)
            {
                Console.WriteLine("{0}", result);
            }
        }

        /// <summary>
        /// Create a post trigger that updates metadata: for each inserted doc it will look at doc.size
        /// and update aggregate properties: { minSize, maxSize, totalSize } in the metadata doc.
        /// In the end print to show the aggregate values of min, max, total for all docs.
        /// </summary>
        private static async Task RunPostTrigger(string collectionLink)
        {
            Random rnd = new Random();

            // 1. Create a trigger.
            string triggerPath = @"js\UpdateMetadata.js";
            string triggerId = Path.GetFileNameWithoutExtension(triggerPath);
            string triggerBody = File.ReadAllText(triggerPath);
            Trigger trigger = new Trigger
            {
                Id = Path.GetFileName(triggerId),
                Body = triggerBody,
                TriggerOperation = TriggerOperation.Create,
                TriggerType = TriggerType.Post
            };

            await TryDeleteTrigger(collectionLink, trigger.Id);
            await client.CreateTriggerAsync(collectionLink, trigger);

            // 2. Create the metadata document.
            var aggregateEntry = new LoggingAggregateEntry
            {
                Id = "Device001_metadata",
                IsMetadata = true,
                MinSize = 0,
                MaxSize = 0,
                TotalSize = 0,
                DeviceId = "Device001"
            };

            await client.CreateDocumentAsync(collectionLink, aggregateEntry); 
            
            // 3. Import a number of docs with trigger. Use client API this time, we already have sample fot using script.
            var requestOptions = new RequestOptions { PostTriggerInclude = new List<string> { triggerId } };

            for (int i=0; i < 4; i++)
            {
                await client.CreateDocumentAsync(
                    collectionLink,
                    new LoggingEntry { DeviceId = "Device001", Size = rnd.Next(1000) },
                    requestOptions);
            }

            // 4. Print aggregate info from the metadata document.
            aggregateEntry = client.CreateDocumentQuery<dynamic>(
                collectionLink, 
                "SELECT * FROM root r WHERE r.isMetadata = true",
                new FeedOptions { PartitionKey = new PartitionKey("Device001") }).AsEnumerable().First();

            Console.WriteLine("Document statistics: min size: {0}, max size: {1}, total size: {2}", 
                aggregateEntry.MinSize, 
                aggregateEntry.MaxSize, 
                aggregateEntry.TotalSize);
        }

        public class LoggingEntry
        {
            [JsonProperty("size")]
            public int Size { get; set; }

            [JsonProperty("LastName")]
            public string DeviceId { get; set; }
        }

        public class LoggingAggregateEntry
        {
            [JsonProperty("id")]
            public string Id { get; set; }

            [JsonProperty("isMetadata")]
            public bool IsMetadata { get; set; }

            [JsonProperty("minSize")]
            public int MinSize { get; set; }

            [JsonProperty("maxSize")]
            public int MaxSize { get; set; }

            [JsonProperty("totalSize")]
            public int TotalSize { get; set; }

            [JsonProperty("LastName")]
            public string DeviceId { get; set; }
        }

        private static async Task RunUDF(string collectionLink)
        {
            // 1. Create UDF.
            var udfFileName = @"JS\Tax.js";
            var udfId = Path.GetFileNameWithoutExtension(udfFileName); 
            var udf = new UserDefinedFunction
            {
                Id = udfId,
                Body = File.ReadAllText(udfFileName),
            };

            await TryDeleteUDF(collectionLink, udf.Id);
            await client.CreateUserDefinedFunctionAsync(collectionLink, udf);

            // 2. Create a few documents.
            await client.CreateDocumentAsync(collectionLink, new
            {
                type = "Company",
                name = "Zucker",
                headquarters = "Germany",
                locations = new [] 
                { 
                    new {country = "Germany", city = "Berlin"}, 
                    new {country = "Russia", city = "Novosibirsk"}
                },
                income = 50000
            });

            await client.CreateDocumentAsync(collectionLink, new
            {
                type = "Company",
                name = "Estel",
                headquarters = "Russia",
                locations = new[] 
                { 
                    new {country = "Russia", city = "Novosibirsk"}, 
                    new {country = "Germany", city = "Berlin"}
                },
                income = 70000
            });

            await client.CreateDocumentAsync(collectionLink, new
            {
                type = "Company",
                name = "Pyramid",
                headquarters = "USA",
                locations = new[] 
                { 
                    new {country = "USA", city = "Seattle"}
                },
                income = 100000
            });

            // 3. Execute a query against UDF: use UDF as part of the SELECT clause.
            var results = client.CreateDocumentQuery<dynamic>(
                collectionLink,
                "SELECT r.name AS company, udf.Tax(r) AS tax FROM root r WHERE r.type='Company'",
                new FeedOptions { EnableCrossPartitionQuery = true });

            // 4. Prints the results.
            Console.WriteLine("Tax per company:");
            foreach (var result in results)
            {
                Console.WriteLine("{0}", result);
            }
        }

        internal class OrderByResult
        {
            public Document[] Result { get; set; }
            public int? Continuation { get; set; }
        }

        /// <summary>
        /// Creates the script for insertion
        /// </summary>
        /// <param name="currentIndex">the current number of documents inserted. this marks the starting point for this script</param>
        /// <param name="maxScriptSize">the maximum number of characters that the script can have</param>
        /// <returns>Script as a string</returns>
        private static string CreateBulkInsertScriptArguments(string[] docFileNames, int currentIndex, int maxCount, int maxScriptSize)
        {
            var jsonDocumentArray = new StringBuilder();
            jsonDocumentArray.Append("[");

            if (currentIndex >= maxCount) return string.Empty;
            jsonDocumentArray.Append(File.ReadAllText(docFileNames[currentIndex]));

            int scriptCapacityRemaining = maxScriptSize;
            string separator = string.Empty;

            int i = 1;
            while (jsonDocumentArray.Length < scriptCapacityRemaining && (currentIndex + i) < maxCount)
            {
                jsonDocumentArray.Append(", " + File.ReadAllText(docFileNames[currentIndex + i]));
                i++;
            }

            jsonDocumentArray.Append("]");
            return jsonDocumentArray.ToString();
        }

        /// <summary>
        /// If a Trigger is found on the DocumentCollection for the Id supplied it is deleted
        /// </summary>
        /// <param name="collectionLink">DocumentCollection to search for the Trigger</param>
        /// <param name="triggerId">Id of the Trigger to delete</param>
        /// <returns></returns>
        private static async Task TryDeleteTrigger(string collectionLink, string triggerId)
        {
            Trigger trigger = client.CreateTriggerQuery(collectionLink).Where(t => t.Id == triggerId).AsEnumerable().FirstOrDefault();
            if (trigger != null)
            {
                await client.DeleteTriggerAsync(trigger.SelfLink);
            }
        }

        /// <summary>
        /// If a Stored Procedure is found on the DocumentCollection for the Id supplied it is deleted
        /// </summary>
        /// <param name="collectionLink">DocumentCollection to search for the Stored Procedure</param>
        /// <param name="sprocId">Id of the Stored Procedure to delete</param>
        /// <returns></returns>
        private static async Task TryDeleteStoredProcedure(string collectionLink, string sprocId)
        {
            StoredProcedure sproc = client.CreateStoredProcedureQuery(collectionLink).Where(s => s.Id == sprocId).AsEnumerable().FirstOrDefault();
            if (sproc != null)
            {
                await client.DeleteStoredProcedureAsync(sproc.SelfLink);
            }
        }
        
        /// <summary>
        /// If a UDF is found on the DocumentCollection for the Id supplied it is deleted
        /// </summary>
        /// <param name="collectionLink">DocumentCollection to search for the UDF</param>
        /// <param name="udfId">Id of the UDF to delete</param>
        /// <returns></returns>
        private static async Task TryDeleteUDF(string collectionLink, string udfId)
        {
            UserDefinedFunction udf = client.CreateUserDefinedFunctionQuery(collectionLink).Where(u => u.Id == udfId).AsEnumerable().FirstOrDefault();
            if (udf != null)
            {
                await client.DeleteUserDefinedFunctionAsync(udf.SelfLink);
            }
        }
                
        /// <summary>
        /// Get a DocumentCollection by id, or create a new one if one with the id provided doesn't exist.
        /// </summary>
        /// <param name="id">The id of the DocumentCollection to search for, or create.</param>
        /// <returns>The matched, or created, DocumentCollection object</returns>
        private static async Task<DocumentCollection> CreateCollectionAsync(string dbLink, string id)
        {
            DocumentCollection collectionDefinition = new DocumentCollection { Id = id };
            collectionDefinition.IndexingPolicy = new IndexingPolicy(new RangeIndex(DataType.String) { Precision = -1 });
            collectionDefinition.PartitionKey.Paths.Add("/LastName");

            return await client.CreateDocumentCollectionAsync(
                dbLink, 
                collectionDefinition, 
                new RequestOptions { OfferThroughput = 1000 });
        }

        /// <summary>
        /// Create a new database for this ID
        /// </summary>
        /// <param name="id">The id of the Database to search for, or create.</param>
        /// <returns>The matched, or created, Database object</returns>
        private static async Task<Database> GetNewDatabaseAsync(string id)
        {
            Database database = client.CreateDatabaseQuery().Where(db => db.Id == id).ToArray().FirstOrDefault();
            if (database != null)
            {
                database = await client.DeleteDatabaseAsync(UriFactory.CreateDatabaseUri(id));
            }

            return await client.CreateDatabaseAsync(new Database { Id = id });
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
    }
}
