using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.Documents.MobileServices
{
    [AttributeUsage(AttributeTargets.Class)]
    public class DocumentAttribute : Attribute
    {
        public string DatabaseId { get; private set; }

        public string CollectionId { get; private set; }

        public DocumentAttribute(string databaseId, string collectionId)
        {
            DatabaseId = databaseId;
            CollectionId = collectionId;
        }
    }
}
