
namespace DocumentDB.Sample.MultiModel
{
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Documents.Linq;
    using System;
    using System.Threading.Tasks;

    internal sealed class DocumentModel
    {
        private readonly DocumentClient client;
        private readonly Uri collectionLink;

        public DocumentModel(DocumentClient client, Uri collectionLink)
        {
            this.client = client;
            this.collectionLink = collectionLink;
        }

        public async Task InsertCountyDataAsync()
        {
            await this.client.CreateDocumentAsync(this.collectionLink, new County() { Name = "King", Population = 1931249, Seat = "Seattle", State = "WA" });
            await this.client.CreateDocumentAsync(this.collectionLink, new County() { Name = "Pierce", Population = 795225, Seat = "Tacoma", State = "WA" });
            await this.client.CreateDocumentAsync(this.collectionLink, new County() { Name = "Snohomish", Population = 713335, Seat = "Everett", State = "WA" });
            await this.client.CreateDocumentAsync(this.collectionLink, new County() { Name = "Spokane", Population = 421221, Seat = "Spokane", State = "WA" });
            await this.client.CreateDocumentAsync(this.collectionLink, new County() { Name = "Lewis", Population = 75455, Seat = "Chehalis", State = "WA" });
            await this.client.CreateDocumentAsync(this.collectionLink, new County() { Name = "Cowlitz", Population = 102410, Seat = "Kelso", State = "WA" });
            await this.client.CreateDocumentAsync(this.collectionLink, new County() { Name = "Clark", Population = 425363, Seat = "Vancouver", State = "WA" });
            await this.client.CreateDocumentAsync(this.collectionLink, new County() { Name = "Skamania", Population = 11066, Seat = "Stevenson", State = "WA" });

            await this.client.CreateDocumentAsync(this.collectionLink, new County() { Name = "Baker", Population = 16510, Seat = "Baker City", State = "OR" });
            await this.client.CreateDocumentAsync(this.collectionLink, new County() { Name = "Multinomah", Population = 790670, Seat = "Portland", State = "OR" });
        }

        public async Task QueryCountyAsync()
        {
            // 1. Query  For Highest Population County
            var query = this.client.CreateDocumentQuery<County>(this.collectionLink,
                "SELECT TOP 1 * FROM root r ORDER BY r.Population DESC").AsDocumentQuery();

            while (query.HasMoreResults)
            {
                foreach (County county in await query.ExecuteNextAsync())
                {
                    Console.WriteLine("County with the Max Population - {0}", county.Name);
                }
            }

            // 2. Query the county name where seat = Seattle
            query = this.client.CreateDocumentQuery<County>(this.collectionLink,
                "SELECT * FROM root r WHERE r.Seat='Everett'").AsDocumentQuery();

            while (query.HasMoreResults)
            {
                foreach (County county in await query.ExecuteNextAsync())
                {
                    Console.WriteLine("County with the Everett Seat - {0}", county.Name);
                }
            }
        }
    }
}
