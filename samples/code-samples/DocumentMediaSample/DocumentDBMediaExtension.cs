using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Linq;
using Microsoft.Azure.Documents.Client;
using Newtonsoft.Json;
using System.Threading;

namespace Microsoft.Samples.Documents.Media
{
    public static class DocumentDBMediaExtension
    {
        public async static Task UploadMediaAsync(this DocumentClient client,
            string databaseId,
            string collectionId,            
            string mediaId,
            Stream mediaStream)
        {
            IList<MediaDocument> mediaDocuments = await DocumentDBMediaExtension.DocumentFromMedia(
                mediaId, mediaStream);

            IList<Task> uploadTasks = new List<Task>();

            foreach (MediaDocument mediaDocument in mediaDocuments)
            {
                uploadTasks.Add(client.CreateDocumentAsync(
                    UriFactory.CreateDocumentCollectionUri(databaseId, collectionId),
                    mediaDocument));
            }

            await Task.WhenAll(uploadTasks);
        }

        public async static Task<Stream> ReadMediaAsync(this DocumentClient client,
            string databaseId,
            string collectionId,
            string mediaId)
        {
            List<MediaDocument> mediaDocuments = new List<MediaDocument>();

            IDocumentQuery<MediaDocument> mediaQuery = (from media in client.CreateDocumentQuery<MediaDocument>(
                                                       UriFactory.CreateDocumentCollectionUri(databaseId, collectionId),
                                                       new FeedOptions { PartitionKey = new PartitionKey(mediaId) })
                                                    where media.MediaId == mediaId
                                                    select media).AsDocumentQuery();

            do
            {
                FeedResponse<MediaDocument> segmentedResult = await mediaQuery.ExecuteNextAsync<MediaDocument>();
                mediaDocuments.AddRange(segmentedResult);
            } while (mediaQuery.HasMoreResults);

            mediaDocuments.Sort(MediaBlockComparer.Instance);

            return new DocumentStream(mediaDocuments);            
        }       

        public async static Task DeleteMediaAsync(this DocumentClient client,
            string databaseId,
            string collectionId,
            string mediaId)
        {
            List<string> mediablockIds = new List<string>();

            IDocumentQuery<string> mediaQuery = (from media in client.CreateDocumentQuery<MediaDocument>(
                                                       UriFactory.CreateDocumentCollectionUri(databaseId, collectionId),
                                                       new FeedOptions { PartitionKey = new PartitionKey(mediaId) })
                                                        where media.MediaId == mediaId
                                                        select media.Id).AsDocumentQuery();

            do
            {
                FeedResponse<string> segmentedResult = await mediaQuery.ExecuteNextAsync<string>();
                mediablockIds.AddRange(segmentedResult);
            } while (mediaQuery.HasMoreResults);

            List<Task> deleteDocumentTasks = new List<Task>();

            foreach (string blockId in mediablockIds)
            {
                deleteDocumentTasks.Add(client.DeleteDocumentAsync(
                    UriFactory.CreateDocumentUri(databaseId, collectionId, blockId),
                    new RequestOptions { PartitionKey = new PartitionKey(mediaId) }));
            }

            await Task.WhenAll(deleteDocumentTasks);
        }

        public async static Task ClearAllMediaAsync(this DocumentClient client, string mediaDatabaseName, string mediaCollectionName)
        {
            HashSet<string> mediaIds = new HashSet<string>();

            IDocumentQuery<string> mediaQuery = (from media in client.CreateDocumentQuery<MediaDocument>(
                                                       UriFactory.CreateDocumentCollectionUri(mediaDatabaseName, mediaCollectionName),
                                                       new FeedOptions { EnableCrossPartitionQuery = true })
                                                 select media.MediaId).AsDocumentQuery();

            do
            {
                FeedResponse<string> segmentedResult = await mediaQuery.ExecuteNextAsync<string>();
                foreach (string mediaId in segmentedResult)
                {
                    mediaIds.Add(mediaId);
                }
            } while (mediaQuery.HasMoreResults);

            List<Task> deleteDocumentTasks = new List<Task>();

            foreach (string blockId in mediaIds)
            {
                deleteDocumentTasks.Add(client.DeleteMediaAsync(
                    mediaDatabaseName, mediaCollectionName, blockId));
            }

            await Task.WhenAll(deleteDocumentTasks);
        }


