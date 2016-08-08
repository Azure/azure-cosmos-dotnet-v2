using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Newtonsoft.Json;
using System.IO;

namespace DocDBClientBulk
{
    public partial class Program
    {
        const string Endpoint = "https://localhost:443/";
        const string AuthKey = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";

        const string DbName = "db";
        const string CollectionName = "c";

        private static Guid activeTransactionsDocId;
        private static Guid transactionId;

        // This console application accomplishes the following: 
        //     1. Creates and inserts a document designated for keeping track of the list of currently active transaction ids. 
        //     2. Generates a GUID (let's call this tId0 for the new transaction and adds this to the document created in step 1.
        //     3. Invokes the bulk import stored procedure. Upon insertion of each document, a new property TransactionId with value = tId0 is added to associate each processed document
        //        with the current active transaction.
        //     4. If the bulk import stored procedure completes successfully, remove tId0 from the document created in step 1.
        //     5. If the bulk import stored procedure fails to complete, clean up the processed documents with TransactionId = tId0. Do this by invoking the bulk delete stored procedure.
        //        Remove tId0 from teh document created in step 1. 
        static void Main(string[] args)
        {
            Console.WriteLine("Using {0}, {1}, {2}", Endpoint, DbName, CollectionName);

            activeTransactionsDocId = Guid.NewGuid();
            ActiveTransactions activeTransactionsDoc = new ActiveTransactions
            {
                Id = activeTransactionsDocId,
                Transactions = new List<string>()
            };

            // Generate the document that keeps track of active transactions if it does not already exist.
            CreateActiveTransactionsDocIfNotExists(activeTransactionsDoc).Wait();

            // Add a new guid to the list of active transactions for the current transaction.
            AddActiveTransaction().Wait();

            // Invoke bulk import sproc, adding new property TransactionId with value = current active transaction to each imported document.
            InvokeBulkImportSproc().Wait();

            // If bulk import succeeded (or if bulk delete completed successfully), remove id of the current transaction from the active transactions document.
            RemoveActiveTransaction().Wait();
        }

        private static async Task InvokeBulkImportSproc()
        {
            int numDocs = 1000;
            ExampleDoc[] docs = new ExampleDoc[numDocs];

            for (int i = 0; i < numDocs; i++)
            {
                ExampleDoc doc = new ExampleDoc
                {
                    Id = Guid.NewGuid().ToString()
                };

                docs[i] = doc;
            }

            var client = new DocumentClient(new Uri(Endpoint), AuthKey);
            Uri collectionLink = UriFactory.CreateDocumentCollectionUri(DbName, CollectionName);

            string scriptFileName = @"bulkImport.js";
            string scriptId = Path.GetFileNameWithoutExtension(scriptFileName);
            string scriptName = "bulkImport";

            await CreateSprocIfNotExists(scriptFileName, scriptId, scriptName);
            Uri sprocUri = UriFactory.CreateStoredProcedureUri(DbName, CollectionName, scriptName);

            try
            {
                await client.ExecuteStoredProcedureAsync<int>(sprocUri, transactionId.ToString(), docs);
            }
            catch (DocumentClientException ex)
            {
                throw;
            }
            catch (AggregateException ex)
            {
                // If bulk import failed, delete all documents in the collection with TransactionId = id of the failed transaction.
                InvokeBulkDeleteSproc().Wait();
            }
        }

        private static async Task InvokeBulkDeleteSproc()
        {
            var client = new DocumentClient(new Uri(Endpoint), AuthKey);
            Uri collectionLink = UriFactory.CreateDocumentCollectionUri(DbName, CollectionName);

            string scriptFileName = @"bulkDelete.js";
            string scriptId = Path.GetFileNameWithoutExtension(scriptFileName);
            string scriptName = "bulkDelete";

            await CreateSprocIfNotExists(scriptFileName, scriptId, scriptName);
            Uri sprocUri = UriFactory.CreateStoredProcedureUri(DbName, CollectionName, scriptName);

            try
            {
                await client.ExecuteStoredProcedureAsync<Document>(sprocUri, transactionId.ToString());
            }
            catch (DocumentClientException ex)
            {
                throw;
            }
        }

