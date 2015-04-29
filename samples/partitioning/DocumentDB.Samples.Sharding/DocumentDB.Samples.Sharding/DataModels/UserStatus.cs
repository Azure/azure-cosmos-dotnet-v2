//--------------------------------------------------------------------------------- 
// <copyright file="UserStatus.cs" company="Microsoft">
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

namespace DocumentDB.Samples.Sharding.DataModels
{
    /// <summary>
    /// Represents the status of a user.
    /// </summary>
    public enum UserStatus
    {
        /// <summary>
        /// The user is available.
        /// </summary>
        Available,

        /// <summary>
        /// The user is busy.
        /// </summary>
        Busy,

        /// <summary>
        /// The user has marked their status as "Do Not Disturb".
        /// </summary>
        DoNotDisturb,

        /// <summary>
        /// The user has marked their status as "Be Right Back".
        /// </summary>
        BeRightBack,

        /// <summary>
        /// The user has marked their status as "Off Work".
        /// </summary>
        OffWork,

        /// <summary>
        /// The user has marked their status as "Appear Away".
        /// </summary>
        AppearAway
    }
}
