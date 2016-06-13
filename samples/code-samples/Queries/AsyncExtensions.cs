using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Documents.Linq;

namespace DocumentDB.Samples.Queries
{
    public static class AsyncExtensions
    {
        public static async Task<IEnumerable<T>> QueryAsync<T>(this IQueryable<T> query)
        {
            var documentQuery = query.AsDocumentQuery();
            var batches = new List<T>();

            do
            {
                batches.AddRange((await documentQuery.ExecuteNextAsync<T>()).ToList());
            } while (documentQuery.HasMoreResults);

            return batches;
        }

        public static async Task<T> FirstOrDefaultAsync<T>(this IQueryable<T> query)
        {
            var documentQuery = query.Take(1).AsDocumentQuery();
            return (await documentQuery.ExecuteNextAsync<T>()).ToList().FirstOrDefault();
        }
    }
}
