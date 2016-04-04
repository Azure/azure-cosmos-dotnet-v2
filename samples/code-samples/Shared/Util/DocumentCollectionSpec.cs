namespace DocumentDB.Samples.Shared.Util
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// The specification/template for creating a new DocumentCollection.
    /// </summary>
    public class DocumentCollectionSpec
    {
        /// <summary>
        /// Gets or sets the IndexingPolicy to use.
        /// </summary>
        public IndexingPolicy IndexingPolicy { get; set; }

        /// <summary>
        /// Gets or sets the collection performance (reserved throughput) in terms of RU/s
        /// </summary>
        public int? OfferThroughput { get; set; }

        /// <summary>
        /// Gets or sets the stored procedures to register.
        /// </summary>
        public IList<StoredProcedure> StoredProcedures { get; set; }

        /// <summary>
        /// Gets or sets the triggers to register.
        /// </summary>
        public IList<Trigger> Triggers { get; set; }

        /// <summary>
        /// Gets or sets the UDFs to register.
        /// </summary>
        public IList<UserDefinedFunction> UserDefinedFunctions { get; set; }
    }
}
