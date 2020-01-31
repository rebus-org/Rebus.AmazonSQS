using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using Amazon.Runtime;
using Amazon.SQS;
using Rebus.Config;
using Rebus.Exceptions;
using Rebus.Extensions;
using Rebus.Logging;
using Rebus.Tests.Contracts.Transports;
using Rebus.Threading.TaskParallelLibrary;
using Rebus.Time;
using Rebus.Transport;

namespace Rebus.AmazonSQS.Tests
{
    public class AmazonSqsTransportFactory : ITransportFactory
    {
        static ConnectionInfo _connectionInfo;

        internal static ConnectionInfo ConnectionInfo => _connectionInfo ??= ConnectionInfoFromFileOrNull(GetFilePath())
                                                                             ?? ConnectionInfoFromEnvironmentVariable("rebus2_asqs_connection_string")
                                                                             ?? Throw("Could not find Amazon Sqs connetion Info!");

        public static AmazonSqsTransport CreateTransport(string inputQueueAddress, TimeSpan peeklockDuration, AmazonSQSTransportOptions options = null)
        {
            var connectionInfo = ConnectionInfo;
            var amazonSqsConfig = new AmazonSQSConfig { RegionEndpoint = connectionInfo.RegionEndpoint };

            var consoleLoggerFactory = new ConsoleLoggerFactory(false);
            var credentials = new BasicAWSCredentials(connectionInfo.AccessKeyId, connectionInfo.SecretAccessKey);

            options ??= new AmazonSQSTransportOptions();

            options.ClientFactory = () => new AmazonSQSClient(credentials, amazonSqsConfig);

            var transport = new AmazonSqsTransport(
                inputQueueAddress,
                consoleLoggerFactory,
                new TplAsyncTaskFactory(consoleLoggerFactory),
                options,
                new DefaultRebusTime()
            );

            transport.Initialize(peeklockDuration);
            transport.Purge();
            return transport;
        }

        readonly ConcurrentStack<IDisposable> _disposables = new ConcurrentStack<IDisposable>();
        readonly Dictionary<string, AmazonSqsTransport> _queuesToDelete = new Dictionary<string, AmazonSqsTransport>();

        public ITransport Create(string inputQueueAddress, TimeSpan peeklockDuration, AmazonSQSTransportOptions options = null)
        {
            if (inputQueueAddress == null)
            {
                // one-way client
                var transport = CreateTransport(null, peeklockDuration, options);
                _disposables.Push(transport);
                return transport;
            }

            return _queuesToDelete.GetOrAdd(inputQueueAddress, () =>
            {
                var amazonSQSTransport = CreateTransport(inputQueueAddress, peeklockDuration, options);
                _disposables.Push(amazonSQSTransport);
                return amazonSQSTransport;
            });
        }

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

            while (_disposables.TryPop(out var disposable))
            {
                Console.WriteLine($"Disposing {disposable}");
                disposable.Dispose();
            }
        }

        public ITransport CreateOneWayClient()
        {
            return Create(null, TimeSpan.FromSeconds(30));
        }

        public ITransport Create(string inputQueueAddress)
        {
            return Create(inputQueueAddress, TimeSpan.FromSeconds(30));
        }

        static string GetFilePath()
        {
            var baseDirectory = AppContext.BaseDirectory;

            // added because of test run issues on MacOS
            var indexOfBin = baseDirectory.LastIndexOf("bin", StringComparison.OrdinalIgnoreCase);
            var connectionStringFileDirectory = baseDirectory.Substring(0, (indexOfBin > 0) ? indexOfBin : baseDirectory.Length);
            return Path.Combine(connectionStringFileDirectory, "sqs_connectionstring.txt");
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