namespace todo.Models
{
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;

    public class Item
    {
        [JsonProperty(PropertyName="id")]
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool Completed { get; set; }
    }
}