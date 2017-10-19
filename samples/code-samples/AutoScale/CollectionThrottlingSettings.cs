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
        public int ThroughputIncreaseOnThrottle { get; set; }

        /// <summary>
        /// Gets or sets the maximum collection throughput.
        /// </summary>
        /// <value>
        /// The maximum collection through put.
        /// </value>
        public int MaxCollectionThroughPut { get; set; }

        /// <summary>
        /// Gets or sets the reset time in seconds after which collection throughput can be increased again.
        /// </summary>
        /// <value>
        /// The throttling reset time in seconds.
        /// </value>
        public int ThrottlingResetTimeInSeconds { get; set; }

        /// <summary>
        /// Gets or sets the minimum number of times throttling needs to occur to increase collection throughput.
        /// </summary>
        /// <value>
        /// The minimum throttling instances.
        /// </value>
        public int MinThrottlingInstances { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CollectionThrottlingSettings"/> class.
        /// </summary>
        /// <param name="throughputIncreaseOnThrottle">The throughput increase on throttle.</param>
        /// <param name="maxCollectionThroughPut">The maximum collection through put.</param>
        /// <param name="resetTimeInSeconds">The reset time in seconds.</param>
        /// <param name="throttlingThreshold">The throttling threshold.</param>
        public CollectionThrottlingSettings(int throughputIncreaseOnThrottle, int maxCollectionThroughPut, int resetTimeInSeconds, int throttlingThreshold)
        {
            this.MaxCollectionThroughPut = maxCollectionThroughPut;
            this.MinThrottlingInstances = throttlingThreshold;
            this.ThrottlingResetTimeInSeconds = resetTimeInSeconds;
            this.ThroughputIncreaseOnThrottle = throughputIncreaseOnThrottle;
        }
    }
}
