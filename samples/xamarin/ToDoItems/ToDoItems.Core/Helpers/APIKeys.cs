using System;
namespace ToDoItems.Core
{
	public class APIKeys
	{
		public APIKeys()
		{
		}

#error Create a CosmosDB SQL Database and enter the URL here
		public static readonly string CosmosEndpointUrl = "** YOUR URL HERE **";

#error Create a CosmosDB SQL Database and enter the authorization key here
		public static readonly string CosmosAuthKey = "** YOUR AUTH KEY HERE **";
	}
}
