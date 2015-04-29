//--------------------------------------------------------------------------------- 
// <copyright file="LookupPartitionResolver.cs" company="Microsoft">
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
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Linq;
    using Microsoft.Azure.Documents.Partitioning;

    /// <summary>
    /// Implement a partition resolver that uses a lookup table to decide how to partition data. Use RangePartitionResolver with single value ranges to provide
    /// a simpler interface.
    /// </summary>
    /// <typeparam name="T">the object type for the partition key.</typeparam>
    public class LookupPartitionResolver<T> : RangePartitionResolver<T> 
        where T : IComparable<T>, IEquatable<T>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LookupPartitionResolver{T}" /> class.
        /// </summary>
        /// <param name="partitionKeyPropertyName">The property name for the partition key.</param>
        /// <param name="partitionMap">The partition map from key to collection.</param>
        public LookupPartitionResolver(string partitionKeyPropertyName, IDictionary<T, string> partitionMap)
            : base(partitionKeyPropertyName, BuildRangePartitionMap(partitionMap))
        {
        }

        /// <summary>
        /// Initialize a range partition map containing a single valued Range for each key in the partitionMap.
        /// </summary>
        /// <param name="partitionMap">The lookup partition map.</param>
        /// <returns>The range partition map.</returns>
        private static IDictionary<Range<T>, string> BuildRangePartitionMap(IDictionary<T, string> partitionMap)
        {
            if (partitionMap == null)
            {
                throw new ArgumentNullException("partitionMap");
            }

            Dictionary<Range<T>, string> rangePartitionMap = new Dictionary<Range<T>, string>();
            foreach (T key in partitionMap.Keys)
            {
                rangePartitionMap[new Range<T>(key)] = partitionMap[key];
            }

            return rangePartitionMap;
        }
    }
}
