using System;
using Amazon.Runtime;
using Amazon.SQS;
using Rebus.Bus;
using Rebus.Transport;

namespace Rebus.Config
{
    /// <summary>
    /// Holds all of the exposed options which can be applied when using the SQS transport.
    /// </summary>
    public class AmazonSQSTransportOptions
    {
        const string ClientContextKey = "SQS_Client";

        /// <summary>
        /// Sets the WaitTimeSeconds on the ReceiveMessage. The default setting is 1, which enables long
        /// polling for a single second. The number of seconds can be set up to 20 seconds. 
        /// In case no long polling is desired, then set the value to 0.
        /// </summary>
        public int ReceiveWaitTimeSeconds { get; set; }

        /// <summary>
        /// Configures whether SQS's built-in deferred messages mechanism is to be used when you <see cref="IBus.Defer"/> messages.
        /// Defaults to <code>true</code>.
        /// Please note that SQS's mechanism is only capably of deferring messages up 900 seconds, so you might need to
        /// set <see cref="UseNativeDeferredMessages"/> to <code>false</code> and then use a "real" timeout manager like e.g.
        /// one that uses SQL Server to store timeouts.
        /// </summary>
        public bool UseNativeDeferredMessages { get; set; }

        /// <summary>
        /// Configures whether Rebus is in control to create queues or not. If set to false, Rebus expects that the queue's are already created. 
        /// Defaults to <code>true</code>.
        /// </summary>
        public bool CreateQueues { get; set; }

        /// <summary>
        /// Default constructor of the exposed SQS transport options.
        /// </summary>
        public AmazonSQSTransportOptions()
        {
            ReceiveWaitTimeSeconds = 1;
            UseNativeDeferredMessages = true;
            CreateQueues = true;

            GetOrCreateClient = (context, credentials, config) =>
            {
                return context.GetOrAdd(ClientContextKey, () =>
                {
                    var amazonSqsClient = new AmazonSQSClient(credentials, config);
                    context.OnDisposed(amazonSqsClient.Dispose);
                    return amazonSqsClient;
                });
            };
        }

        /// <summary>
        /// Function that is getting or creating the <cref type="IAmazonSQS"/> object that will be used for SQS.
        /// </summary>
        /// <returns>The <cref type="IAmazonSQS"/> object to be used for SQS.</returns>
        public Func<ITransactionContext, AWSCredentials, AmazonSQSConfig, IAmazonSQS> GetOrCreateClient;
    }
}
