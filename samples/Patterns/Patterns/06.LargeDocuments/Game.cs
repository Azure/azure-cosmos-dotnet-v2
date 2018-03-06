using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Patterns.LargeDocuments
{
    class Game
    {
        [JsonProperty("id")]
        public String Id { get; set; }

        [JsonProperty("playerId")]
        public String PlayerId { get; set; }

        [JsonProperty("score")]
        public Double Score { get; set; }

        [JsonProperty("startTime")]
        public DateTime StartTime { get; set; }

        [JsonProperty("endTime")]
        public DateTime EndTime { get; set; }

        [JsonProperty("_etag")]
        public String ETag { get; set; }

        [JsonIgnore]
        [JsonProperty("bigGameState")]
        public Dictionary<String, Object> BigGameState { get; set; }

        [JsonProperty("gameStateRefId")]
        public String GameStateReferenceId { get; set; }
    }
}
