using System;
namespace ToDoItems.Core
{
    public class APIKeys
    {
        public APIKeys()
        {
        }

#error Enter the URL of your Azure Cosmos DB endpoint here
        public static readonly string CosmosEndpointUrl = "";

#error Enter the read/write authentication key of your Azure Cosmos DB endpoint here
        public static readonly string CosmosAuthKey = "";
    }
}
