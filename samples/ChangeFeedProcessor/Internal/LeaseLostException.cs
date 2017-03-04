namespace DocumentDB.ChangeFeedProcessor
{
    using System;
    using System.Runtime.Serialization;

    [Serializable]
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

        public LeaseLostException(Lease lease, Exception innerException)
            : base(null, innerException)
        {
            this.Lease = lease;
        }

        public LeaseLostException(string message)
            : base(message)
        {
        }

        public LeaseLostException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected LeaseLostException(SerializationInfo info, StreamingContext context) :
            base(info, context)
        {
            this.Lease = (Lease)info.GetValue("Lease", typeof(Lease));
        }

        public Lease Lease { get; private set; }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);

            if (Lease != null)
            {
                info.AddValue("Lease", this.Lease);
            }
        }
    }
}
