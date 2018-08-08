using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.Sql;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents;
using Newtonsoft.Json;
using System.Threading;
using System.Configuration;
using System.Diagnostics;
using AzureCosmosDBLib;

namespace IotEmulator
{
    class Program
    {
        static void Main(string[] args)
        {
            ConnectionPolicy connectionPolicy = new ConnectionPolicy();
            connectionPolicy.UserAgentSuffix = " samples-net/3";
            connectionPolicy.ConnectionMode = ConnectionMode.Direct;
            connectionPolicy.ConnectionProtocol = Protocol.Tcp;

            // Set the read region selection preference order
            connectionPolicy.PreferredLocations.Add(LocationNames.WestUS); // first preference
            connectionPolicy.PreferredLocations.Add(LocationNames.NorthEurope); // second preference
            connectionPolicy.PreferredLocations.Add(LocationNames.SoutheastAsia); // third preference

            Client<IoTData>.Initialize(ConfigurationManager.AppSettings["database"],
                                                          ConfigurationManager.AppSettings["collection"],
                                                          ConfigurationManager.AppSettings["endpoint"],
                                                          ConfigurationManager.AppSettings["authKey"], connectionPolicy);
            while (true)
            {
                Console.Write("*");
                InsertData();
                if (Console.ReadKey().Key ==  ConsoleKey.Escape)
                {
                    break;
                }
            }
            return;
        }
       
        private async static void InsertData()
        {
            var _timestamp = DateTime.UtcNow.Ticks;
            Random r = new Random(DateTime.Now.Millisecond);

            IoTData data = new IoTData
            {
                id = Guid.NewGuid().ToString(),
                iotid = "AA",
                pk = Guid.NewGuid().ToString(),
                //for demo these lat and long are fixed. 
                lat = 47.639002,
                longitude = -122.128196, 
                carid ="AAA", 
                timestamp = _timestamp
            };

            Document doc =  await Client<IoTData>.CreateItemAsync(data);
            IoTData i =  (IoTData) doc;

        }
    }
    public class IoTData 
    {
        [JsonProperty("id")]
        public string id;

        [JsonProperty("pk")]
        public string pk;

        [JsonProperty("iotid")]
        public string iotid;

        [JsonProperty("lat")]
        public double lat;

        [JsonProperty("longitude")]
        public double longitude;

        [JsonProperty("carid")]
        public string carid;

        [JsonProperty("timestamp")]
        public long timestamp;

        public static explicit operator IoTData (Document doc)
        {
            IoTData _iotData = new IoTData();
            _iotData.id = doc.Id;
            _iotData.iotid = doc.GetPropertyValue<string>("iotid");
            _iotData.timestamp = doc.GetPropertyValue<long>("timestamp");
            return _iotData;
        }
    }
}
