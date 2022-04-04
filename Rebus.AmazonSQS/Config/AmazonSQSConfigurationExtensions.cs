using System;
using Amazon;
using Amazon.Runtime;
using Amazon.SQS;
using Rebus.AmazonSQS;
using Rebus.Exceptions;
using Rebus.Logging;
using Rebus.Pipeline;
using Rebus.Pipeline.Receive;
using Rebus.Threading;
using Rebus.Time;
using Rebus.Timeouts;
using Rebus.Transport;
// ReSharper disable ArgumentsStyleNamedExpression
// ReSharper disable UnusedMember.Global

namespace Rebus.Config;

/// <summary>
/// Configuration extensions for the Amazon Simple Queue Service transport
/// </summary>
public static class AmazonSQSConfigurationExtensions
{
    const string SqsTimeoutManagerText = "A disabled timeout manager was installed as part of the SQS configuration, becuase the transport has native support for deferred messages";

    /// <summary>
    /// Configures Rebus to use Amazon Simple Queue Service as the message transport
    /// </summary>
    public static void UseAmazonSQS(this StandardConfigurer<ITransport> configurer, string inputQueueAddress, AmazonSQSTransportOptions options = null)
    {
        Configure(configurer, inputQueueAddress, GetTransportOptions(options, null, null));
    }

    /// <summary>
    /// Configures Rebus to use Amazon Simple Queue Service as the message transport
    /// </summary>
    public static void UseAmazonSQS(this StandardConfigurer<ITransport> configurer, string inputQueueAddress, AmazonSQSConfig config, AmazonSQSTransportOptions options = null)
    {
        Configure(configurer, inputQueueAddress, GetTransportOptions(options, null, config));
    }

    /// <summary>
    /// Configures Rebus to use Amazon Simple Queue Service as the message transport
    /// </summary>
    public static void UseAmazonSQS(this StandardConfigurer<ITransport> configurer, AWSCredentials credentials, AmazonSQSConfig config, string inputQueueAddress, AmazonSQSTransportOptions options = null)
    {
        Configure(configurer, inputQueueAddress, GetTransportOptions(options, credentials, config));
    }

    /// <summary>
    /// Configures Rebus to use Amazon Simple Queue Service as the message transport
    /// </summary>
    public static void UseAmazonSQS(this StandardConfigurer<ITransport> configurer, string accessKeyId, string secretAccessKey, RegionEndpoint regionEndpoint, string inputQueueAddress, AmazonSQSTransportOptions options = null)
    {
        var config = new AmazonSQSConfig { RegionEndpoint = regionEndpoint };
        var credentials = new BasicAWSCredentials(accessKeyId, secretAccessKey);

        Configure(configurer, inputQueueAddress, GetTransportOptions(options, credentials, config));
    }

    /// <summary>
    /// Configures Rebus to use Amazon Simple Queue Service as the message transport
    /// </summary>
    public static void UseAmazonSQS(this StandardConfigurer<ITransport> configurer, string accessKeyId, string secretAccessKey, AmazonSQSConfig config, string inputQueueAddress, AmazonSQSTransportOptions options = null)
    {
        var credentials = new BasicAWSCredentials(accessKeyId, secretAccessKey);

        Configure(configurer, inputQueueAddress, GetTransportOptions(options, credentials, config));
    }

    /// <summary>
    /// Configures Rebus to use Amazon Simple Queue Service as the message transport
    /// </summary>
    public static void UseAmazonSQSAsOneWayClient(this StandardConfigurer<ITransport> configurer, string accessKeyId, string secretAccessKey, RegionEndpoint regionEndpoint, AmazonSQSTransportOptions options = null)
    {
        var credentials = new BasicAWSCredentials(accessKeyId, secretAccessKey);
        var config = new AmazonSQSConfig { RegionEndpoint = regionEndpoint };

        ConfigureOneWayClient(configurer, GetTransportOptions(options, credentials, config));
    }

    /// <summary>
    /// Configures Rebus to use Amazon Simple Queue Service as the message transport
    /// </summary>
    public static void UseAmazonSQSAsOneWayClient(this StandardConfigurer<ITransport> configurer, string accessKeyId, string secretAccessKey, AmazonSQSConfig amazonSqsConfig, AmazonSQSTransportOptions options = null)
    {
        var credentials = new BasicAWSCredentials(accessKeyId, secretAccessKey);

        ConfigureOneWayClient(configurer, GetTransportOptions(options, credentials, amazonSqsConfig));
    }

