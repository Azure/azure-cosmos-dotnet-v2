using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Patterns.OneToOne
{
    class Player
    {
        [JsonProperty("id")]
        public String Id { get; set; }

        [JsonProperty("name")]
        public String Name { get; set; }

        [JsonProperty("handle")]
        public String Handle { get; set; }

        [JsonProperty("highScore")]
        public Double HighScore { get; set; }

        [JsonProperty("lastLogin")]
        public DateTime LastLogin { get; set; }

        [JsonProperty("_etag")]
        public String ETag { get; set; }
    }
}
