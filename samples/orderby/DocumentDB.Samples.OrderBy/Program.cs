namespace DocumentDB.Samples.OrderBy
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Documents.Linq;
    using Microsoft.Azure.Documents.Partitioning;
    using Newtonsoft;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// This sample demonstrates basic CRUD operations on a Database resource for Azure DocumentDB.
    /// </summary>
    public class Program
    {
        /// <summary>
        /// The DocumentDB endpoint read from config. 
        /// These values are available from the Azure Management Portal on the DocumentDB Account Blade under "Keys".
        /// NB Keep these values in a safe and secure location. Together they provide Administrative access to your DocDB account.
        /// </summary>
        private static readonly string EndpointUrl = ConfigurationManager.AppSettings["EndPointUrl"];

        /// <summary>
        /// The DocumentDB authorization key.
        /// These values are available from the Azure Management Portal on the DocumentDB Account Blade under "Keys"
        /// </summary>
        private static readonly string AuthorizationKey = ConfigurationManager.AppSettings["AuthorizationKey"];

        /// <summary>
        /// Database Id used for these samples.
        /// </summary>
        private static readonly string DatabaseId = ConfigurationManager.AppSettings["DatabaseId"];

        /// <summary>
        /// The ConnectionPolicy for these samples. Sets custom user-agent.
        /// </summary>
        private static readonly ConnectionPolicy ConnectionPolicy = new ConnectionPolicy { UserAgentSuffix = " samples-net-orderby/1" };

        /// <summary>
        /// The DocumentDB client instance.
        /// </summary>
        private DocumentClient client;

        /// <summary>
        /// Initializes a new instance of the <see cref="Program"/> class.
        /// </summary>
        /// <param name="client">The DocumentDB client instance.</param>
        private Program(DocumentClient client)
        {
            this.client = client;
        }

        /// <summary>
        /// Main method for the sample.
        /// </summary>
        /// <param name="args">command line arguments.</param>
        public static void Main(string[] args)
        {
            try
            {
                using (var client = new DocumentClient(new Uri(EndpointUrl), AuthorizationKey, ConnectionPolicy))
                {
                    var program = new Program(client);
                    program.RunAsync().Wait();
                    Console.WriteLine("Samples completed successfully.");
                }
            }
#if !DEBUG
            catch (Exception e)
            {
                // If the Exception is a DocumentClientException, the "StatusCode" value might help identity 
                // the source of the problem. 
                Console.WriteLine("Samples failed with exception:{0}", e);
            }
#endif
            finally
            {
                Console.WriteLine("End of samples, press any key to exit.");
                Console.ReadKey();
            }
        }

        /// <summary>
        /// Run samples for Database create, read, update and delete.
        /// </summary>
        /// <returns>a Task object.</returns>
        private async Task RunAsync()
        {
        }
    }
}