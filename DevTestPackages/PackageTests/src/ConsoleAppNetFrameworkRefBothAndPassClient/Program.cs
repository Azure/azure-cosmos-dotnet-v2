using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Documents.Client;

namespace ConsoleAppNetFrameworkRefBothAndPassClient
{
    class Program
    {
        private static readonly string endpointUrl = "https://localhost:8081/";
        private static readonly string authorizationKey = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
        static void Main(string[] args)
        {
            var client = new DocumentClient(new Uri(endpointUrl), authorizationKey, new ConnectionPolicy { ConnectionMode = ConnectionMode.Direct, ConnectionProtocol = Protocol.Tcp });

            var test = new SmokeTestLib.SmokeTest();
            test.Client = client;
            test.RunDemoAsync().Wait();

            var test2 = new SmokeTestLib.NetStandard.SmokeTest();
            test.Client = client;
            test.RunDemoAsync().Wait();
        }
    }
}
