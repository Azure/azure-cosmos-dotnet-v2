namespace DocumentDB.ChangeFeedProcessor
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;

    internal static class ParallelHelper
    {
        public static async Task ForEachAsync<TSource>(this IEnumerable<TSource> source, Func<TSource, Task> resultProcessor, int maxParallelTaskCount = 0, CancellationToken cancellationToken = new CancellationToken())
        {
            if (maxParallelTaskCount <= 0) maxParallelTaskCount = 100;

            List<Exception> exceptions = new List<Exception>();
            bool isCancelled = false;

            using (var resourceTracker = new SemaphoreSlim(maxParallelTaskCount, maxParallelTaskCount))
            {
                List<Task> tasks = new List<Task>();
                foreach (var item in source)
                {
                    await resourceTracker.WaitAsync(cancellationToken);

                    if (cancellationToken.IsCancellationRequested)
                    {
                        resourceTracker.Release();
                        isCancelled = true;
                        break;
                    }

                    TraceLog.Verbose(string.Format("ForEachAsync: starting task for item '{0}'...", item));
                    Task task = ProcessOneAsync(item, resultProcessor, resourceTracker, exceptions);
                    TraceLog.Verbose(string.Format("ForEachAsync: task for item '{0}' started.", item));

                    tasks.Add(task);
                }

                TraceLog.Verbose("ForEachAsync: waiting for tasks to finish...");
                await Task.WhenAll(tasks.ToArray());
                TraceLog.Verbose("ForEachAsync: done waiting for tasks to finish.");

                if (isCancelled)
                {
                    throw new TaskCanceledException("ParallelHelper.ForEachAsync was cancelled.");
                }
                else if (exceptions.Count != 0)
                {
                    throw new AggregateException(exceptions);
                }
            }
        }

        private static async Task ProcessOneAsync<TSource>(TSource item, Func<TSource, Task> resultProcessor, SemaphoreSlim resourceTracker, List<Exception> exceptions)
        {
            Debug.Assert(resultProcessor != null);
            Debug.Assert(resourceTracker != null);
            Debug.Assert(exceptions != null);

            try
            {
                await resultProcessor(item);
            }
            catch (Exception ex)
            {
                TraceLog.Warning(string.Format("ForEachAsync: exception : {0}", ex));
                lock (exceptions)
                {
                    exceptions.Add(ex);
                }
            }
            finally
            {
                resourceTracker.Release();
                TraceLog.Verbose(string.Format("ForEachAsync: task for item '{0}' finished.", item));
            }
        }
    }
}
