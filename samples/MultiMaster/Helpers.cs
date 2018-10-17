using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;

namespace CosmosDB.Sql.DotNet.MultiMaster
{
    internal static class Helpers
    {
        public static async Task<Document[]> PerformInsertConflictAsync(Uri collectionLink, IList<DocumentClient> clients)
        {
            do
            {
                Console.WriteLine(
                    "1) Performing conflicting insert across {0} regions on {1}",
                    clients.Count,
                    collectionLink);

                IList<Task<Document>> insertTask = new List<Task<Document>>();

                Document conflictDocument = new Document { Id = Guid.NewGuid().ToString() };
                int index = 0;
                foreach (DocumentClient client in clients)
                {
                    insertTask.Add(
                        Helpers.TryInsertDocumentAsync(
                            client,
                            collectionLink,
                            conflictDocument,
                            index++));
                }

                Document[] conflictDocuments = await Task.WhenAll(insertTask);

                int numberOfConflicts = -1;
                foreach (Document document in conflictDocuments)
                {
                    if (document != null)
                    {
                        ++numberOfConflicts;
                    }
                }

                if (numberOfConflicts > 0)
                {
                    Console.WriteLine("Inserted {0} conflicts", numberOfConflicts);

                    return conflictDocuments;
                }
                else
                {
                    Console.WriteLine("Retrying insert to induce conflicts");
                }
            }
            while (true);
        }
        public static async Task<Document[]> PerformUpdateConflictAsync(Uri collectionUri, IList<DocumentClient> clients)
        {
            do
            {
                Document conflictDocument = new Document { Id = Guid.NewGuid().ToString() };

                conflictDocument = await Helpers.TryInsertDocumentAsync(
                    clients[0],
                    collectionUri,
                    conflictDocument,
                    0);

                await Task.Delay(1000); // 1 Second for write to sync.

                Console.WriteLine(
                    "1) Performing conflicting update across {0} regions on {1}",
                    clients.Count,
                    collectionUri);

                IList<Task<Document>> updateTask = new List<Task<Document>>();

                int index = 0;
                foreach (DocumentClient client in clients)
                {
                    updateTask.Add(
                        Helpers.TryUpdateDocumentAsync(
                            client,
                            collectionUri,
                            conflictDocument,
                            index++));
                }

                Document[] conflictDocuments = await Task.WhenAll(updateTask);

                int numberOfConflicts = -1;
                foreach (Document document in conflictDocuments)
                {
                    if (document != null)
                    {
                        ++numberOfConflicts;
                    }
                }

                if (numberOfConflicts > 0)
                {
                    Console.WriteLine("2) Caused {0} update conflicts, verifying conflict resolution", numberOfConflicts);

                    return conflictDocuments;
                }
                else
                {
                    Console.WriteLine("Retrying update to induce conflicts");
                }
            }
            while (true);
        }
        public static async Task<Document[]> PerformDeleteConflictAsync(Uri collectionUri, IList<DocumentClient> clients, bool bMixUpdateConflict = false)
        {
            do
            {
                Document conflictDocument = new Document { Id = Guid.NewGuid().ToString() };

                conflictDocument = await Helpers.TryInsertDocumentAsync(
                    clients[0],
                    collectionUri,
                    conflictDocument,
                    0);

                await Task.Delay(1000); // 1 Second for write to sync.

                Console.WriteLine(
                    "1) Performing conflicting delete across {0} regions on {1}",
                    clients.Count,
                    collectionUri);

                IList<Task<Document>> deleteTask = new List<Task<Document>>();

                int index = 0;
                foreach (DocumentClient client in clients)
                {
                    if (index % 2 == 1 || !bMixUpdateConflict)
                    {
                        deleteTask.Add(
                            Helpers.TryDeleteDocumentAsync(
                            client,
                            collectionUri,
                            conflictDocument,
                            index++));
                    }
                    else
                    {
                        deleteTask.Add(
                            Helpers.TryUpdateDocumentAsync(
                                client,
                                collectionUri,
                                conflictDocument,
                                index++));
                    }
                }

                Document[] conflictDocuments = await Task.WhenAll(deleteTask);

                int numberOfConflicts = -1;
                foreach (Document document in conflictDocuments)
                {
                    if (document != null)
                    {
                        ++numberOfConflicts;
                    }
                }

                if (numberOfConflicts > 0)
                {
                    Console.WriteLine("2) Caused {0} delete conflicts, verifying conflict resolution", numberOfConflicts);

                    return conflictDocuments;
                }
                else
                {
                    Console.WriteLine("Retrying update/delete to induce conflicts");
                }
            }
            while (true);
        }
        public static async Task AssertNoConflictsAsync(DocumentClient client, Uri collectionUri)
        {
            FeedResponse<Conflict> response = await client.ReadConflictFeedAsync(collectionUri);

            if (response.Count != 0)
            {
                Helpers.TraceError("Found {0} conflicts in the lww collection", response.Count);
                throw new InvalidProgramException();
            }
        }
        public static async Task VerifyDocumentNotExistsAsync(DocumentClient client, string documentId, string documentLink)
        {
            do
            {
                try
                {
                    await client.ReadDocumentAsync(documentLink);

                    Helpers.TraceError(
                        "Delete conflict for document {0} didnt win @ {1}, retrying ...",
                        documentId,
                        client.ReadEndpoint);

                    await Task.Delay(500);
                }
                catch (DocumentClientException exception)
                {
                    if (exception.StatusCode == HttpStatusCode.NotFound)
                    {
                        Console.WriteLine("Delete conflict won @ {0}", client.ReadEndpoint);
                        return;
                    }
                    else
                    {
                        Helpers.TraceError(
                            "Delete conflict for document {0} didnt win @ {1}",
                            documentId,
                            client.ReadEndpoint);

                        await Task.Delay(500);
                    }
                }
            }
            while (true);
        }
        public static async Task<bool> VerifyConflictExistsAsync(DocumentClient client, Uri collectionUri, Document conflictDocument)
        {
            while (true)
            {
                FeedResponse<Conflict> response = await client.ReadConflictFeedAsync(
                    collectionUri);

                foreach (Conflict conflict in response)
                {
                    if (conflict.OperationKind != OperationKind.Delete)
                    {
                        Document conflictDocumentContent = conflict.GetResource<Document>();
                        if (conflictDocument.Id == conflictDocumentContent.Id)
                        {
                            if (conflictDocument.ResourceId == conflictDocumentContent.ResourceId &&
                            conflictDocument.ETag == conflictDocumentContent.ETag)
                            {
                                Console.WriteLine(
                                    "Document from Region {0} lost conflict @ {1}",
                                    conflictDocument.GetPropertyValue<int>("regionId"),
                                    client.ReadEndpoint);
                                return true;
                            }
                            else
                            {
                                try
                                {
                                    // Checking whether this is the winner.
                                    Document winnerDocument = await client.ReadDocumentAsync(conflictDocument.SelfLink);

                                    Console.WriteLine(
                                        "Document from region {0} won the conflict @ {1}",
                                        conflictDocument.GetPropertyValue<int>("regionId"),
                                        client.ReadEndpoint);
                                    return false;
                                }
                                catch (DocumentClientException exception)
                                {
                                    if (exception.StatusCode != System.Net.HttpStatusCode.NotFound)
                                    {
                                        throw;
                                    }
                                    else
                                    {
                                        Console.WriteLine(
                                            "Document from region {0} not found @ {1}",
                                            conflictDocument.GetPropertyValue<int>("regionId"),
                                            client.ReadEndpoint);
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        if (conflict.SourceResourceId == conflictDocument.ResourceId)
                        {
                            Console.WriteLine(
                                "Delete conflict found @ {0}",
                                client.ReadEndpoint);
                            return false;
                        }
                    }
                }

                Helpers.TraceError(
                    "Document {0} is not found in conflict feed @ {1}, retrying",
                    conflictDocument.Id,
                    client.ReadEndpoint);

                await Task.Delay(500);
            }
        }
        public static Document ResolveWinner(Document[] conflictDocuments)
        {
            Document winnerDocument = null;

            foreach (Document document in conflictDocuments)
            {
                if (document != null)
                {
                    if (winnerDocument == null ||
                        winnerDocument.GetPropertyValue<int>("regionId") <= document.GetPropertyValue<int>("regionId"))
                    {
                        winnerDocument = document;
                    }
                }
            }

            Console.WriteLine(
                "Document from region {0} should be the winner",
                winnerDocument.GetPropertyValue<int>("regionId"));

            return winnerDocument;
        }
        public static async Task EnsureDocumentExistsAsync(DocumentClient client, Document winnerDocument, bool bUseSelfLink)
        {
            while (true)
            {
                try
                {
                    Document existingDocument = await client.ReadDocumentAsync(
                        bUseSelfLink ? winnerDocument.SelfLink : winnerDocument.AltLink);

                    if (existingDocument.GetPropertyValue<int>("regionId") == winnerDocument.GetPropertyValue<int>("regionId"))
                    {
                        Console.WriteLine(
                            "Winner document from region {0} found at {1}",
                            existingDocument.GetPropertyValue<int>("regionId"),
                            client.ReadEndpoint);
                        break;
                    }
                    else
                    {
                        Helpers.TraceError(
                            "Winning document version from region {0} is not found @ {1}, retrying...",
                            winnerDocument.GetPropertyValue<int>("regionId"),
                            client.WriteEndpoint);
                        await Task.Delay(500);
                    }
                }
                catch (DocumentClientException)
                {
                    Helpers.TraceError(
                        "Winner document from region {0} is not found @ {1}, retrying...",
                        winnerDocument.GetPropertyValue<int>("regionId"),
                        client.WriteEndpoint);
                    await Task.Delay(500);
                }
            }
        }
        private static async Task<Document> TryInsertDocumentAsync(DocumentClient client, Uri collectionUri, Document document, int index)
        {
            try
            {
                document = Helpers.Clone(document);
                document.SetPropertyValue("regionId", index);
                document.SetPropertyValue("regionEndpoint", client.ReadEndpoint);
                return await client.CreateDocumentAsync(collectionUri, document);
            }
            catch (DocumentClientException exception)
            {
                if (exception.StatusCode == System.Net.HttpStatusCode.Conflict)
                {
                    return null;
                }

                throw;
            }
        }
        private static async Task<Document> TryUpdateDocumentAsync(DocumentClient client, Uri collectionUri, Document document, int index)
        {
            try
            {
                document = Helpers.Clone(document);
                document.SetPropertyValue("regionId", index);
                document.SetPropertyValue("regionEndpoint", client.ReadEndpoint);
                return await client.ReplaceDocumentAsync(document.SelfLink, document, new RequestOptions
                {
                    AccessCondition = new AccessCondition
                    {
                        Type = AccessConditionType.IfMatch,
                        Condition = document.ETag,
                    },
                });
            }
            catch (DocumentClientException exception)
            {
                if (exception.StatusCode == System.Net.HttpStatusCode.PreconditionFailed ||
                    exception.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    // Lost synchronously or not document yet. No conflict is induced.
                    return null;
                }

                throw;
            }
        }
        private static async Task<Document> TryDeleteDocumentAsync(DocumentClient client, Uri collectionUri, Document document, int index)
        {
            try
            {
                document = Helpers.Clone(document);
                document.SetPropertyValue("regionId", index);
                document.SetPropertyValue("regionEndpoint", client.ReadEndpoint);
                await client.DeleteDocumentAsync(document.SelfLink, new RequestOptions
                {
                    AccessCondition = new AccessCondition
                    {
                        Type = AccessConditionType.IfMatch,
                        Condition = document.ETag,
                    },
                });
                return document;
            }
            catch (DocumentClientException exception)
            {
                if (exception.StatusCode == System.Net.HttpStatusCode.NotFound ||
                    exception.StatusCode == System.Net.HttpStatusCode.PreconditionFailed)
                {
                    // Lost synchronously. No conflict is induced.
                    return null;
                }

                throw;
            }
        }
        private static void TraceError(string format, params object[] values)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(format, values);
            Console.ResetColor();
        }
        private static Document Clone(Document source)
        {
            using (Stream stream = new MemoryStream())
            {
                source.SaveTo(stream);
                stream.Position = 0;

                return Document.LoadFrom<Document>(stream);
            }
        }
    }
}
