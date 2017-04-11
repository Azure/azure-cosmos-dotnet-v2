namespace DocumentDB.ChangeFeedProcessor.DocumentLeaseStore
{
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;
    using System;
    using System.Globalization;

    class DocumentServiceLease : Lease
    {
        private static readonly DateTime UnixStartTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

        public DocumentServiceLease()
        { 
        }

        public DocumentServiceLease(DocumentServiceLease other) : base(other)
        {
            this.Id = other.Id;
            this.State = other.State;
            this.ETag = other.ETag;
            this.TS = other.TS;
        }

        public DocumentServiceLease(Document document) : this(FromDocument(document))
        {
            if (document == null)
            {
                throw new ArgumentException("document");
            }
        }

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("_etag")]
        public string ETag { get; set; }

        [JsonProperty("state")]
        public LeaseState State { get; set; }

        [JsonIgnore]
        public DateTime Timestamp
        {
            get { return UnixStartTime.AddSeconds(this.TS); }
            set { this.TS = (long)(value - UnixStartTime).TotalSeconds; }
        }

        [JsonIgnore]
        public override string ConcurrencyToken
        {
            get { return this.ETag; }
        }

        [JsonProperty("_ts")]
        private long TS { get; set; }

        public override string ToString()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} {1} Owner='{2}' Continuation={3} Timestamp(local)={4}",
                this.Id,
                this.State,
                this.Owner,
                this.ContinuationToken,
                this.Timestamp.ToLocalTime());
        }

        private static DocumentServiceLease FromDocument(Document document)
        {
            string json = JsonConvert.SerializeObject(document);
            return JsonConvert.DeserializeObject<DocumentServiceLease>(json);
        }
    }
}