        private async static Task<IList<MediaDocument>> DocumentFromMedia(string mediaId, Stream mediaStream)
        {
            IList<MediaDocument> mediaDocuments = new List<MediaDocument>();

            //1 MB Chunks
            int blockSize = 1 * 1024 * 1024;
            long currentIndex = 0;
            int blockId = 0;
            long streamLength = mediaStream.Length;

            do
            {
                Byte[] inArray = new Byte[blockSize];

                int readSize = await mediaStream.ReadAsync(
                    inArray, 0, blockSize);

                mediaDocuments.Add(
                    new MediaDocument
                    {
                        MediaId = mediaId,
                        Id = blockId.ToString(),
                        MediaContent = Convert.ToBase64String(inArray, 0, readSize, Base64FormattingOptions.None)
                    });
                currentIndex += readSize;
                ++blockId;
            } while (currentIndex < streamLength);

            return mediaDocuments;
        }

        private static byte[] MediaFromDocument(MediaDocument document)
        {
            return Convert.FromBase64String(document.MediaContent);
        }

        private sealed class DocumentStream : Stream
        {
            private readonly List<MediaDocument> sortedDocuments;
            private int documentIndex;

            private int streamIndex;
            private byte[] currentBuffer;

            private long position;

            public DocumentStream(List<MediaDocument> sortedDocuments)
            {
                this.sortedDocuments = sortedDocuments;
            }

            public override Task<int> ReadAsync(byte[] buffer,
                int offset,
                int count,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(this.Read(buffer, offset, count));   
            }

            public override long Position
            {
                get
                {
                    return this.position;
                }
                set
                {
                    throw new NotImplementedException();
                }
            }

            public override bool CanWrite
            {
                get { return false; }
            }

            public override bool CanSeek
            {
                get { return false; }
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotImplementedException();
            }

            public override void SetLength(long value)
            {
                throw new NotImplementedException();
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotImplementedException();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                if (this.currentBuffer == null)
                {
                    if (documentIndex < sortedDocuments.Count)
                    {
                        this.currentBuffer = DocumentDBMediaExtension.MediaFromDocument(
                            sortedDocuments[documentIndex++]);
                    }
                    else
                    {
                        return 0;
                    }
                }

                int copyLength = Math.Min(count, currentBuffer.Length - (this.streamIndex));

                Buffer.BlockCopy(this.currentBuffer, this.streamIndex, buffer, offset, copyLength);

                this.streamIndex += copyLength;
                this.position += copyLength;

                if (this.streamIndex >= this.currentBuffer.Length)
                {
                    this.currentBuffer = null;
                    this.streamIndex = 0;                    
                }

                return copyLength;
            }

            public override bool CanRead
            {
                get { return true; }
            }

            public override void Flush()
            {
                throw new NotImplementedException();
            }

            public override long Length
            {
                get { throw new NotImplementedException(); }
            }
        }

        private class MediaBlockComparer : Comparer<MediaDocument>
        {
            private static Comparer<MediaDocument> instance;

            public static Comparer<MediaDocument> Instance
            {
                get
                {
                    if (MediaBlockComparer.instance == null)
                    {
                        MediaBlockComparer.instance = new MediaBlockComparer();
                    }
                    return MediaBlockComparer.instance;
                }
            }

            public override int Compare(MediaDocument x, MediaDocument y)
            {
                return int.Parse(x.Id).CompareTo(int.Parse(y.Id));
            }
        }

        private sealed class MediaDocument
        {
            [JsonProperty("id")]
            public string Id { get; set; }

            [JsonProperty("mediaId")]
            public string MediaId { get; set; }

            public string MediaContent { get; set; }
        }
    }    
}
