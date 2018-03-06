using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Patterns.EventSourcing
{
    class OrderEvent
    {
        [JsonProperty("orderId")]
        public String OrderId { get; set; }

        [JsonProperty("state")]
        public String CurrentState { get; set; }

        [JsonProperty("eventType")]
        [JsonConverter(typeof(IsoDateTimeConverter))]
        public DateTime EventTime { get; set; }

        [JsonExtensionData]
        public Dictionary<String, String> AdditionalData { get; set; }
    }
}
