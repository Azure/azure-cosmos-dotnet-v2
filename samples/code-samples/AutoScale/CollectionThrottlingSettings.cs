using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocumentDB.Samples.AutoScale
{
    /// <summary>
    /// Setting for configuring throttling properties
    /// </summary>
    public class CollectionThrottlingSettings
    {
        /// <summary>
        /// Gets or sets the throughput to be increased on throttle.
        /// </summary>
        /// <value>
        /// The throughput increase on throttle.
        /// </value>
        public int ThroughputIncreaseOnThrottle { get; }

        /// <summary>
        /// Gets or sets the maximum collection throughput.
        /// </summary>
        /// <value>
        /// The maximum collection through put.
        /// </value>
        public int MaxCollectionThroughPut { get; }

        /// <summary>
        /// Gets or sets the reset time in seconds after which collection throughput can be increased again.
        /// </summary>
        /// <value>
        /// The throttling reset time in seconds.
        /// </value>
        public int ThrottlingResetTimeInSeconds { get; }

        /// <summary>
        /// Gets or sets the minimum number of times throttling needs to occur to increase collection throughput.
        /// </summary>
        /// <value>
        /// The minimum throttling instances.
        /// </value>
        public int MinThrottlingInstances { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CollectionThrottlingSettings"/> class.
        /// </summary>
        /// <param name="throughputIncreaseOnThrottle">The throughput increase on throttle.</param>
        /// <param name="maxCollectionThroughPut">The maximum collection through put.</param>
        /// <param name="resetTimeInSeconds">The reset time in seconds.</param>
        /// <param name="throttlingThreshold">The throttling threshold.</param>
        public CollectionThrottlingSettings(int throughputIncreaseOnThrottle, int maxCollectionThroughPut, int resetTimeInSeconds, int throttlingThreshold)
        {
            this.MaxCollectionThroughPut = maxCollectionThroughPut < 400 ? 400 : maxCollectionThroughPut;
            this.MinThrottlingInstances = throttlingThreshold < 1 ? 1 : throttlingThreshold;
            this.ThrottlingResetTimeInSeconds = resetTimeInSeconds < 1 ? 1 : resetTimeInSeconds;
            this.ThroughputIncreaseOnThrottle = throughputIncreaseOnThrottle < 100 ? 100 :throughputIncreaseOnThrottle;
        }
    }
}
