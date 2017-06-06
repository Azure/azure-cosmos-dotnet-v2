namespace DocumentDB.ChangeFeedProcessor
{
    using System;
    using System.Runtime.Serialization;

#if NETFX
    [Serializable]
#endif
    class LeaseLostException : Exception
    {
        /// <summary>Initializes a new instance of the <see cref="DocumentDB.ChangeFeedProcessor.LeaseLostException" /> class using default values.</summary>
        public LeaseLostException()
        {
        }

        public LeaseLostException(Lease lease)
        {
            this.Lease = lease;
        }

        public LeaseLostException(Lease lease, Exception innerException, bool isGone = false)
            : base(null, innerException)
        {
            this.Lease = lease;
            this.IsGone = isGone;
        }

        public LeaseLostException(string message)
            : base(message)
        {
        }

        public LeaseLostException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

#if NETFX
        protected LeaseLostException(SerializationInfo info, StreamingContext context) :
            base(info, context)
        {
            this.Lease = (Lease)info.GetValue("Lease", typeof(Lease));
        }
#endif

        public Lease Lease { get; private set; }

        public bool IsGone { get; private set; }

#if NETFX
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);

            if (Lease != null)
            {
                info.AddValue("Lease", this.Lease);
            }
        }
#endif
    }
}
