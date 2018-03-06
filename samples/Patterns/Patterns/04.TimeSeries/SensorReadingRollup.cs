using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Patterns.TimeSeries
{
    class SensorReadingRollup
    {
        [JsonProperty("id")]
        public String Id { get; set; }

        [JsonProperty("sensorId")]
        public String SensorId { get; set; }

        [JsonProperty("siteId")]
        public String SiteId { get; set; }

        [JsonProperty("ts")]
        public double UnixTimestamp { get; set; }

        [JsonProperty("count")]
        public int Count { get; set; }

        [JsonProperty("sum_temp")]
        public Double SumTemperature { get; set; }

        [JsonProperty("sum_pressure")]
        public Double SumPressure { get; set; }
    }
}
