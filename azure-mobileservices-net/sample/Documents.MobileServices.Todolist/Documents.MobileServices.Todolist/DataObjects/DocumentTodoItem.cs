using Microsoft.Azure.Documents.MobileServices;
using Microsoft.WindowsAzure.Mobile.Service;
using Newtonsoft.Json;

namespace Documents.MobileServices.Todolist.DataObjects
{
    [Document("todolist", "AMSDocumentDB")]
    public class DocumentTodoItem : DocumentResource
    {
        [JsonProperty(PropertyName = "text")] 
        public string Text { get; set; }

        [JsonProperty(PropertyName = "complete")] 
        public bool Complete { get; set; }
    }
}