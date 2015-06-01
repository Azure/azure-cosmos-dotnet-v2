namespace DocumentDB.Samples.Twitter
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using DocumentDB.Samples.Shared.Util;
    using Newtonsoft;
    using Newtonsoft.Json;

    /// <summary>
    /// Represents a Twitter-like status message - includes text, user and entities (hash tags/user mentions).
    /// </summary>
    public class Status
    {
        [JsonProperty("status_id")]
        public long StatusId { get; set; }

        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("user")]
        public User User { get; set; }

        [JsonProperty("created_at")]
        [JsonConverter(typeof(UnixDateTimeConverter))]
        public DateTime CreatedAt { get; set; }

        [JsonProperty("retweet_count")]
        public int RetweetCount { get; set; }

        [JsonProperty("favorite_count")]
        public int FavoriteCount { get; set; }

        [JsonProperty("entities")]
        public Entities Entities { get; set; }

        [JsonProperty("in_reply_to_status_id")]
        public long? InReplyToStatusId { get; set; }
    }
}
