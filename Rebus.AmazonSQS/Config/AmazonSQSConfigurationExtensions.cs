using System;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.SQS;
using Rebus.AmazonSQS;
using Rebus.Config;
using Rebus.Logging;
using Rebus.Pipeline;
using Rebus.Pipeline.Receive;
using Rebus.Threading;
using Rebus.Timeouts;
using Rebus.Transport;
// ReSharper disable ArgumentsStyleNamedExpression

namespace Rebus.Config
{
    /// <summary>
    /// Configuration extensions for the Amazon Simple Queue Service transport
    /// </summary>
    public static class AmazonSQSConfigurationExtensions
    {
        const string SqsTimeoutManagerText = "A disabled timeout manager was installed as part of the SQS configuration, becuase the transport has native support for deferred messages";

        /// <summary>
        /// Configures Rebus to use Amazon Simple Queue Service as the message transport
        /// </summary>
        public static void UseAmazonSQS(this StandardConfigurer<ITransport> configurer, AWSCredentials credentials, AmazonSQSConfig config, string inputQueueAddress, AmazonSQSTransportOptions options = null, AmazonS3Config s3Config = null, AmazonS3Options s3Options = null)
        {
            Configure(configurer, credentials, config, inputQueueAddress, options ?? new AmazonSQSTransportOptions(), s3Config, s3Options);
        }

        /// <summary>
        /// Configures Rebus to use Amazon Simple Queue Service as the message transport
        /// </summary>
        public static void UseAmazonSQS(this StandardConfigurer<ITransport> configurer, string accessKeyId, string secretAccessKey, RegionEndpoint regionEndpoint, string inputQueueAddress, AmazonSQSTransportOptions options = null)
        {
            var config = new AmazonSQSConfig { RegionEndpoint = regionEndpoint };
            var credentials = new BasicAWSCredentials(accessKeyId, secretAccessKey);

            Configure(configurer, credentials, config, inputQueueAddress, options ?? new AmazonSQSTransportOptions(), null, null);
        }

        /// <summary>
        /// Configures Rebus to use Amazon Simple Queue Service as the message transport
        /// </summary>
        public static void UseAmazonSQS(this StandardConfigurer<ITransport> configurer, string accessKeyId, string secretAccessKey, AmazonSQSConfig config, string inputQueueAddress, AmazonSQSTransportOptions options = null, AmazonS3Config s3Config = null, AmazonS3Options s3Options = null)
        {
            var credentials = new BasicAWSCredentials(accessKeyId, secretAccessKey);

            Configure(configurer, credentials, config, inputQueueAddress, options ?? new AmazonSQSTransportOptions(), s3Config, s3Options);
        }

        /// <summary>
        /// Configures Rebus to use Amazon Simple Queue Service as the message transport
        /// </summary>
        public static void UseAmazonSQSAsOneWayClient(this StandardConfigurer<ITransport> configurer, string accessKeyId, string secretAccessKey, RegionEndpoint regionEndpoint, AmazonSQSTransportOptions options = null)
        {
            var credentials = new BasicAWSCredentials(accessKeyId, secretAccessKey);
            var config = new AmazonSQSConfig { RegionEndpoint = regionEndpoint };

            ConfigureOneWayClient(configurer, credentials, config, options ?? new AmazonSQSTransportOptions(), null, null);
        }

        /// <summary>
        /// Configures Rebus to use Amazon Simple Queue Service as the message transport
        /// </summary>
        public static void UseAmazonSQSAsOneWayClient(this StandardConfigurer<ITransport> configurer, string accessKeyId, string secretAccessKey, AmazonSQSConfig amazonSqsConfig, AmazonSQSTransportOptions options = null, AmazonS3Config s3Config = null, AmazonS3Options s3Options = null)
        {
            var credentials = new BasicAWSCredentials(accessKeyId, secretAccessKey);

            ConfigureOneWayClient(configurer, credentials, amazonSqsConfig, options ?? new AmazonSQSTransportOptions(), s3Config, s3Options);
        }

