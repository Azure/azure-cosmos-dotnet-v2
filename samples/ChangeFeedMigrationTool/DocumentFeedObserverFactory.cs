//--------------------------------------------------------------------------------- 
// <copyright file="DocumentFeedObserverFactory.cs" company="Microsoft">
// Microsoft (R)  Azure SDK 
// Software Development Kit 
//  
// Copyright (c) Microsoft Corporation. All rights reserved.   
// 
// THIS CODE AND INFORMATION ARE PROVIDED "AS IS" WITHOUT WARRANTY OF ANY KIND,  
// EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE IMPLIED WARRANTIES  
// OF MERCHANTABILITY AND/OR FITNESS FOR A PARTICULAR PURPOSE.  
// </copyright>
//---------------------------------------------------------------------------------

namespace ChangeFeedMigrationSample
{
    using Microsoft.Azure.Documents.ChangeFeedProcessor;
    using Microsoft.Azure.Documents.Client;

    /// <summary>
    /// Factory class to create instance of document feed observer. 
    /// </summary>
    public class DocumentFeedObserverFactory : IChangeFeedObserverFactory
    {
        private DocumentClient client;
        private DocumentCollectionInfo collectionInfo;

        public DocumentFeedObserverFactory(DocumentCollectionInfo destCollInfo, DocumentClient destClient)
        {
            this.collectionInfo = destCollInfo;
            this.client = destClient;
        }

        public IChangeFeedObserver CreateObserver()
        {
            DocumentFeedObserver newObserver = new DocumentFeedObserver(this.client, this.collectionInfo);
            return newObserver;
        }
    }
}