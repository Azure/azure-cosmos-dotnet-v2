//---------------------------------------------------------------------------------
// <copyright file="DocumentFeedObserver.cs" company="Microsoft">
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

namespace ChangeFeedProcessor
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.ChangeFeedProcessor.FeedProcessing;

    /// <summary>
    /// This class implements the IChangeFeedObserver interface and is used to observe
    /// changes from the change feed. ChangeFeedEventHost will create as many instances of
    /// this class as needed.
    /// </summary>
    public class DocumentFeedObserver : IChangeFeedObserver
    {
        private static int totalDocs = 0;

        /// <summary>
        /// Initializes a new instance of the <see cref="DocumentFeedObserver" /> class.
        /// </summary>
        public DocumentFeedObserver()
        {

        }

        /// <summary>
        /// Called when change feed observer is opened; this function prints out the
        /// observer's partition key id.
        /// </summary>
        /// <param name="context">The context specifying the partition for this observer, etc.</param>
        /// <returns>A Task to allow asynchronous execution</returns>
        public Task OpenAsync(IChangeFeedObserverContext context)
        {
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("Observer opened for partition Key Range: {0}", context.PartitionKeyRangeId);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Called when change feed observer is closed; this function prints out the
        /// observer's partition key id and reason for shut down.
        /// </summary>
        /// <param name="context">The context specifying the partition for this observer, etc.</param>
        /// <param name="reason">Specifies the reason the observer is closed.</param>
        /// <returns>A Task to allow asynchronous execution</returns>
        public Task CloseAsync(IChangeFeedObserverContext context, ChangeFeedObserverCloseReason reason)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("Observer closed, {0}", context.PartitionKeyRangeId);
            Console.WriteLine("Reason for shutdown, {0}", reason);
            return Task.CompletedTask;
        }


        /// <summary>
        /// Called when there is a batch of changes to be processed.
        /// </summary>
        /// <param name="context">The context specifying the partition for this observer, etc.</param>
        /// <param name="docs">The documents changed.</param>
        /// <param name="cancellationToken">Token to signal that the parition processing is going to finish.</param>
        /// <returns>A Task to allow asynchronous execution</returns>
        public Task ProcessChangesAsync(IChangeFeedObserverContext context, IReadOnlyList<Document> docs, CancellationToken cancellationToken)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Change feed: PartitionId {0} total {1} doc(s)", context.PartitionKeyRangeId, Interlocked.Add(ref totalDocs, docs.Count));
            foreach (Document doc in docs)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(doc.Id.ToString());
            }

            return Task.CompletedTask;
        }
    }
}