        private static async Task AddActiveTransaction()
        {
            var client = new DocumentClient(new Uri(Endpoint), AuthKey);
            Uri collectionLink = UriFactory.CreateDocumentCollectionUri(DbName, CollectionName);

            transactionId = Guid.NewGuid();

            string scriptFileName = @"addActiveTransaction.js";
            string scriptId = Path.GetFileNameWithoutExtension(scriptFileName);
            string scriptName = "addActiveTransaction";

            await CreateSprocIfNotExists(scriptFileName, scriptId, scriptName);
            Uri sprocUri = UriFactory.CreateStoredProcedureUri(DbName, CollectionName, scriptName);

            try
            {
                await client.ExecuteStoredProcedureAsync<Document>(sprocUri, activeTransactionsDocId.ToString(), transactionId.ToString());
            }
            catch (DocumentClientException ex)
            {
                throw;
            }
        }

        private static async Task RemoveActiveTransaction()
        {
            var client = new DocumentClient(new Uri(Endpoint), AuthKey);
            Uri collectionLink = UriFactory.CreateDocumentCollectionUri(DbName, CollectionName);

            string scriptFileName = @"removeActiveTransaction.js";
            string scriptId = Path.GetFileNameWithoutExtension(scriptFileName);
            string scriptName = "removeActiveTransaction";

            await CreateSprocIfNotExists(scriptFileName, scriptId, scriptName);
            Uri sprocUri = UriFactory.CreateStoredProcedureUri(DbName, CollectionName, scriptName);

            try
            {
                await client.ExecuteStoredProcedureAsync<Document>(sprocUri, activeTransactionsDocId.ToString(), transactionId.ToString());
            }
            catch (DocumentClientException ex)
            {
                throw;
            }
        }

        private static async Task CreateActiveTransactionsDocIfNotExists(ActiveTransactions activeTransactionsDoc)
        {
            var client = new DocumentClient(new Uri(Endpoint), AuthKey);
            Uri collectionLink = UriFactory.CreateDocumentCollectionUri(DbName, CollectionName);
            bool needToCreate = false;

            try
            {
                await client.ReadDocumentAsync(UriFactory.CreateDocumentUri(DbName, CollectionName, activeTransactionsDoc.Id.ToString()));
            }
            catch (DocumentClientException de)
            {
                if (de.StatusCode != HttpStatusCode.NotFound)
                {
                    throw;
                }
                else
                {
                    needToCreate = true;
                }
            }
            if (needToCreate)
            {
                await client.CreateDocumentAsync(collectionLink, activeTransactionsDoc);
            }
        }

        private static async Task ResetActiveTransactionsDocIfNotExists()
        {
            var client = new DocumentClient(new Uri(Endpoint), AuthKey);
            Uri collectionLink = UriFactory.CreateDocumentUri(DbName, CollectionName, activeTransactionsDocId.ToString());

            ActiveTransactions activeTransactionsDoc = new ActiveTransactions
            {
                Id = activeTransactionsDocId,
                Transactions = new List<string>()
            };

            try {
                await client.ReplaceDocumentAsync(collectionLink, activeTransactionsDoc);
            }
            catch (DocumentClientException de)
            {
                throw;
            }
        }

        private static async Task CreateSprocIfNotExists(string scriptFileName, string scriptId, string scriptName)
        {
            var client = new DocumentClient(new Uri(Endpoint), AuthKey);
            Uri collectionLink = UriFactory.CreateDocumentCollectionUri(DbName, CollectionName);

            var sproc = new StoredProcedure
            {
                Id = scriptId,
                Body = File.ReadAllText(scriptFileName)
            };

            bool needToCreate = false;
            Uri sprocUri = UriFactory.CreateStoredProcedureUri(DbName, CollectionName, scriptName);

            try
            {
                await client.ReadStoredProcedureAsync(sprocUri);
            }
            catch (DocumentClientException de)
            {
                if (de.StatusCode != HttpStatusCode.NotFound)
                {
                    throw;
                }
                else
                {
                    needToCreate = true;
                }
            }

            if (needToCreate)
            {
                await client.CreateStoredProcedureAsync(collectionLink, sproc);
            }
        }

        public class ActiveTransactions
        {
            [JsonProperty(PropertyName = "id")]
            public Guid Id { get; set; }
            public List<string> Transactions { get; set; }
            public override string ToString()
            {
                return JsonConvert.SerializeObject(this);
            }
        }

        public class ExampleDoc
        {
            [JsonProperty(PropertyName = "id")]
            public string Id { get; set; }
            public override string ToString()
            {
                return JsonConvert.SerializeObject(this);
            }
        }
    }
}
