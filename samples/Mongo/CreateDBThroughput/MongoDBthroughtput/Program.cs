using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Authentication;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.IO;
using MongoDB.Bson;

namespace MongoDBthroughtput
{
    class Program
    {
        static MongoClient _mongoClient;
        static string _databaseName = "DB";
        static string _collectionName = "IoT";
        static IMongoDatabase _mdb;

        static void Main(string[] args)
        {
            string connectionString = "YourConnectionString";
            MongoClientSettings settings = MongoClientSettings.FromUrl(
              new MongoUrl(connectionString)
            );
            settings.SslSettings =
              new SslSettings() { EnabledSslProtocols = SslProtocols.Tls12 };
            _mongoClient = new MongoClient(settings);

            CreateDatabase();
            CreateCollection(_mdb);
            InsertDocument(_mdb);

            Console.Read();
        }

        /*
         * Create database with offerThroughput
         */

        private static void CreateDatabase()
        {
            try
            {
                _mdb = _mongoClient.GetDatabase(_databaseName);
                BsonDocumentCommand<BsonDocument> command = new BsonDocumentCommand<BsonDocument>
                    (BsonDocument.Parse("" +
                    "                     {customAction: \"createDatabase\", " +
                    "                     \"offerThroughput\": 10000" +
                    "                     }"));
                _mdb.RunCommand(command);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Database is created");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine("Database creation error: " + ex.Message);
                DropDatabase(_mdb);

            }
        }
        
        /*
         * Create a Collection
         */
        private static void CreateCollection(IMongoDatabase mdb)
        {
            try
            {
                BsonDocumentCommand<BsonDocument> command = new BsonDocumentCommand<BsonDocument>(
                    BsonDocument.Parse(
                    "{ shardCollection: \"" + _databaseName + "." + _collectionName + "\", key: {iotId: \"hashed\"}}"
                    ));
                mdb.RunCommand(command);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine("Collection creation error: " + ex.Message);
            }
        }
     
        /*
         * Insert a Document
         */ 
        private static void InsertDocument(IMongoDatabase mdb)
        {
            IMongoCollection<BsonDocument> collection = mdb.GetCollection<BsonDocument>(_collectionName);

            BsonDocument doc = new BsonDocument
                {
                    {"id", new BsonString("1") },
                    {"iotId", new BsonString("1")}
                };
            collection.InsertOne(doc);
            Console.WriteLine("Document inserted");
        }

        /*
         * Drop a Database
         */
        private static void DropDatabase(IMongoDatabase mdb)
        {
            var cmd = new JsonCommand<BsonDocument>("{dropDatabase: 1}");
            mdb.RunCommand(cmd);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Database is dropped");

        }
    }
}
