
namespace DocumentDB.Sample.MultiModel
{
    using Newtonsoft.Json;

    internal sealed class County
    {
        [JsonProperty(PropertyName = "id")]
        public string Name { get; set; }

        [JsonProperty(PropertyName = "label")]
        public string Label { get; set; }

        public int Population { get; set; }

        public string State { get; set; }

        public string Seat { get; set; }
    }
}
