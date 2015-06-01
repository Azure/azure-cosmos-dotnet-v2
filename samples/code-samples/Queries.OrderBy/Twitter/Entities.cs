namespace DocumentDB.Samples.Twitter
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Newtonsoft;
    using Newtonsoft.Json;

    public class Entities
    {
        [JsonProperty("hashtags")]
        public HashTag[] HashTags { get; set; }

        [JsonProperty("user_mentions")]
        public UserMention[] UserMentions { get; set; }
    }
}
