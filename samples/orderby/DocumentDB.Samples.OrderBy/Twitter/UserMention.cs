namespace DocumentDB.Samples.Twitter
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Newtonsoft;
    using Newtonsoft.Json;

    /// <summary>
    /// Represents a "user mention" within a status message.
    /// </summary>
    public class UserMention
    {
        [JsonProperty("user_id")]
        public long UserId { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("screen_name")]
        public string ScreenName { get; set; }

        [JsonProperty("indices")]
        public int[] Indices { get; set; }
    }
}
