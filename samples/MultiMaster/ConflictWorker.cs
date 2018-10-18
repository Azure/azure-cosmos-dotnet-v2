using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;

namespace CosmosDB.Sql.DotNet.MultiMaster
{
    internal sealed class ConflictWorker
    {
        private readonly IList<DocumentClient> clients;
        private readonly Uri basicCollectionUri;
        private readonly Uri manualCollectionUri;
        private readonly Uri lwwCollectionUri;
        private readonly Uri udpCollectionUri;
        private readonly string databaseName;
        private readonly string basicCollectionName;
        private readonly string manualCollectionName;
        private readonly string lwwCollectionName;
        private readonly string udpCollectionName;

        public ConflictWorker(string databaseName, string basicCollectionName, string manualCollectionName, string lwwCollectionName, string udpCollectionName)
        {
            this.clients = new List<DocumentClient>();
            this.basicCollectionUri = UriFactory.CreateDocumentCollectionUri(databaseName, basicCollectionName);
            this.manualCollectionUri = UriFactory.CreateDocumentCollectionUri(databaseName, manualCollectionName);
            this.lwwCollectionUri = UriFactory.CreateDocumentCollectionUri(databaseName, lwwCollectionName);
            this.udpCollectionUri = UriFactory.CreateDocumentCollectionUri(databaseName, udpCollectionName);

            this.databaseName = databaseName;
            this.basicCollectionName = basicCollectionName;
            this.manualCollectionName = manualCollectionName;
            this.lwwCollectionName = lwwCollectionName;
            this.udpCollectionName = udpCollectionName;
        }

