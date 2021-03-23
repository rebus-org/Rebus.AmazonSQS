using System;

namespace Rebus.AmazonSQS.Tests
{
    /// <summary>
    /// DELETES the queue when it is disposed
    /// </summary>
    class QueueDeleter : IDisposable
    {
        readonly string _queueName;

        public QueueDeleter(string queueName) => _queueName = queueName;

        public void Dispose()
        {
            using var transport = AmazonSqsTransportFactory.CreateTransport(_queueName, TimeSpan.FromMinutes(5));
            
            transport.DeleteQueue();
        }
    }
}