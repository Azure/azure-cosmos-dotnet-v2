
namespace DocumentDB.Sample.MultiModel
{
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Documents.Linq;
    using Microsoft.Azure.Graphs;
    using System;
    using System.Threading.Tasks;

    class GraphModel
    {
        private readonly DocumentClient client;
        private readonly DocumentCollection collection;

        private GraphConnection graphConnection;
        private GraphCommand graphCommand;

        public GraphModel(DocumentClient client, DocumentCollection collection)
        {
            this.client = client;
            this.collection = collection;
            this.graphConnection = new GraphConnection(client, collection);
            this.graphCommand = new GraphCommand(this.graphConnection);
        }

        public async Task AddNodesAsync()
        {
            string[] edgeQueries = new string[]
            {
                //Adams
                "g.AddV('county').Property('name', 'Adams').Property('id', 'Adams').Property('Population', 18728).Property('Seat', 'Ritzville')",

                //Grant
                "g.AddV('county').Property('name', 'Grant').Property('id', 'Grant').Property('Population', 89120).Property('Seat', 'Ephrata')",
            };

            foreach (string edgeQuery in edgeQueries)
            {
                Console.WriteLine("Query : " + edgeQuery);
                IDocumentQuery<dynamic> query = this.client.CreateGremlinQuery(this.collection, edgeQuery);
                while (query.HasMoreResults)
                {
                    await query.ExecuteNextAsync();
                }
            }
        }

        public async Task AddEdgesAsync()
        {
            string[] edgeQueries = new string[]
            {
                // King -> Pierce
                "g.V('King').AddE('neighbor').Property('inter-state', 'false').To(g.V('Pierce'))",

                // King -> Snohomish
                "g.V('King').AddE('neighbor').Property('inter-state', 'false').To(g.V('Snohomish'))",

                // Pierce -> Lewis
                "g.V('Pierce').AddE('neighbor').Property('inter-state', 'false').To(g.V('Lewis'))",

                // Lewis -> Cowlitz
                "g.V('Lewis').AddE('neighbor').Property('inter-state', 'false').To(g.V('Cowlitz'))",

                // Cowlitz -> Clark
                "g.V('Cowlitz').AddE('neighbor').Property('inter-state', 'false').To(g.V('Clark'))",

                // Lewis -> Skamania 
                "g.V('Lewis').AddE('neighbor').Property('inter-state', 'false').To(g.V('Skamania'))",

                // Skamania -> Clark
                "g.V('Skamania').AddE('neighbor').Property('inter-state', 'false').To(g.V('Clark'))",

                // Clark -> Multinomah
                "g.V('Clark').AddE('neighbor').Property('inter-state', 'true').To(g.V('Multinomah'))",

                // Adams -> Grant
                "g.V('Adams').AddE('neighbor').Property('inter-state', 'false').To(g.V('Grant'))"
            };

            foreach (string edgeQuery in edgeQueries)
            {
                Console.WriteLine("Query : " + edgeQuery);
                IDocumentQuery<dynamic> query = this.client.CreateGremlinQuery(this.collection, edgeQuery);
                while (query.HasMoreResults)
                {
                    await query.ExecuteNextAsync();
                }
            }
        }


        public async Task QueryAsync()
        {
            string[] queries = new string[]
            {
                // 1. All neighbors of King county
                "g.V('King').out()",

                //2. All neighbors of Clark where inter-state = false (connecting between states)
                "g.V('Clark').outE().has('inter-state', 'true')",

                //3. Find the path from King County till Multinomah  -- DFS  (King->Pierce->Lewis->Cowlitz->Clark->Multinomah)
                "g.V('King').repeat(out()).until(hasId('Multinomah')).path().limit(1)"
            };

            foreach (string queryText in queries)
            {
                Console.WriteLine("Query : " + queryText);

                IDocumentQuery<dynamic> query = this.client.CreateGremlinQuery(this.collection, queryText);
                while (query.HasMoreResults)
                {
                    foreach (var resultStep in await query.ExecuteNextAsync())
                    {
                        Console.WriteLine(" Path : " + resultStep.ToString());
                    }
                }
            }
        }
    }
}
