namespace DocumentDB.ChangeFeedProcessor
{
    enum StatusCode
    {
        /// <summary>
        /// The operation is attempting to act on a resource that no longer exists. For example, the resource may have already been deleted.
        /// </summary>
        NotFound = 404,

        /// <summary>
        /// The id provided for a resource on a PUT or POST operation has been taken by an existing resource. 
        /// Use another id for the resource to resolve this issue. For partitioned collections, id must be unique within all documents with the same partition key value.
        /// </summary>
        Conflict = 409,

        /// <summary>
        /// The resource is gone.
        /// </summary>
        Gone = 410,
        
        /// <summary>
        /// The operation specified an eTag that is different from the version available at the server, i.e., an optimistic concurrency error. 
        /// Retry the request after reading the latest version of the resource and updating the eTag on the request.
        /// </summary>
        PreconditionFailed = 412,

        /// <summary>
        /// The collection has exceeded the provisioned throughput limit. Retry the request after the server specified retry after duration. 
        /// For more information on DocumentDB performance levels, see DocumentDB levels.
        /// </summary>
        TooManyRequests = 429,

        /// <summary>
        /// The operation could not be completed because the service was unavailable. This could happen due to network connectivity or service availability issues. 
        /// It is safe to retry the operation. If the issue persists, please contact support.
        /// </summary>
        ServiceUnavailable = 503,
    }
}
