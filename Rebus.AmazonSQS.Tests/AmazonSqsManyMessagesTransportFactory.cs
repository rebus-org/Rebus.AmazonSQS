using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Amazon.Runtime;
using Amazon.SQS;
using Rebus.Activation;
using Rebus.Bus;
using Rebus.Config;
using Rebus.Logging;
using Rebus.Tests.Contracts.Transports;
using Rebus.Threading.TaskParallelLibrary;

namespace Rebus.AmazonSQS.Tests
{
    public class AmazonSqsManyMessagesTransportFactory : IBusFactory
    {
        readonly ConcurrentStack<IDisposable> _stuffToDispose = new ConcurrentStack<IDisposable>();

        public IBus GetBus<TMessage>(string inputQueueAddress, Func<TMessage, Task> handler)
        {
            var builtinHandlerActivator = new BuiltinHandlerActivator();

            builtinHandlerActivator.Handle(handler);

            PurgeQueue(inputQueueAddress);

            var bus = Configure.With(builtinHandlerActivator)
                .Transport(t =>
                {
                    var info = AmazonSqsTransportFactory.ConnectionInfo;

                    var amazonSqsConfig = new AmazonSQSConfig { RegionEndpoint = info.RegionEndpoint };

                    t.UseAmazonSQS(info.AccessKeyId, info.SecretAccessKey, amazonSqsConfig, inputQueueAddress);
                })
                .Options(o =>
                {
                    o.SetNumberOfWorkers(10);
                    o.SetMaxParallelism(10);
                })
                .Start();

            _stuffToDispose.Push(bus);

            return bus;
        }

        public static void PurgeQueue(string queueName)
        {
            var consoleLoggerFactory = new ConsoleLoggerFactory(false);

            var connectionInfo = AmazonSqsTransportFactory.ConnectionInfo;
            var amazonSqsConfig = new AmazonSQSConfig { RegionEndpoint = connectionInfo.RegionEndpoint };

            var credentials = new BasicAWSCredentials(connectionInfo.AccessKeyId, connectionInfo.SecretAccessKey);

            var transport = new AmazonSqsTransport(
                queueName,
                consoleLoggerFactory,
                new TplAsyncTaskFactory(consoleLoggerFactory),
                new AmazonSQSTransportOptions
                {
                    ClientFactory = () => new AmazonSQSClient(credentials, amazonSqsConfig)
                }
            );

            transport.Purge();
        }

        public void Cleanup()
        {
            while (_stuffToDispose.TryPop(out var disposable))
            {
                Console.WriteLine($"Disposing {disposable}");
                disposable.Dispose();
            }
        }
    }
}