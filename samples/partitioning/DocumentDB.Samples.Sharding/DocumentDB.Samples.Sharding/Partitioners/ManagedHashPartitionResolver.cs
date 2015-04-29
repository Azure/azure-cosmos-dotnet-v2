//--------------------------------------------------------------------------------- 
// <copyright file="ManagedHashPartitionResolver.cs" company="Microsoft">
// Microsoft (R) Azure DocumentDB SDK 
// Software Development Kit 
//  
// Copyright (c) Microsoft Corporation. All rights reserved.   
// 
// THIS CODE AND INFORMATION ARE PROVIDED "AS IS" WITHOUT WARRANTY OF ANY KIND,  
// EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE IMPLIED WARRANTIES  
// OF MERCHANTABILITY AND/OR FITNESS FOR A PARTICULAR PURPOSE.  
// </copyright>
//--------------------------------------------------------------------------------- 

namespace DocumentDB.Samples.Sharding.Partitioners
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Documents.Partitioning;

    /// <summary>
    /// Implement a "managed" hash partition resolver that creates collections as needed.
    /// </summary>
    public class ManagedHashPartitionResolver : HashPartitionResolver
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ManagedHashPartitionResolver" /> class.
        /// </summary>
        /// <param name="partitionKeyExtractor">The partition key extractor function.</param>
        /// <param name="client">The DocumentDB client instance.</param>
        /// <param name="database">The database to use.</param>
        /// <param name="numberOfCollections">the number of collections.</param>
        /// <param name="hashGenerator">the hash generator.</param>
        /// <param name="collectionSpec">the specification/template to create collections from.</param>
        /// <param name="collectionIdPrefix">the prefix to use for collections.</param>
        public ManagedHashPartitionResolver(
            Func<object, string> partitionKeyExtractor, 
            DocumentClient client,
            Database database,
            int numberOfCollections, 
            IHashGenerator hashGenerator = null,
            DocumentCollectionSpec collectionSpec = null,
            string collectionIdPrefix = "ManagedHashCollection.")
            : base(
            partitionKeyExtractor, 
            GetCollections(client, database, numberOfCollections, collectionIdPrefix, collectionSpec), 
            128,
            hashGenerator)
        {
            this.DocumentCollectionSpec = collectionSpec;
        }

        /// <summary>
        /// Gets the template to create collections from.
        /// </summary>
        public DocumentCollectionSpec DocumentCollectionSpec { get; private set; }

        /// <summary>
        /// Gets the prefix for collection ids (names).
        /// </summary>
        public string CollectionIdPrefix { get; private set; }

        /// <summary>
        /// Gets the number of collections/partitions.
        /// </summary>
        public int PartitionCount 
        { 
            get 
            {
                return this.CollectionLinks.Count();
            }  
        }

        /// <summary>
        /// Gets or creates the collections for the hash resolver.
        /// </summary>
        /// <param name="client">The DocumentDB client instance.</param>
        /// <param name="database">The database to use.</param>
        /// <param name="numberOfCollections">The number of collections.</param>
        /// <param name="collectionIdPrefix">The prefix to use while creating collections.</param>
        /// <param name="spec">The specification/template to use to create collections.</param>
        /// <returns>The list of collection self links.</returns>
        private static List<string> GetCollections(
            DocumentClient client, 
            Database database, 
            int numberOfCollections, 
            string collectionIdPrefix, 
            DocumentCollectionSpec spec)
        {
            var collections = new List<string>();
            for (int i = 0; i < numberOfCollections; i++)
            {
                string collectionId = string.Format("{0}{1}", collectionIdPrefix, i);
                var collection = DocumentClientHelper.GetCollectionAsync(client, database, collectionId, spec).Result;
                collections.Add(collection.SelfLink);
            }

            return collections;
        }
    }
}
