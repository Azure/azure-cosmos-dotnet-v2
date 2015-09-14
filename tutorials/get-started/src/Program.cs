//--------------------------------------------------------------------------------- 
// Microsoft (R)  Azure SDK 
// Software Development Kit 
//  
// Copyright (c) Microsoft Corporation. All rights reserved.   
// 
// THIS CODE AND INFORMATION ARE PROVIDED "AS IS" WITHOUT WARRANTY OF ANY KIND,  
// EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE IMPLIED WARRANTIES  
// OF MERCHANTABILITY AND/OR FITNESS FOR A PARTICULAR PURPOSE.  
//---------------------------------------------------------------------------------

namespace DocumentDB.GetStarted
{
    using System;
    using System.Configuration;
    using System.Linq;
    using System.Threading.Tasks;

    // Add DocumentDB references
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Documents.Linq;
    using Newtonsoft.Json;

    /// <summary>
    /// This get-started sample demonstrates the creation of resources and execution of simple queries
    /// For more detailed samples with best practices, visit http://code.msdn.microsoft.com/Azure-DocumentDB-NET-Code-6b3da8af
    /// </summary>
    public class Program
    {
        // Read the DocumentDB endpointUrl and authorizationKey from config file
        // WARNING: Never store credentials in source code
        // For more information, visit http://azure.microsoft.com/blog/2013/07/17/windows-azure-web-sites-how-application-strings-and-connection-strings-work/
        private static readonly string EndpointUrl = ConfigurationManager.AppSettings["EndPointUrl"];
        private static readonly string AuthorizationKey = ConfigurationManager.AppSettings["AuthorizationKey"];

        public static void Main(string[] args)
        {
            try
            {
                GetStartedDemo().Wait();
            }
            catch (DocumentClientException de)
            {
                Exception baseException = de.GetBaseException();
                Console.WriteLine("{0} error occurred: {1}, Message: {2}", de.StatusCode, de.Message, baseException.Message);
            }
            catch (Exception e)
            {
                Exception baseException = e.GetBaseException();
                Console.WriteLine("Error: {0}, Message: {1}", e.Message, baseException.Message);
            }
            finally
            {
                Console.WriteLine("End of demo, press any key to exit.");
                Console.ReadKey();
            }
        }

