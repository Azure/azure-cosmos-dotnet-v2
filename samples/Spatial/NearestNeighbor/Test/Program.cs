namespace Test
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Documents.Spatial;
    using Spatial;

    internal sealed class Program
    {
        //Read the DocumentDB endpointUrl and authorisationKeys from config
        //These values are available from the Azure Management Portal on the DocumentDB Account Blade under "Keys"
        //NB > Keep these values in a safe & secure location. Together they provide Administrative access to your DocumentDB account
        private static readonly string endpointUrl = ConfigurationManager.AppSettings["EndPointUrl"];
        private static readonly string authorizationKey = ConfigurationManager.AppSettings["AuthorizationKey"];

        private static readonly string documentCollectionLink = ConfigurationManager.AppSettings["DocumentCollectionLink"];
        private static readonly string propertyName = ConfigurationManager.AppSettings["PropertyName"];

        static void Main(string[] args)
        {
            DocumentClient client = new DocumentClient(new Uri(endpointUrl), authorizationKey);
            DocumentCollection collection = client.ReadDocumentCollectionAsync(documentCollectionLink).Result;

            Point point = new Point(-87.636836, 41.884615);
            int minDistance = 100;
            int maxDistance = 5000;
            int maxPoints = 500;

            IEnumerable<dynamic> points = SpatialHelper.Near(client, collection.DocumentsLink, propertyName, point, minDistance, maxDistance, maxPoints);

            long count = 0;
            foreach(dynamic p in points)
            {
                Console.WriteLine(@"Point ID: {0}", p.id);
                ++count;
            }

            Console.WriteLine(@"Found {0} points", count);
        }
    }
}
