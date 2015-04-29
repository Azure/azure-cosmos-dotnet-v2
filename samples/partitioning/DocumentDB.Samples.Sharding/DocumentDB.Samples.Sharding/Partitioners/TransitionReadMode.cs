//--------------------------------------------------------------------------------- 
// <copyright file="TransitionReadMode.cs" company="Microsoft">
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
    /// <summary>
    /// Specifies how to handle requests to partitions in transition.
    /// </summary>
    public enum TransitionReadMode
    {
        /// <summary>
        /// Perform reads using the current PartitionResolver.
        /// </summary>
        ReadCurrent,

        /// <summary>
        /// Perform reads using the targeted PartitionResolver.
        /// </summary>
        ReadNext,

        /// <summary>
        /// Perform reads using partitions from both current and targeted PartitionResolvers, and 
        /// return the union of results.
        /// </summary>
        ReadBoth,

        /// <summary>
        /// Throw an transient Exception when reads are attempted during migration.
        /// </summary>
        None
    }
}
