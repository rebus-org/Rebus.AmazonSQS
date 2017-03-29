﻿using Amazon;
using Amazon.Runtime;
using Amazon.SQS;
using Rebus.AmazonSQS;
using Rebus.Logging;
using Rebus.Pipeline;
using Rebus.Pipeline.Receive;
using Rebus.Threading;
using Rebus.Timeouts;
using Rebus.Transport;

namespace Rebus.Config
{
    /// <summary>
    /// Configuration extensions for the Amazon Simple Queue Service transport
    /// </summary>
    public static class AmazonSqsConfigurationExtensions
    {
        const string SqsTimeoutManagerText = "A disabled timeout manager was installed as part of the SQS configuration, becuase the transport has native support for deferred messages";

        /// <summary>
        /// Configures Rebus to use Amazon Simple Queue Service as the message transport
        /// </summary>
        public static void UseAmazonSqs(this StandardConfigurer<ITransport> configurer, AWSCredentials credentials, AmazonSQSConfig config, string inputQueueAddress, AmazonSQSTransportOptions options = null)
        {
            configurer.Register(c =>
            {
                var rebusLoggerFactory = c.Get<IRebusLoggerFactory>();
                var asyncTaskFactory = c.Get<IAsyncTaskFactory>();

                return new AmazonSqsTransport(inputQueueAddress, credentials, config, rebusLoggerFactory, asyncTaskFactory, options);
            });

            configurer
                .OtherService<IPipeline>()
                .Decorate(p =>
                {
                    var pipeline = p.Get<IPipeline>();

                    return new PipelineStepRemover(pipeline)
                        .RemoveIncomingStep(s => s.GetType() == typeof(HandleDeferredMessagesStep));
                });

            configurer.OtherService<ITimeoutManager>().Register(c => new DisabledTimeoutManager(), description: SqsTimeoutManagerText);
        }

        /// <summary>
        /// Configures Rebus to use Amazon Simple Queue Service as the message transport
        /// </summary>
        public static void UseAmazonSqs(this StandardConfigurer<ITransport> configurer, string accessKeyId, string secretAccessKey, RegionEndpoint reguEndpoint, string inputQueueAddress, AmazonSQSTransportOptions options = null)
        {
            UseAmazonSqs(configurer, accessKeyId, secretAccessKey, new AmazonSQSConfig { RegionEndpoint = reguEndpoint }, inputQueueAddress, options);
        }

        /// <summary>
        /// Configures Rebus to use Amazon Simple Queue Service as the message transport
        /// </summary>
        public static void UseAmazonSqs(this StandardConfigurer<ITransport> configurer, string accessKeyId, string secretAccessKey, AmazonSQSConfig config, string inputQueueAddress, AmazonSQSTransportOptions options = null)
        {
            UseAmazonSqs(configurer, new BasicAWSCredentials(accessKeyId, secretAccessKey), config, inputQueueAddress, options);
        }

        /// <summary>
        /// Configures Rebus to use Amazon Simple Queue Service as the message transport
        /// </summary>
        public static void UseAmazonSqsAsOneWayClient(this StandardConfigurer<ITransport> configurer, string accessKeyId, string secretAccessKey, RegionEndpoint regionEndpoint, AmazonSQSTransportOptions options = null)
        {
            UseAmazonSqsAsOneWayClient(configurer, accessKeyId, secretAccessKey, new AmazonSQSConfig { RegionEndpoint = regionEndpoint }, options);
        }

        /// <summary>
        /// Configures Rebus to use Amazon Simple Queue Service as the message transport
        /// </summary>
        public static void UseAmazonSqsAsOneWayClient(this StandardConfigurer<ITransport> configurer, string accessKeyId, string secretAccessKey, AmazonSQSConfig amazonSqsConfig, AmazonSQSTransportOptions options = null)
        {
            UseAmazonSqsAsOneWayClient(configurer, new BasicAWSCredentials(accessKeyId, secretAccessKey), amazonSqsConfig, options);
        }

        /// <summary>
        /// Configures Rebus to use Amazon Simple Queue Service as the message transport
        /// </summary>
        static void UseAmazonSqsAsOneWayClient(StandardConfigurer<ITransport> configurer, AWSCredentials credentials, AmazonSQSConfig amazonSqsConfig, AmazonSQSTransportOptions options = null)
        {
            configurer.Register(c =>
            {
                var rebusLoggerFactory = c.Get<IRebusLoggerFactory>();
                var asyncTaskFactory = c.Get<IAsyncTaskFactory>();

                return new AmazonSqsTransport(null, credentials, amazonSqsConfig, rebusLoggerFactory, asyncTaskFactory, options);
            });

            OneWayClientBackdoor.ConfigureOneWayClient(configurer);
        }
    }
}
