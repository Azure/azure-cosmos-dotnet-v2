namespace DocumentDB.Samples.Twitter
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using Newtonsoft;
    using Newtonsoft.Json;
    
    /// <summary>
    /// Simple Twitter Client that uses the REST API to fetch status updates as JSON to be stored in Azure DocumentDB.
    /// </summary>
    public sealed class TwitterClient
    {
        private const string TwitterSearchApiUriFormat = @"https://api.twitter.com/1.1/search/tweets.json?q={0}&count={1}";

        private const int TwitterApiPageSize = 100;

        private const int RetryMinimumSleepSeconds = 1;

        private static readonly DateTime UnixStartTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

        private HttpClient httpClient;

        private OAuthMessageHandler oathMessageHandler;

        /// <summary>
        /// Initializes a new instance of a simple Twitter API client.
        /// </summary>
        /// <param name="consumerKey"></param>
        /// <param name="consumerSecret"></param>
        /// <param name="accessToken"></param>
        /// <param name="accessTokenSecret"></param>
        public TwitterClient(string consumerKey, string consumerSecret, string accessToken, string accessTokenSecret)
        {
            this.oathMessageHandler = new OAuthMessageHandler(new HttpClientHandler(), consumerKey, consumerSecret, accessToken, accessTokenSecret);
            this.httpClient = new HttpClient(this.oathMessageHandler);
        }

        /// <summary>
        /// Get Twitter status updates (tweets) by query string.
        /// </summary>
        /// <param name="query">Twitter query string</param>
        /// <returns>A list of status updates.</returns>
        public IEnumerable<Status> GetStatuses(string query)
        {
            int numItemsRead = 0;
            int numItemsReadInBatch = 0;
            long? continuationStatusId = null;

            do
            {
                string uri = string.Format(TwitterSearchApiUriFormat, query, TwitterApiPageSize);

                if (continuationStatusId != null)
                {
                    uri = uri + "&max_id=" + (continuationStatusId - 1);
                }

                numItemsReadInBatch = 0;
                var readStatuses = new List<Status>();
                int sleepTimeSeconds = RetryMinimumSleepSeconds;
                bool isSuccessful = false;

                while (!isSuccessful)
                {
                    try
                    {
                        using (Stream twitterStream = this.httpClient.GetStreamAsync(uri).Result)
                        using (StreamReader sr = new StreamReader(twitterStream))
                        {
                            string statusesString = sr.ReadToEnd();
                            dynamic statuses = JsonConvert.DeserializeObject(statusesString);

                            foreach (dynamic statusMessage in statuses.statuses)
                            {
                                numItemsReadInBatch++;
                                Status status = this.TransformDocument(statusMessage);

                                if (status.StatusId < continuationStatusId || continuationStatusId == null)
                                {
                                    continuationStatusId = status.StatusId;
                                }

                                readStatuses.Add(status);
                            }
                        }

                        isSuccessful = true;
                    }
                    catch (Exception)
                    {
                        Console.WriteLine("Throttled; sleeping for {0}s", sleepTimeSeconds);
                        Thread.Sleep(sleepTimeSeconds * 1000);
                        sleepTimeSeconds *= 2;
                    }
                }

                numItemsRead += numItemsReadInBatch;
                Console.WriteLine("Read {0} messages, continuation: {1}", numItemsRead, continuationStatusId);

                foreach (Status statusMessage in readStatuses) 
                {
                    yield return statusMessage;
                }
            }
            while (numItemsReadInBatch > 0);
        }

        /// <summary>
        /// Transforms the document with some minor updates for easy querying with DocumentDB.
        /// </summary>
        /// <param name="statusMessage">The un-typed statusMessage</param>
        /// <returns>The status update as a typed Status object.</returns>
        private Status TransformDocument(dynamic statusMessage)
        {
            long statusId = (long)statusMessage.id;

            statusMessage["status_id"] = statusId;
            statusMessage["id"] = Guid.NewGuid();

            statusMessage["created_at"] = this.GetEpochTime((string)statusMessage["created_at"]);
            statusMessage["user"]["created_at"] = this.GetEpochTime((string)statusMessage["user"]["created_at"]);

            return (Status)statusMessage;
        }

        /// <summary>
        /// Convert DateTime string to epoch.
        /// </summary>
        /// <param name="twitterDateString">NS format DateTime.</param>
        /// <returns>time as seconds since the epoch.</returns>
        private long GetEpochTime(string twitterDateString)
        {
            DateTime dt = DateTime.ParseExact(
                twitterDateString, 
                "ddd MMM dd HH:mm:ss zzz yyyy", 
                CultureInfo.InvariantCulture, 
                DateTimeStyles.AdjustToUniversal);
            
            return (long)((DateTime)dt - UnixStartTime).TotalSeconds;
        }
    }
}