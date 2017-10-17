using System;
using System.Collections.Generic;
using System.IO;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.SQS;
using Rebus.Config;
using Rebus.Exceptions;
using Rebus.Extensions;
using Rebus.Logging;
using Rebus.Tests.Contracts.Transports;
using Rebus.Threading.TaskParallelLibrary;
using Rebus.Transport;

namespace Rebus.AmazonSQS.Tests
{
    public class AmazonSqsTransportFactory : ITransportFactory
    {
        static ConnectionInfo _connectionInfo;

        internal static ConnectionInfo ConnectionInfo => _connectionInfo ?? (_connectionInfo = ConnectionInfoFromFileOrNull(GetFilePath())
                                                                                               ?? ConnectionInfoFromEnvironmentVariable("rebus2_asqs_connection_string")
                                                                                               ?? Throw("Could not find Amazon Sqs connection Info!"));

        static string GetFilePath()
        {
#if NET45
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
#elif NETSTANDARD1_3
            var baseDirectory = AppContext.BaseDirectory;
#endif
            // added because of test run issues on MacOS
            var indexOfBin = baseDirectory.LastIndexOf("bin", StringComparison.OrdinalIgnoreCase);
            var connectionStringFileDirectory = baseDirectory.Substring(0, (indexOfBin > 0) ? indexOfBin : baseDirectory.Length);
            return Path.Combine(connectionStringFileDirectory, "sqs_connectionstring.txt");
        }

        public ITransport Create(string inputQueueAddress, TimeSpan peeklockDuration, AmazonSQSTransportOptions options = null, AmazonS3Options s3Options = null)
        {
            if (inputQueueAddress == null)
            {
                // one-way client
                return CreateTransport(null, peeklockDuration, options, s3Options);
            }

            return _queuesToDelete.GetOrAdd(inputQueueAddress, () => CreateTransport(inputQueueAddress, peeklockDuration, options, s3Options));
        }

        public static AmazonSQSTransport CreateTransport(string inputQueueAddress, TimeSpan peeklockDuration, AmazonSQSTransportOptions options = null, AmazonS3Options s3Options = null)
        {
            var connectionInfo = ConnectionInfo;
            var amazonSqsConfig = new AmazonSQSConfig { RegionEndpoint = connectionInfo.RegionEndpoint };
            var amazonS3Config = s3Options != null ? new AmazonS3Config { RegionEndpoint = connectionInfo.RegionEndpoint } : null;

            var consoleLoggerFactory = new ConsoleLoggerFactory(false);
            var credentials = new BasicAWSCredentials(connectionInfo.AccessKeyId, connectionInfo.SecretAccessKey);

            var transport = new AmazonSQSTransport(
                inputQueueAddress,
                credentials,
                amazonSqsConfig,
                consoleLoggerFactory,
                new TplAsyncTaskFactory(consoleLoggerFactory),
                options,
                amazonS3Config,
                s3Options
            );

            transport.Initialize(peeklockDuration);
            transport.Purge();
            return transport;
        }

        public ITransport CreateOneWayClient()
        {
            return Create(null, TimeSpan.FromSeconds(30));
        }

        public ITransport Create(string inputQueueAddress)
        {
            return Create(inputQueueAddress, TimeSpan.FromSeconds(30));
        }

        public ITransport CreateExtended(string inputQueueAddress, string bucketName)
        {
            return Create(inputQueueAddress, TimeSpan.FromSeconds(30), null, new AmazonS3Options { BucketName = bucketName });
        }

        readonly Dictionary<string, AmazonSQSTransport> _queuesToDelete = new Dictionary<string, AmazonSQSTransport>();


        public void CleanUp()
        {
            CleanUp(false);
        }

        public void CleanUp(bool deleteQueues)
        {
            if (deleteQueues)
            {
                foreach (var queueAndTransport in _queuesToDelete)
                {
                    var transport = queueAndTransport.Value;

                    transport.DeleteQueue();
                }
            }
        }


        static ConnectionInfo ConnectionInfoFromEnvironmentVariable(string environmentVariableName)
        {
            var value = Environment.GetEnvironmentVariable(environmentVariableName);

            if (value == null)
            {
                Console.WriteLine("Could not find env variable {0}", environmentVariableName);
                return null;
            }

            Console.WriteLine("Using AmazonSqs connection info from env variable {0}", environmentVariableName);

            return ConnectionInfo.CreateFromString(value);
        }

        static ConnectionInfo ConnectionInfoFromFileOrNull(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Console.WriteLine("Could not find file {0}", filePath);
                return null;
            }

            Console.WriteLine("Using Amazon SQS connectionInfo string from file {0}", filePath);
            return ConnectionInfo.CreateFromString(File.ReadAllText(filePath));
        }

        static ConnectionInfo Throw(string message)
        {
            throw new RebusConfigurationException(message);
        }
    }
}