        private static async Task GetStartedDemo()
        {
            // Create a new instance of the DocumentClient
            var client = new DocumentClient(new Uri(EndpointUrl), AuthorizationKey);

            // Check to verify a database with the id=FamilyRegistry does not exist
            Database database = client.CreateDatabaseQuery().Where(db => db.Id == "FamilyRegistry").AsEnumerable().FirstOrDefault();

            // If the database does not exist, create a new database
            if (database == null)
            {
                database = await client.CreateDatabaseAsync(
                    new Database
                    {
                        Id = "FamilyRegistry"
                    });

                WriteMessage("Created dbs/FamilyRegistry");
            }

            // Check to verify a document collection with the id=FamilyCollection does not exist
            DocumentCollection documentCollection = client.CreateDocumentCollectionQuery("dbs/" + database.Id).Where(c => c.Id == "FamilyCollection").AsEnumerable().FirstOrDefault();

            // If the document collection does not exist, create a new collection
            if (documentCollection == null)
            {
                documentCollection = await client.CreateDocumentCollectionAsync("dbs/" + database.Id,
                    new DocumentCollection
                    {
                        Id = "FamilyCollection"
                    });

                WriteMessage("Created dbs/FamilyRegistry/colls/FamilyCollection");
            }

            // Check to verify a document with the id=AndersenFamily does not exist
            Document document = client.CreateDocumentQuery("dbs/" + database.Id + "/colls/" + documentCollection.Id).Where(d => d.Id == "AndersenFamily").AsEnumerable().FirstOrDefault();

            // If the document does not exist, create a new document
            if (document == null)
            {
                // Create the Andersen Family document
                Family andersonFamily = new Family
                {
                    Id = "AndersenFamily",
                    LastName = "Andersen",
                    Parents = new Parent[] {
                        new Parent { FirstName = "Thomas" },
                        new Parent { FirstName = "Mary Kay"}
                    },
                    Children = new Child[] {
                        new Child
                        { 
                            FirstName = "Henriette Thaulow", 
                            Gender = "female", 
                            Grade = 5, 
                            Pets = new Pet[] {
                                new Pet { GivenName = "Fluffy" } 
                            }
                        } 
                    },
                    Address = new Address { State = "WA", County = "King", City = "Seattle" },
                    IsRegistered = true
                };

                await client.CreateDocumentAsync("dbs/" + database.Id + "/colls/" + documentCollection.Id, andersonFamily);

                WriteMessage("Created dbs/FamilyRegistry/colls/FamilyCollection/docs/AndersenFamily");
            }

            // Check to verify a document with the id=AndersenFamily does not exist
            document = client.CreateDocumentQuery("dbs/" + database.Id + "/colls/" + documentCollection.Id).Where(d => d.Id == "WakefieldFamily").AsEnumerable().FirstOrDefault();

            if (document == null)
            {
                // Create the WakeField document
                Family wakefieldFamily = new Family
                {
                    Id = "WakefieldFamily",
                    Parents = new Parent[] {
                        new Parent { FamilyName= "Wakefield", FirstName= "Robin" },
                        new Parent { FamilyName= "Miller", FirstName= "Ben" }
                    },
                    Children = new Child[] {
                        new Child {
                            FamilyName= "Merriam", 
                            FirstName= "Jesse", 
                            Gender= "female", 
                            Grade= 8,
                            Pets= new Pet[] {
                                new Pet { GivenName= "Goofy" },
                                new Pet { GivenName= "Shadow" }
                            }
                        },
                        new Child {
                            FamilyName= "Miller", 
                            FirstName= "Lisa", 
                            Gender= "female", 
                            Grade= 1
                        }
                    },
                    Address = new Address { State = "NY", County = "Manhattan", City = "NY" },
                    IsRegistered = false
                };

                await client.CreateDocumentAsync("dbs/" + database.Id + "/colls/" + documentCollection.Id, wakefieldFamily);

                WriteMessage("Created dbs/FamilyRegistry/colls/FamilyCollection/docs/WakefieldFamily");
            }

            // Query the documents using DocumentDB SQL for the Andersen family
            var families = client.CreateDocumentQuery("dbs/" + database.Id + "/colls/" + documentCollection.Id,
                "SELECT * " +
                "FROM Families f " +
                "WHERE f.id = \"AndersenFamily\"");

            foreach (var family in families)
            {
                Console.WriteLine("\tRead {0} from SQL", family);
            }

            // Query the documents using LINQ for the Andersen family
            families =
                from f in client.CreateDocumentQuery("dbs/" + database.Id + "/colls/" + documentCollection.Id)
                where f.Id == "AndersenFamily"
                select f;

            foreach (var family in families)
            {
                Console.WriteLine("Read {0} from LINQ", family);
            }

            // Query the documents using LINQ lambdas for the Andersen family
            families = client.CreateDocumentQuery("dbs/" + database.Id + "/colls/" + documentCollection.Id)
                .Where(f => f.Id == "AndersenFamily")
                .Select(f => f);

            foreach (var family in families)
            {
                Console.WriteLine("\tRead {0} from LINQ query", family);
            }

            // Clean up/delete the database and client
            await client.DeleteDatabaseAsync("dbs/" + database.Id);
            client.Dispose();
        }

        private static void WriteMessage(string msg)
        {
            Console.WriteLine(msg);
            Console.WriteLine("Press any key to continue ...");
            Console.ReadKey();
            Console.Clear();
        }

        internal sealed class Parent
        {
            public string FamilyName { get; set; }
            public string FirstName { get; set; }
        }

        internal sealed class Child
        {
            public string FamilyName { get; set; }
            public string FirstName { get; set; }
            public string Gender { get; set; }
            public int Grade { get; set; }
            public Pet[] Pets { get; set; }
        }

        internal sealed class Pet
        {
            public string GivenName { get; set; }
        }

        internal sealed class Address
        {
            public string State { get; set; }
            public string County { get; set; }
            public string City { get; set; }
        }

        internal sealed class Family
        {
            [JsonProperty(PropertyName = "id")]
            public string Id { get; set; }
            public string LastName { get; set; }
            public Parent[] Parents { get; set; }
            public Child[] Children { get; set; }
            public Address Address { get; set; }
            public bool IsRegistered { get; set; }
        }
    }
}