    /// <summary>
    /// Configures Rebus to use Amazon Simple Queue Service as the message transport
    /// </summary>
    public static void UseAmazonSQSAsOneWayClient(this StandardConfigurer<ITransport> configurer, AWSCredentials credentials, AmazonSQSConfig config, AmazonSQSTransportOptions options = null)
    {
        ConfigureOneWayClient(configurer, GetTransportOptions(options, credentials, config));
    }

    /// <summary>
    /// Configures Rebus to use Amazon Simple Queue Service as the message transport
    /// </summary>
    public static void UseAmazonSQSAsOneWayClient(this StandardConfigurer<ITransport> configurer, AmazonSQSConfig config, AmazonSQSTransportOptions options = null)
    {
        ConfigureOneWayClient(configurer, GetTransportOptions(options, null, config));
    }

    /// <summary>
    /// Configures Rebus to use Amazon Simple Queue Service as the message transport
    /// </summary>
    public static void UseAmazonSQSAsOneWayClient(this StandardConfigurer<ITransport> configurer, AmazonSQSTransportOptions options = null)
    {
        ConfigureOneWayClient(configurer, GetTransportOptions(options, null, null));
    }

    static AmazonSQSTransportOptions GetTransportOptions(AmazonSQSTransportOptions options, AWSCredentials credentials, AmazonSQSConfig config)
    {
        options ??= new AmazonSQSTransportOptions();

        if (options.ClientFactory == null)
        {
            options.ClientFactory = GetClientFactory(credentials, config);
        }
        else
        {
            if (credentials != null || config != null)
            {
                throw new RebusConfigurationException($"Could not configure SQS client, because a client factory was provided at the same time as either AWS credentials and/or SQS config. Please EITHER provide a factory, OR provide the necessary credentials and/or config, OR do not provide anything alltogether to fall back to EC2 roles");
            }
        }

        return options;
    }

    static Func<IAmazonSQS> GetClientFactory(AWSCredentials credentials, AmazonSQSConfig config)
    {
        IAmazonSQS CreateClientFromCredentialsAndConfig() => new AmazonSQSClient(credentials, config);

        IAmazonSQS CreateClientFromCredentials() => new AmazonSQSClient(credentials);

        IAmazonSQS CreateClientFromConfig() => new AmazonSQSClient(config);

        IAmazonSQS CreateDefaultClient() => new AmazonSQSClient();

        if (credentials != null && config != null)
        {
            return CreateClientFromCredentialsAndConfig;
        }

        if (credentials != null)
        {
            return CreateClientFromCredentials;
        }

        if (config != null)
        {
            return CreateClientFromConfig;
        }

        return CreateDefaultClient;
    }

    static void Configure(StandardConfigurer<ITransport> configurer, string inputQueueAddress, AmazonSQSTransportOptions options)
    {
        if (configurer == null) throw new ArgumentNullException(nameof(configurer));
        if (inputQueueAddress == null) throw new ArgumentNullException(nameof(inputQueueAddress));
        if (options == null) throw new ArgumentNullException(nameof(options));

        configurer.Register(c =>
        {
            var rebusLoggerFactory = c.Get<IRebusLoggerFactory>();
            var asyncTaskFactory = c.Get<IAsyncTaskFactory>();
            var rebusTime = c.Get<IRebusTime>();

            return new AmazonSqsTransport(inputQueueAddress, rebusLoggerFactory, asyncTaskFactory, options, rebusTime);
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

    static void ConfigureOneWayClient(StandardConfigurer<ITransport> configurer, AmazonSQSTransportOptions options)
    {
        if (configurer == null) throw new ArgumentNullException(nameof(configurer));
        if (options == null) throw new ArgumentNullException(nameof(options));

        configurer.Register(c =>
        {
            var rebusLoggerFactory = c.Get<IRebusLoggerFactory>();
            var asyncTaskFactory = c.Get<IAsyncTaskFactory>();
            var rebusTime = c.Get<IRebusTime>();

            return new AmazonSqsTransport(null, rebusLoggerFactory, asyncTaskFactory, options, rebusTime);
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