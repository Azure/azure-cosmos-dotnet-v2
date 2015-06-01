namespace DocumentDB.Samples.Partitioning.Partitioners
{
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Documents.Partitioning;
    using System.Collections.Generic;

    /// <summary>
    /// Implement a partition resolver that uses a lookup table to decide how to partition data. Uses an inner partition resolver 
    /// to support enumerations as the type parameter. 
    /// </summary>
    /// <typeparam name="T">Type for the enumeration.</typeparam>
    public class EnumLookupPartitionResolver<T> : IPartitionResolver
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EnumLookupPartitionResolver{T}" /> class.
        /// </summary>
        /// <param name="partitionKeyPropertyName">The property name for the partition key.</param>
        /// <param name="partitionMap">The partition map from key to collection.</param>
        public EnumLookupPartitionResolver(string partitionKeyPropertyName, IDictionary<T, string> partitionMap)
        {
            var innerPartitionMap = new Dictionary<Range<string>, string>();
            foreach (T key in partitionMap.Keys)
            {
                innerPartitionMap[new Range<string>(key.ToString())] = partitionMap[key];
            }

            this.InnerRangePartitionResolver = new RangePartitionResolver<string>(partitionKeyPropertyName, innerPartitionMap);
        }

        /// <summary>
        /// Gets the inner LookupPartitionResolver used for the enumeration.
        /// </summary>
        public RangePartitionResolver<string> InnerRangePartitionResolver { get; private set; }

        /// <summary>
        /// Returns the collections to read for a document. Here we return all collections.
        /// </summary>
        /// <param name="partitionKey">The partition key for the read.</param>
        /// <returns>The list of collections.</returns>
        public IEnumerable<string> ResolveForRead(object partitionKey)
        {
            return this.InnerRangePartitionResolver.ResolveForRead(partitionKey);
        }

        /// <summary>
        /// Returns the collection to create this document. Returns the last collection.
        /// </summary>
        /// <param name="partitionKey">The partition key for the create.</param>
        /// <returns>The collection to create in.</returns>
        public string ResolveForCreate(object partitionKey)
        {
            return this.InnerRangePartitionResolver.ResolveForCreate(partitionKey);
        }

        /// <summary>
        /// Returns the partition key for the document. Bypass by returning null.
        /// </summary>
        /// <param name="document">The document to locate.</param>
        /// <returns>The partition key.</returns>
        public object GetPartitionKey(object document)
        {
            return this.InnerRangePartitionResolver.GetPartitionKey(document);
        }
    }
}
