using Microsoft.Azure.Documents;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace searchabletodo.Models
{
    public class Item : Document
    {
        [JsonProperty(PropertyName = "id")]
        public override string Id { get; set; }

        [JsonProperty(PropertyName = "title")]
        public string Title { get; set; }

        [JsonProperty(PropertyName = "description")]
        public string Description { get; set; }

        [JsonProperty(PropertyName = "isComplete")]
        public bool Completed { get; set; }

        private DateTime dueDate;
        [DataType(DataType.Date)]
        [JsonProperty(PropertyName = "dueDate")]
        public DateTime DueDate 
        {
            get { return this.dueDate.ToUniversalTime(); }
            set { this.dueDate = value; }
        }

        [JsonProperty(PropertyName = "dueDateEpoch")]
        public Int64 DueDateEpoch 
        {
            get
            {
                return (this.DueDate.Equals(null) || this.DueDate.Equals(DateTime.MinValue))
                    ? int.MinValue
                    : this.DueDate.ToEpoch();
            }
        }

        [JsonProperty(PropertyName = "tags")]
        public List<string> Tags { get; set; }
    }
}