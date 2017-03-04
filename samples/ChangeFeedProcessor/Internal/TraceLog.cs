namespace DocumentDB.ChangeFeedProcessor
{
    using System;
    using System.Diagnostics;
    using System.Threading;

    class TraceLog
    {
        private static TraceSource traceSource = new TraceSource("ChangeFeedEventHost");
        private static int id = 0;

        public static void Verbose(string message)
        {
            TraceEvent(TraceEventType.Verbose, message);
        }

        public static void Informational(string message)
        {
            TraceEvent(TraceEventType.Information, message);
        }

        public static void Warning(string message)
        {
            TraceEvent(TraceEventType.Warning, message);
        }

        public static void Error(string message)
        {
            TraceEvent(TraceEventType.Error, message);
        }

        public static void Exception(Exception ex)
        {
            Error(GetExceptionText(ex));
        }

        private static void TraceEvent(TraceEventType eventType, string message)
        {
            traceSource.TraceEvent(eventType, Interlocked.Increment(ref id), string.Format("{0}: {1}", DateTime.Now, message));
        }

        private static string GetExceptionText(Exception ex)
        {
            string message = string.Empty;
            try
            {
                message = string.Format("Exception: {0}:{1}\n{2}", ex.GetType(), ex.Message, ex.StackTrace);
            }
            catch
            {
                message = "Error formatting exception.";
            }

            return message;
        }
    }
}