        /// <summary>
        /// Configures Rebus to use Amazon Simple Queue Service as the message transport
        /// </summary>
        public static void UseAmazonSQSAsOneWayClient(this StandardConfigurer<ITransport> configurer, AWSCredentials credentials, AmazonSQSConfig config, AmazonSQSTransportOptions options = null, AmazonS3Config s3Config = null, AmazonS3Options s3Options = null)
        {
            ConfigureOneWayClient(configurer, credentials, config, options ?? new AmazonSQSTransportOptions(), s3Config, s3Options);
        }

        /// <summary>
        /// Configures Rebus to use Amazon Simple Queue Service as the message transport
        /// </summary>
        public static void UseAmazonSQSAsOneWayClient(this StandardConfigurer<ITransport> configurer, AWSCredentials credentials, RegionEndpoint regionEndpoint, AmazonSQSTransportOptions options = null)
        {
            var config = new AmazonSQSConfig { RegionEndpoint = regionEndpoint };

            ConfigureOneWayClient(configurer, credentials, config, options ?? new AmazonSQSTransportOptions(), null, null);
        }

        static void Configure(StandardConfigurer<ITransport> configurer, AWSCredentials credentials, AmazonSQSConfig config, string inputQueueAddress, AmazonSQSTransportOptions options, AmazonS3Config s3Config, AmazonS3Options s3Options)
        {
            if (configurer == null) throw new ArgumentNullException(nameof(configurer));
            if (credentials == null) throw new ArgumentNullException(nameof(credentials));
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (inputQueueAddress == null) throw new ArgumentNullException(nameof(inputQueueAddress));
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (s3Config != null && s3Options == null) throw new ArgumentNullException(nameof(s3Options));

            configurer.Register(c =>
            {
                var rebusLoggerFactory = c.Get<IRebusLoggerFactory>();
                var asyncTaskFactory = c.Get<IAsyncTaskFactory>();

                return new AmazonSQSTransport(inputQueueAddress, credentials, config, rebusLoggerFactory, asyncTaskFactory, options, s3Config, s3Options);
            });

            if (options.UseNativeDeferredMessages)
            {
                configurer
                    .OtherService<IPipeline>()
                    .Decorate(p =>
                    {
                        var pipeline = p.Get<IPipeline>();

                        return new PipelineStepRemover(pipeline)
                            .RemoveIncomingStep(s => s.GetType() == typeof(HandleDeferredMessagesStep));
                    });

                configurer.OtherService<ITimeoutManager>()
                    .Register(c => new DisabledTimeoutManager(), description: SqsTimeoutManagerText);
            }
        }

        static void ConfigureOneWayClient(StandardConfigurer<ITransport> configurer, AWSCredentials credentials, AmazonSQSConfig amazonSqsConfig, AmazonSQSTransportOptions options, AmazonS3Config s3Config, AmazonS3Options s3Options)
        {
            if (configurer == null) throw new ArgumentNullException(nameof(configurer));
            if (credentials == null) throw new ArgumentNullException(nameof(credentials));
            if (amazonSqsConfig == null) throw new ArgumentNullException(nameof(amazonSqsConfig));
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (s3Config != null && s3Options == null) throw new ArgumentNullException(nameof(s3Options));

            configurer.Register(c =>
            {
                var rebusLoggerFactory = c.Get<IRebusLoggerFactory>();
                var asyncTaskFactory = c.Get<IAsyncTaskFactory>();

                return new AmazonSQSTransport(null, credentials, amazonSqsConfig, rebusLoggerFactory, asyncTaskFactory, options, s3Config, s3Options);
            });

            OneWayClientBackdoor.ConfigureOneWayClient(configurer);

            if (options.UseNativeDeferredMessages)
            {
                configurer
                    .OtherService<IPipeline>()
                    .Decorate(p =>
                    {
                        var pipeline = p.Get<IPipeline>();

                        return new PipelineStepRemover(pipeline)
                            .RemoveIncomingStep(s => s.GetType() == typeof(HandleDeferredMessagesStep));
                    });

                configurer.OtherService<ITimeoutManager>()
                    .Register(c => new DisabledTimeoutManager(), description: SqsTimeoutManagerText);
            }
        }
    }
}