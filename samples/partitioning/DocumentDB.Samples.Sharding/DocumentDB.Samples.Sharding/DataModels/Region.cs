//--------------------------------------------------------------------------------- 
// <copyright file="Region.cs" company="Microsoft">
// Microsoft (R) Azure DocumentDB SDK 
// Software Development Kit 
//  
// Copyright (c) Microsoft Corporation. All rights reserved.   
// 
// THIS CODE AND INFORMATION ARE PROVIDED "AS IS" WITHOUT WARRANTY OF ANY KIND,  
// EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE IMPLIED WARRANTIES  
// OF MERCHANTABILITY AND/OR FITNESS FOR A PARTICULAR PURPOSE.  
// </copyright>
//--------------------------------------------------------------------------------- v

namespace DocumentDB.Samples.Sharding.DataModels
{
    using System;

    /// <summary>
    /// The geographic region a user is in.
    /// </summary>
    public enum Region : int
    {
        /// <summary>
        /// The US East region.
        /// </summary>
        UnitedStatesEast,

        /// <summary>
        /// The West US region.
        /// </summary>
        UnitedStatesWest,

        /// <summary>
        /// The Europe region.
        /// </summary>
        Europe,

        /// <summary>
        /// The Asia Pacific region.
        /// </summary>
        AsiaPacific,

        /// <summary>
        /// Other regions.
        /// </summary>
        Other
    }
}
