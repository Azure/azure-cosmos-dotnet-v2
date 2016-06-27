namespace Spatial
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Documents.Linq;
    using Microsoft.Azure.Documents.Spatial;
    using Newtonsoft.Json;

    public sealed class SpatialHelper
    {
        private static readonly int MaxRetries = 100;
        private static readonly int StatusCodeTooManyRequests = 429;
        private static readonly TimeSpan MinSleepTime = TimeSpan.FromMilliseconds(100);

        /// <summary>
        /// Get the points near the specified point in the order of increasing distance using spatial queries.
        /// </summary>
        /// <param name="client">
        /// The <see cref="DocumentClient"/> object.
        /// </param>
        /// <param name="documentCollectionLink">
        /// The collection self link string.
        /// </param>
        /// <param name="propertyName">
        /// The document property that stores GeoJSON.
        /// </param>
        /// <param name="point">
        /// The specified <see cref="Point"/> of which to find the nearby points.
        /// </param>
        /// <param name="minDistance">
        /// The minimum distance to start with (exclusive).
        /// </param>
        /// <param name="maxDistance">
        /// The maximum distance to end with (inclusive).
        /// </param>
        /// <param name="maxPoints">
        /// The maximum number of points to find.
        /// </param>
        public static IEnumerable<dynamic> Near(DocumentClient client, string documentCollectionLink, string propertyName, Point point, long minDistance, long maxDistance, int maxPoints)
        {
            // Validate arguments
            if(minDistance <= 0) throw new ArgumentOutOfRangeException("Minimum distance must be a positive number.");
            if(maxDistance < minDistance) throw new ArgumentOutOfRangeException("Maximum distance must be greater than or equal to minimum distance.");
            if(maxPoints <= 0) throw new ArgumentOutOfRangeException("Max points must be positive.");

            List<dynamic> totalPoints = new List<dynamic>(maxPoints);

            // Use parametrized query
            // Note: We do a SELECT r here. You may want to modify this and select specified properties.
            string queryFormat = @"SELECT r AS doc, ST_DISTANCE(r.{0}, @loc) AS distance FROM Root r WHERE ST_DISTANCE(r.{0}, @loc) BETWEEN @min AND @max";
            string queryString = String.Format(queryFormat, propertyName);

            for(long distance = minDistance, previousDistance = 0; distance <= maxDistance; distance = Math.Min(2 * distance, maxDistance))
            {
                SqlQuerySpec spec = new SqlQuerySpec()
                {
                    QueryText = queryString,
                    Parameters = new SqlParameterCollection()
                    {
                        new SqlParameter() {
                            Name = "@loc",
                            Value = point
                        },

                        new SqlParameter() {
                            Name = "@min",
                            Value = previousDistance
                        },

                        new SqlParameter() {
                            Name = "@max",
                            Value = distance
                        }
                    }
                };

                IDocumentQuery<dynamic> query = client.CreateDocumentQuery(documentCollectionLink, spec).AsDocumentQuery();

                while (query.HasMoreResults)
                {
                    // Fetch results using ExecuteWithRetries in case of throttle.
                    FeedResponse<dynamic> result = ExecuteWithRetries(() => query.ExecuteNextAsync()).Result;
                    totalPoints.AddRange(result);
                }

                // Using BETWEEN can give us duplicate results. Here we remove them.
                totalPoints.GroupBy(result => result.doc.id).Select(group => group.First());

                // Break once we have found enough points
                if(totalPoints.Count >= maxPoints) break;

                // Break once we reach the maximum distance to search
                if(distance == maxDistance) break;

                previousDistance = distance;
            }

            // Sort the results based on distance
            totalPoints.Sort((point1, point2) => {
                int cmp = point1.distance.CompareTo(point2.distance);
                return cmp != 0 ? cmp : point1.doc.id.CompareTo(point2.doc.id);
            });

            foreach(dynamic result in totalPoints.Take(maxPoints))
                yield return result.doc;
        }

        /// <summary>
        /// Execute the function with retries on throttle.
        /// </summary>
        /// <typeparam name="V">The type of return value from the execution.</typeparam>
        /// <param name="function">The function to execute.</param>
        /// <returns>The response from the execution.</returns>
        public static async Task<V> ExecuteWithRetries<V>(Func<Task<V>> function)
        {
            TimeSpan sleepTime = TimeSpan.Zero;

            int retryCount = 0;
            while(true)
            {
                try
                {
                    ++retryCount;
                    return await function();
                }
                catch(Exception ex)
                {
                    if(retryCount >= MaxRetries)
                    {
                        throw;
                    }

                    DocumentClientException de;

                    while(ex is AggregateException) 
                    { 
                        ex = ((AggregateException)ex).InnerException; 
                    }

                    if(ex is DocumentClientException)
                    {
                        de = (DocumentClientException)ex.InnerException;
                    }
                    else
                    {
                        throw;
                    }

                    if((int)de.StatusCode == StatusCodeTooManyRequests)
                    {
                        sleepTime = (de.RetryAfter < MinSleepTime) ? MinSleepTime : de.RetryAfter;
                    }
                    else
                    {
                        throw;
                    }
                }

                await Task.Delay(sleepTime);
            }
        }
    }
}