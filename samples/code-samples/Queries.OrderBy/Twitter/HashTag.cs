namespace DocumentDB.Samples.Twitter
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Newtonsoft;
    using Newtonsoft.Json;

    public class HashTag
    {
        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("indices")]
        public int[] Indices { get; set; }
    }
}