        public void AddClient(DocumentClient client)
        {
            this.clients.Add(client);
        }
        public async Task InitializeAsync()
        {
            DocumentClient createClient = this.clients[0];

            Database database = await createClient.CreateDatabaseIfNotExistsAsync(new Database { Id = this.databaseName });

            DocumentCollection basic = await createClient.CreateDocumentCollectionIfNotExistsAsync(
                UriFactory.CreateDatabaseUri(this.databaseName), new DocumentCollection
                {
                    Id = this.basicCollectionName,
                });

            DocumentCollection manualCollection = await createClient.CreateDocumentCollectionIfNotExistsAsync(
                UriFactory.CreateDatabaseUri(this.databaseName), new DocumentCollection
                {
                    Id = this.manualCollectionName,
                    ConflictResolutionPolicy = new ConflictResolutionPolicy
                    {
                        Mode = ConflictResolutionMode.Custom,
                    },
                });

            DocumentCollection lwwCollection = await createClient.CreateDocumentCollectionIfNotExistsAsync(
                UriFactory.CreateDatabaseUri(this.databaseName), new DocumentCollection
                {
                    Id = this.lwwCollectionName,
                    ConflictResolutionPolicy = new ConflictResolutionPolicy
                    {
                        Mode = ConflictResolutionMode.LastWriterWins,
                        ConflictResolutionPath = "/regionId",
                    },
                });

            DocumentCollection udpCollection = await createClient.CreateDocumentCollectionIfNotExistsAsync(
                UriFactory.CreateDatabaseUri(this.databaseName), new DocumentCollection
                {
                    Id = this.udpCollectionName,
                    ConflictResolutionPolicy = new ConflictResolutionPolicy
                    {
                        Mode = ConflictResolutionMode.Custom,
                        ConflictResolutionProcedure = string.Format("dbs/{0}/colls/{1}/sprocs/{2}", this.databaseName, this.udpCollectionName, "resolver"),
                    },
                });

            StoredProcedure lwwSproc = new StoredProcedure
            {
                Id = "resolver",
                Body = @"
                        function resolver(incomingRecord, existingRecord, isTombstone, conflictingRecords) {
                            var collection = getContext().getCollection();

                            if (!incomingRecord) {
                                if (existingRecord) {

                                    collection.deleteDocument(existingRecord._self, {}, function(err, responseOptions) {
                                        if (err) throw err;
                                    });
                                }
                            } else if (isTombstone) {
                                // delete always wins.
                            } else {
                                var documentToUse = incomingRecord;

                                if (existingRecord) {
                                    if (documentToUse.regionId < existingRecord.regionId) {
                                        documentToUse = existingRecord;
                                    }
                                }

                                var i;
                                for (i = 0; i < conflictingRecords.length; i++) {
                                    if (documentToUse.regionId < conflictingRecords[i].regionId) {
                                        documentToUse = conflictingRecords[i];
                                    }
                                }

                                tryDelete(conflictingRecords, incomingRecord, existingRecord, documentToUse);
                            }

                            function tryDelete(documents, incoming, existing, documentToInsert) {
                                if (documents.length > 0) {
                                    collection.deleteDocument(documents[0]._self, {}, function(err, responseOptions) {
                                        if (err) throw err;

                                        documents.shift();
                                        tryDelete(documents, incoming, existing, documentToInsert);
                                    });
                                } else if (existing) {
                                        collection.replaceDocument(existing._self, documentToInsert,
                                            function(err, documentCreated) {
                                                if (err) throw err;
                                            });
                                } else {
                                    collection.createDocument(collection.getSelfLink(), documentToInsert,
                                        function(err, documentCreated) {
                                            if (err) throw err;
                                        });
                                }
                            }
                        }",
            };

            lwwSproc = await createClient.UpsertStoredProcedureAsync(UriFactory.CreateDocumentCollectionUri(this.databaseName, this.udpCollectionName), lwwSproc);
        }
        public async Task RunManualConflictAsync()
        {
            Console.WriteLine("\r\nInsert Conflict\r\n");
            await this.RunInsertConflictOnManualAsync();

            Console.WriteLine("\r\nUpdate Conflict\r\n");
            await this.RunUpdateConflictOnManualAsync();

            Console.WriteLine("\r\nDelete Conflict\r\n");
            await this.RunDeleteConflictOnManualAsync();
        }
        public async Task RunLWWConflictAsync()
        {
            Console.WriteLine("\r\nInsert Conflict\r\n");
            await this.RunInsertConflictOnLWWAsync();

            Console.WriteLine("\r\nUpdate Conflict\r\n");
            await this.RunUpdateConflictOnLWWAsync();

            Console.WriteLine("\r\nDelete Conflict\r\n");
            await this.RunDeleteConflictOnLWWAsync();
        }
        public async Task RunUDPAsync()
        {
            Console.WriteLine("\r\nInsert Conflict\r\n");
            await this.RunInsertConflictOnUdpAsync();

            Console.WriteLine("\r\nUpdate Conflict\r\n");
            await this.RunUpdateConflictOnUdpAsync();

            Console.WriteLine("\r\nDelete Conflict\r\n");
            await this.RunDeleteConflictOnUdpAsync();
        }
        public async Task RunInsertConflictOnManualAsync()
        {
            Document[] conflictDocuments = await Helpers.PerformInsertConflictAsync(
                this.manualCollectionUri,
                this.clients);

            foreach (Document conflictingInsert in conflictDocuments)
            {
                if (conflictingInsert != null)
                {
                    await this.ValidateManualConflictAsync(this.clients, conflictingInsert);
                }
            }
        }
        public async Task RunUpdateConflictOnManualAsync()
        {
            Document[] conflictDocuments = await Helpers.PerformUpdateConflictAsync(
                this.manualCollectionUri,
                this.clients);

            foreach (Document conflictingUpdate in conflictDocuments)
            {
                if (conflictingUpdate != null)
                {
                    await this.ValidateManualConflictAsync(this.clients, conflictingUpdate);
                }
            }
        }
        public async Task RunDeleteConflictOnManualAsync()
        {
            Document[] conflictDocuments = await Helpers.PerformDeleteConflictAsync(
                this.manualCollectionUri,
                this.clients);

            foreach (Document conflictingDelete in conflictDocuments)
            {
                if (conflictingDelete != null)
                {
                    await this.ValidateManualConflictAsync(this.clients, conflictingDelete);
                }
            }
        }
        public async Task RunInsertConflictOnLWWAsync()
        {
            Document[] conflictDocuments = await Helpers.PerformInsertConflictAsync(
                this.lwwCollectionUri,
                this.clients);

            await this.ValidateLWWAsync(this.clients, conflictDocuments);
        }
        public async Task RunUpdateConflictOnLWWAsync()
        {
            Document[] conflictDocuments = await Helpers.PerformUpdateConflictAsync(
                this.lwwCollectionUri,
                this.clients);

            await this.ValidateLWWAsync(this.clients, conflictDocuments);
        }
        public async Task RunDeleteConflictOnLWWAsync()
        {
            Document[] conflictDocuments = await Helpers.PerformDeleteConflictAsync(
                this.lwwCollectionUri,
                this.clients,
                true);

            // Delete should always win. irrespective of LWW.
            await this.ValidateLWWAsync(this.clients, conflictDocuments, true);
        }
        public async Task RunInsertConflictOnUdpAsync()
        {
            Document[] conflictDocuments = await Helpers.PerformInsertConflictAsync(
                this.udpCollectionUri,
                this.clients);

            await this.ValidateUDPAsync(this.clients, conflictDocuments);
        }
        public async Task RunUpdateConflictOnUdpAsync()
        {
            Document[] conflictDocuments = await Helpers.PerformUpdateConflictAsync(
                this.udpCollectionUri,
                this.clients);

            await this.ValidateUDPAsync(this.clients, conflictDocuments);
        }
        public async Task RunDeleteConflictOnUdpAsync()
        {
            Document[] conflictDocuments = await Helpers.PerformDeleteConflictAsync(
                this.udpCollectionUri,
                this.clients,
                true);

            await this.ValidateUDPAsync(
                this.clients,
                conflictDocuments,
                true);
        }
        private async Task ValidateManualConflictAsync(IList<DocumentClient> clients, Document conflictDocument)
        {
            bool conflictExists = false;
            foreach (DocumentClient client in clients)
            {
                conflictExists = await Helpers.VerifyConflictExistsAsync(
                    client,
                    this.manualCollectionUri,
                    conflictDocument);
            }

            if (conflictExists)
            {
                await this.DeleteConflictAsync(conflictDocument);
            }
        }
        private async Task DeleteConflictAsync(Document conflictDocument)
        {
            DocumentClient delClient = this.clients[0];

            FeedResponse<Conflict> conflicts = await delClient.ReadConflictFeedAsync(this.manualCollectionUri);

            foreach (Conflict conflict in conflicts)
            {
                if (conflict.OperationKind != OperationKind.Delete)
                {
                    Document conflictContent = conflict.GetResource<Document>();
                    if (conflictContent.ResourceId == conflictDocument.ResourceId
                        && conflictContent.ETag == conflictDocument.ETag
                        && conflictContent.GetPropertyValue<int>("regionId") == conflictDocument.GetPropertyValue<int>("regionId"))
                    {
                        Console.WriteLine(
                            "Deleting manual conflict {0} from region {1}",
                            conflict.SourceResourceId,
                            conflictContent.GetPropertyValue<int>("regionId"));
                        await delClient.DeleteConflictAsync(conflict.SelfLink);
                    }
                }
                else if (conflict.SourceResourceId == conflictDocument.ResourceId)
                {
                    Console.WriteLine(
                        "Deleting manual conflict {0} from region {1}",
                        conflict.SourceResourceId,
                        conflictDocument.GetPropertyValue<int>("regionId"));
                    await delClient.DeleteConflictAsync(conflict.SelfLink);
                }
            }
        }
        private async Task ValidateLWWAsync(IList<DocumentClient> clients, Document[] conflictDocument, bool hasDeleteConflict = false)
        {
            Document winnerDocument = Helpers.ResolveWinner(conflictDocument);

            foreach (DocumentClient client in clients)
            {
                await this.ValidateLWWAsync(client, winnerDocument, hasDeleteConflict);
            }
        }
        private async Task ValidateLWWAsync(DocumentClient client, Document winnerDocument, bool hasDeleteConflict)
        {
            await Helpers.AssertNoConflictsAsync(
                client,
                this.lwwCollectionUri);

            // Delete must always win in LWW.
            if (hasDeleteConflict)
            {
                await Helpers.VerifyDocumentNotExistsAsync(
                    client,
                    winnerDocument.Id,
                    winnerDocument.SelfLink);
            }
            else
            {
                // Ensure the write version of document exists in the final image.
                await Helpers.EnsureDocumentExistsAsync(client, winnerDocument, true);
            }
        }
        private async Task ValidateUDPAsync(IList<DocumentClient> clients, Document[] conflictDocument, bool hasDeleteConflict = false)
        {
            Document winnerDocument = Helpers.ResolveWinner(conflictDocument);

            foreach (DocumentClient client in clients)
            {
                await this.ValidateUDPAsync(client, winnerDocument, hasDeleteConflict);
            }
        }
        private async Task ValidateUDPAsync(DocumentClient client, Document winnerDocument, bool hasDeleteConflict)
        {
            await Helpers.AssertNoConflictsAsync(client, this.udpCollectionUri);

            // Delete always wins per our UDP
            if (hasDeleteConflict)
            {
                await Helpers.VerifyDocumentNotExistsAsync(
                    client,
                    winnerDocument.Id,
                    winnerDocument.AltLink);

                return;
            }

            await Helpers.EnsureDocumentExistsAsync(
                client,
                winnerDocument,
                false); // UDP Creates a new document with the same content
        }
    }
}
