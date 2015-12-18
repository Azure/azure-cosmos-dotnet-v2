using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocumentDB.Samples.Shared.Twitter
{
    public class Entities
    {
        [JsonProperty("hashtags")]
        public HashTag[] HashTags { get; set; }

        [JsonProperty("user_mentions")]
        public UserMention[] UserMentions { get; set; }
    }
}
