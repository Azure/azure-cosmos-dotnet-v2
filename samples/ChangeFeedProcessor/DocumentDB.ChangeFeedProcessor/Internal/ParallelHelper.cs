namespace DocumentDB.ChangeFeedProcessor
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    internal static class ParallelHelper
    {
        public static Task ForEachAsync<TSource>(this IEnumerable<TSource> source, Func<TSource, Task> worker, int maxParallelTaskCount = 0, CancellationToken cancellationToken = new CancellationToken())
        {
            Debug.Assert(source != null);
            Debug.Assert(worker != null);
            if (maxParallelTaskCount <= 0) maxParallelTaskCount = 100;

            return Task.WhenAll(
                Partitioner.Create(source)
                    .GetPartitions(maxParallelTaskCount)
                    .Select(partition => Task.Run(
                        async delegate
                        {
                            using (partition)
                            {
                                while (partition.MoveNext())
                                {
                                    cancellationToken.ThrowIfCancellationRequested();
                                    await worker(partition.Current);
                                }
                            }
                        })));
        }
    }
}
