using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using System.Web;
using searchabletodo.Models;
using System.Net.Http;
using Newtonsoft.Json;
using System.Net;
using System.Text;
using Newtonsoft.Json.Linq;

namespace searchabletodo.Data
{
    public static class ItemSearchRepository
    {
        private static readonly Uri _serviceRoot;
        private static readonly HttpClient _httpClient;

        static ItemSearchRepository()
        {
            string apiKey = ConfigurationManager.AppSettings["search-authKey"];
            _serviceRoot = new Uri(ConfigurationManager.AppSettings["search-endpoint"]);

            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("api-key", apiKey);
        }

        public static async Task<ItemSearchResults> SearchAsync(string text)
        {
            const string urlTemplate = "/indexes/todo/docs?facet=dueDate,interval:day&facet=tags&$count=true&&$top=15&search={0}";

            var response = await SendAsync(HttpMethod.Get, String.Format(urlTemplate, Uri.EscapeDataString(text)));
            response.EnsureSuccessStatusCode();
            var results = JsonConvert.DeserializeObject<dynamic>(await response.Content.ReadAsStringAsync());

            return new ItemSearchResults
            {
                TotalCount = results["@odata.count"],
                Items = ((IEnumerable<dynamic>)results.value).Select(i => new Item { Id = i.id, Title = i.title, Description = i.description, DueDate = i.dueDate, Tags = ((JArray)i.tags).Select(t => (string)t).ToList(), Completed = i.isComplete }),
                TagCounts = ((IEnumerable<dynamic>)results["@search.facets"].tags).Select(f => Tuple.Create((string)f.value, (int)f.count)),
                DateCounts = ((IEnumerable<dynamic>)results["@search.facets"].dueDate).Select(f => Tuple.Create((string)f.value, (int)f.count))
            };
        }

        public static async Task<string[]> SuggestAsync(string prefix)
        {
            const string urlTemplate = "/indexes/todo/docs/suggest?suggesterName=sg&$top=10&searchFields=title&fuzzy=true&search={0}";

            var response = await SendAsync(HttpMethod.Get, String.Format(urlTemplate, Uri.EscapeDataString(prefix)));
            response.EnsureSuccessStatusCode();
            var results = JsonConvert.DeserializeObject<dynamic>(await response.Content.ReadAsStringAsync());

            return ((IEnumerable<dynamic>)results.value).Select(s => (string)s["@search.text"]).ToArray();
        }

        public static async Task RunIndexerAsync()
        {
            var response = await SendAsync(HttpMethod.Post, "/indexers/todoixr/run");
            response.EnsureSuccessStatusCode();
        }

        public static async Task SetupSearchAsync()
        {
            await CreateToDoIndexAsync().ConfigureAwait(false);
            await CreateDocumentDBIndexerAsync().ConfigureAwait(false);
        }

        public static async Task DeleteAll()
        {
            await SendAsync(HttpMethod.Post, "/indexers/todoixr/reset").ConfigureAwait(false);
            await SendAsync(HttpMethod.Delete, "/indexers/todoixr").ConfigureAwait(false);
            await SendAsync(HttpMethod.Delete, "/datasources/tododocdb").ConfigureAwait(false);
            await SendAsync(HttpMethod.Delete, "/indexes/todo").ConfigureAwait(false);
        }

        private static async Task CreateToDoIndexAsync()
        {
            if (!await ResourceExistsAsync("/indexes/todo"))
            {
                var index = new
                {
                    name = "todo",
                    fields = new[] 
                    { 
                        new { name = "id", type = "Edm.String",               key = true,  facetable = false, filterable = false, searchable = false, sortable = false },
                        new { name = "title", type = "Edm.String",            key = false, facetable = false, filterable = false, searchable = true,  sortable = false },
                        new { name = "description", type = "Edm.String",      key = false, facetable = false, filterable = false, searchable = true,  sortable = false },
                        new { name = "dueDate", type = "Edm.DateTimeOffset",  key = false, facetable = true,  filterable = true,  searchable = false, sortable = true  },
                        new { name = "isComplete", type = "Edm.String",       key = false, facetable = false, filterable = false, searchable = false, sortable = false },
                        new { name = "tags", type = "Collection(Edm.String)", key = false, facetable = true,  filterable = true,  searchable = true,  sortable = false }
                    },
                    suggesters = new[] 
                    { 
                        new { name = "sg", searchMode = "analyzingInfixMatching", sourceFields = new[] { "title", "tags" } }
                    },
                    corsOptions = new
                    {
                        allowedOrigins = new[] { "*" } // use a specific domain when allowing Javascript hit your search index directly, "*" is used for demo purposes
                    }
                };

                await SendAsync(HttpMethod.Post, "/indexes", JsonConvert.SerializeObject(index));
            }
        }

        private static async Task CreateDocumentDBIndexerAsync()
        {
            string account = new Uri(ConfigurationManager.AppSettings["docdb-endpoint"]).Host.Split('.')[0];
            string authKey = ConfigurationManager.AppSettings["docdb-authKey"];
            string database = ConfigurationManager.AppSettings["docdb-database"];
            string collection = ConfigurationManager.AppSettings["docdb-collection"];

            // create data source
            if (!await ResourceExistsAsync("/datasources/tododocdb"))
            {
                var dataSource = new
                {
                    name = "tododocdb",
                    type = "documentdb",
                    credentials = new
                    {
                        connectionString = String.Format("AccountName={0};AuthKey={1};DatabaseId={2}", account, authKey, database)
                    },
                    container = new
                    {
                        name = collection
                    },
                    dataChangeDetectionPolicy = new Dictionary<string, object>
                    {
                        { "@odata.type", "#Microsoft.Azure.Search.HighWaterMarkChangeDetectionPolicy" },
                        { "highWaterMarkColumnName", "_ts" }, // DocumentDB has a built-in timestamp property called "_ts"
                    }
                };

                await SendAsync(HttpMethod.Post, "/datasources", JsonConvert.SerializeObject(dataSource));
            }

            // create indexer and schedule it
            if (!await ResourceExistsAsync("/indexers/todoixr"))
            {
                var indexer = new
                {
                    name = "todoixr",
                    dataSourceName = "tododocdb",
                    schedule = new { interval = "PT5M" }, // every 5 minutes
                    targetIndexName = "todo"
                };

                await SendAsync(HttpMethod.Post, "/indexers", JsonConvert.SerializeObject(indexer));
            }
        }

        private static async Task<bool> ResourceExistsAsync(string url)
        {
            var response = await SendAsync(HttpMethod.Get, url);
            return response.StatusCode != HttpStatusCode.NotFound;
        }

        private static async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, string content = null)
        {
            url += (url.Contains('?') ? "&" : "?") + "api-version=2014-10-20-Preview";
            Uri fullUrl = new Uri(_serviceRoot, url);
            var request = new HttpRequestMessage(method, fullUrl);
            if (content != null)
            {
                request.Content = new StringContent(content, Encoding.UTF8, "application/json");
            }
            var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotFound)
            {
                throw new Exception("Azure Search request failed:" + await response.Content.ReadAsStringAsync().ConfigureAwait(false));
            }
            return response;
        }
    }
}