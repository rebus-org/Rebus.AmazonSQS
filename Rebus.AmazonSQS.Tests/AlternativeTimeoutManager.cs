using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebus.Activation;
using Rebus.Config;
using Rebus.Persistence.InMem;
using Rebus.Tests.Contracts;
using Rebus.Tests.Contracts.Extensions;
using Rebus.Timeouts;

#pragma warning disable 1998

namespace Rebus.AmazonSQS.Tests
{
    [TestFixture]
    public class AlternativeTimeoutManager : FixtureBase
    {
        const string QueueName = "alt-timeout-man-1";
        const string TimeoutManagerQueueName = "alt-timeout-man-2";

        BuiltinHandlerActivator _activator;

        protected override void SetUp()
        {
            AmazonSqsManyMessagesTransportFactory.PurgeQueue(QueueName);

            _activator = new BuiltinHandlerActivator();

            Using(_activator);
        }

        [Test]
        public async Task CanUseAlternativeTimeoutManager()
        {
            var info = AmazonSqsTransportFactory.ConnectionInfo;
            var gotTheString = new ManualResetEvent(false);

            _activator.Handle<string>(async str =>
            {
                Console.WriteLine($"Received string: '{str}'");

                gotTheString.Set();
            });

            var bus = Configure.With(_activator)
                .Transport(t => t.UseAmazonSQS(info.AccessKeyId, info.SecretAccessKey, info.RegionEndpoint, QueueName, new AmazonSQSTransportOptions
                {
                    UseNativeDeferredMessages = false
                }))
                .Timeouts(t => t.Register(c => new InMemoryTimeoutManager()))
                .Start();

            await bus.Defer(TimeSpan.FromSeconds(5), "hej med dig min ven!!!!!");

            gotTheString.WaitOrDie(TimeSpan.FromSeconds(10), "Did not receive the string withing 10 s timeout");
        }

        [Test]
        public async Task CanUseDedicatedAlternativeTimeoutManager()
        {
            var info = AmazonSqsTransportFactory.ConnectionInfo;

            // start the timeout manager
            Configure.With(Using(new BuiltinHandlerActivator()))
                .Transport(t => t.UseAmazonSQS(info.AccessKeyId, info.SecretAccessKey, info.RegionEndpoint, TimeoutManagerQueueName, new AmazonSQSTransportOptions
                {
                    UseNativeDeferredMessages = false
                }))
                .Timeouts(t => t.Register(c => new InMemoryTimeoutManager()))
                .Start();

            var gotTheString = new ManualResetEvent(false);

            _activator.Handle<string>(async str =>
            {
                Console.WriteLine($"Received string: '{str}'");

                gotTheString.Set();
            });

            var bus = Configure.With(_activator)
                .Transport(t => t.UseAmazonSQS(info.AccessKeyId, info.SecretAccessKey, info.RegionEndpoint, QueueName, new AmazonSQSTransportOptions
                {
                    UseNativeDeferredMessages = false
                }))
                .Timeouts(t => t.UseExternalTimeoutManager(TimeoutManagerQueueName))
                .Start();

            await bus.Defer(TimeSpan.FromSeconds(5), "hej med dig min ven!!!!!");

            gotTheString.WaitOrDie(TimeSpan.FromSeconds(10), "Did not receive the string withing 10 s timeout");
        }
    }
}