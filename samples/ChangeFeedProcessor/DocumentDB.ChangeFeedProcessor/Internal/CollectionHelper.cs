using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;

namespace DocumentDB.ChangeFeedProcessor
{
    internal class CollectionHelper
    {
        internal static Int64 GetDocumentCount(ResourceResponse<DocumentCollection> response)
        {
            Debug.Assert(response != null);

            var resourceUsage = response.ResponseHeaders["x-ms-resource-usage"];
            if (resourceUsage != null)
            {
                var parts = resourceUsage.Split(';');
                foreach (var part in parts)
                {
                    var name = part.Split('=');
                    if (name.Length > 1 && string.Equals(name[0], "documentsCount", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(name[1]))
                    {
                        Int64 result = -1;
                        if (Int64.TryParse(name[1], out result))
                        {
                            return result;
                        }
                        else
                        {
                            TraceLog.Error(string.Format("Failed to get document count from response, can't Int64.TryParse('{0}')", part));
                        }

                        break;
                    }
                }
            }

            return -1;
        }

       internal static async Task<List<PartitionKeyRange>> EnumPartitionKeyRangesAsync(DocumentClient client, string collectionSelfLink)
        {
            Debug.Assert(client != null);
            Debug.Assert(!string.IsNullOrWhiteSpace(collectionSelfLink), "collectionSelfLink");

            string partitionkeyRangesPath = string.Format(CultureInfo.InvariantCulture, "{0}/pkranges", collectionSelfLink);

            FeedResponse<PartitionKeyRange> response = null;
            var partitionKeyRanges = new List<PartitionKeyRange>();
            do
            {
                FeedOptions feedOptions = new FeedOptions { MaxItemCount = 1000, RequestContinuation = response != null ? response.ResponseContinuation : null };
                response = await client.ReadPartitionKeyRangeFeedAsync(partitionkeyRangesPath, feedOptions);
                partitionKeyRanges.AddRange(response);
            }
            while (!string.IsNullOrEmpty(response.ResponseContinuation));

            return partitionKeyRanges;
        }
    }
}
