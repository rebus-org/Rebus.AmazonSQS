using System;
using System.Collections.Generic;
using System.IO;
using Amazon;
using Amazon.SQS;
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

        internal static ConnectionInfo ConnectionInfo => _connectionInfo ?? (_connectionInfo = ConnectionInfoFromFileOrNull(Path.Combine(GetBaseDirectory(), "sqs_connectionstring.txt"))
                                                                                               ?? ConnectionInfoFromEnvironmentVariable("rebus2_asqs_connection_string")
                                                                                               ?? Throw("Could not find Amazon Sqs connetion Info!"));

        static string GetBaseDirectory()
        {
#if NETSTANDARD1_6
            return AppContext.BaseDirectory;
#else
            return AppDomain.CurrentDomain.BaseDirectory;
#endif
        }


        public ITransport Create(string inputQueueAddress, TimeSpan peeklockDuration)
        {
            if (inputQueueAddress == null)
            {
                return CreateTransport(inputQueueAddress, peeklockDuration);
            }

            return _queuesToDelete.GetOrAdd(inputQueueAddress, () => CreateTransport(inputQueueAddress, peeklockDuration));
        }

        public static AmazonSqsTransport CreateTransport(string inputQueueAddress, TimeSpan peeklockDuration)
        {
            var amazonSqsConfig = new AmazonSQSConfig
            {
                RegionEndpoint = RegionEndpoint.GetBySystemName(ConnectionInfo.RegionEndpoint)
            };

            var consoleLoggerFactory = new ConsoleLoggerFactory(false);
            var transport = new AmazonSqsTransport(inputQueueAddress, ConnectionInfo.AccessKeyId, ConnectionInfo.SecretAccessKey,
                amazonSqsConfig,
                consoleLoggerFactory,
                new TplAsyncTaskFactory(consoleLoggerFactory));

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


        readonly Dictionary<string, AmazonSqsTransport> _queuesToDelete = new Dictionary<string, AmazonSqsTransport>();


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