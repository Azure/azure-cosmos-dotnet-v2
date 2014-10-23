using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.MobileServices;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Documents.MobileServices.Todolist.DataObjects
{
    [Document("todolist", "AMSDocumentDB")]
    public class DocumentTodoItem : Resource
    {

        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        [JsonProperty(PropertyName = "desc")]
        public string Description { get; set; }

        [JsonProperty(PropertyName = "isComplete")]
        public bool Completed { get; set; }
    }
}