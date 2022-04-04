using System;
using Amazon.SQS;
using Rebus.Bus;
using Rebus.Transport;

namespace Rebus.Config;

/// <summary>
/// Holds all of the exposed options which can be applied when using the SQS transport.
/// </summary>
public class AmazonSQSTransportOptions
{
    ushort _messageBatchSize = 10;

    /// <summary>
    /// Sets the WaitTimeSeconds on the ReceiveMessage. The default setting is 1, which enables long
    /// polling for a single second. The number of seconds can be set up to 20 seconds. 
    /// In case no long polling is desired, then set the value to 0.
    /// </summary>
    public int ReceiveWaitTimeSeconds { get; set; } = 1;

    /// <summary>
    /// Configures whether SQS's built-in deferred messages mechanism is to be used when you <see cref="IBus.Defer"/> messages.
    /// Defaults to <code>true</code>.
    /// Please note that SQS's mechanism is only capably of deferring messages up 900 seconds, so you might need to
    /// set <see cref="UseNativeDeferredMessages"/> to <code>false</code> and then use a "real" timeout manager like e.g.
    /// one that uses SQL Server to store timeouts.
    /// </summary>
    public bool UseNativeDeferredMessages { get; set; } = true;

    /// <summary>
    /// Configures whether Rebus is in control to create queues or not. If set to false, Rebus expects that the queue's are already created. 
    /// Defaults to <code>true</code>.
    /// </summary>
    public bool CreateQueues { get; set; } = true;

    /// <summary>
    /// Sets the MessageBatchSize for sending batch messages to SQS. 
    /// Value of BatchSize can be set up to 10.
    /// Defaults to <code>10</code>.
    /// </summary>
    public ushort MessageBatchSize
    {
        get => _messageBatchSize;
        set
        {
            if (value is < 1 or > 10)
            {
                throw new ArgumentOutOfRangeException(nameof(MessageBatchSize), value, "MessageBatchSize must be between 1 and 10.");
            }

            _messageBatchSize = value;
        }
    }

    /// <summary>
    /// Provides the ability to customize the visibility timeout set to a message when it is aborted. If this one is not set,
    /// the visibility timeout defaults to 0, which means that failed messages will be immediately retried.
    /// Return a TimeSpan > 0 means from this function means that message delivery will automatically be delayed after each failure to consume the message.
    /// </summary>
    public Func<ITransactionContext, TimeSpan> GetVisibilityTimeoutOnAbort { get; set; }

    /// <summary>
    /// Optional function that gets a new instance of <see cref="IAmazonSQS"/>. Set this if you wish to override how <see cref="IAmazonSQS"/> is retrieved.
    /// </summary>
    public Func<IAmazonSQS> ClientFactory;
}