//--------------------------------------------------------------------------------- 
// <copyright file="TransitionHashPartitionResolver.cs" company="Microsoft">
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
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Documents.Linq;
    using Microsoft.Azure.Documents.Partitioning;

    /// <summary>
    /// Creates a partition resolver that handles routing of reads and creates during transitions between partitioning configurations.
    /// Most commonly when you add an additional collection to the hash ring. This also shows how you can manage the policy for 
    /// handling reads during migration (read both old and new partitions, or throw a retry-able error, etc.)
    /// </summary>
    /// <seealso cref="TransitionReadMode"/>
    /// <seealso cref="DocumentClientHashPartitioningManager"/>
    public class TransitionHashPartitionResolver : IPartitionResolver
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TransitionHashPartitionResolver" /> class.
        /// </summary>
        /// <param name="current">The current IPartitionResolver.</param>
        /// <param name="next">The next IPartitionResolver.</param>
        /// <param name="readMode">How to handle read requests during transition.</param>
        public TransitionHashPartitionResolver(
            IPartitionResolver current, 
            IPartitionResolver next, 
            TransitionReadMode readMode = TransitionReadMode.ReadBoth)
        {
            this.CurrentResolver = current;
            this.NextResolver = next;
        }

        /// <summary>
        /// Gets the current IPartitionResolver to migrate from.
        /// </summary>
        public IPartitionResolver CurrentResolver { get; private set; }

        /// <summary>
        /// Gets the next IPartitionResolver to migrate to.
        /// </summary>
        public IPartitionResolver NextResolver { get; private set; }

        /// <summary>
        /// Gets the <see cref="TransitionReadMode"/> to handle reads during transitions.
        /// </summary>
        public TransitionReadMode ReadMode { get; private set; }

        /// <summary>
        /// Returns the collections to read for a partitionKey, based on the TransitionReadMode.
        /// </summary>
        /// <param name="partitionKey">The partition key for the read.</param>
        /// <returns>The list of collections.</returns>
        public IEnumerable<string> ResolveForRead(object partitionKey)
        {
            var allPartitions = new HashSet<string>();
            switch (this.ReadMode)
            {
                case TransitionReadMode.ReadCurrent:
                    foreach (string collectionLink in this.CurrentResolver.ResolveForRead(partitionKey))
                    {
                        allPartitions.Add(collectionLink);
                    }

                    break;

                case TransitionReadMode.ReadNext:
                    foreach (string collectionLink in this.NextResolver.ResolveForRead(partitionKey))
                    {
                        allPartitions.Add(collectionLink);
                    }

                    break;

                case TransitionReadMode.ReadBoth:
                    foreach (string collectionLink in this.CurrentResolver.ResolveForRead(partitionKey))
                    {
                        allPartitions.Add(collectionLink);
                    }

                    foreach (string collectionLink in this.NextResolver.ResolveForRead(partitionKey))
                    {
                        allPartitions.Add(collectionLink);
                    }

                    break;

                case TransitionReadMode.None:
                    throw new InvalidOperationException("Partitions are being migrated. Retry request with a finite delay.");
            }

            return allPartitions;
        }

        /// <summary>
        /// Returns the collection to create this document. Returns the last collection.
        /// </summary>
        /// <param name="partitionKey">The partition key for the create.</param>
        /// <returns>The collection to create in.</returns>
        public string ResolveForCreate(object partitionKey)
        {
            return this.NextResolver.ResolveForCreate(partitionKey);
        }

        /// <summary>
        /// Returns the partition key for the document.
        /// </summary>
        /// <param name="document">The document to locate.</param>
        /// <returns>The partition key.</returns>
        public object GetPartitionKey(object document)
        {
            return this.NextResolver.GetPartitionKey(document);
        }
    }
}
