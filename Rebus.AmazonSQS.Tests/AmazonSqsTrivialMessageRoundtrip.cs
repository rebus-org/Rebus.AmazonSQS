using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebus.Activation;
using Rebus.Config;
using Rebus.Messages;
using Rebus.Tests.Contracts;
using Rebus.Tests.Contracts.Extensions;
using Rebus.Transport;
#pragma warning disable 1998

namespace Rebus.AmazonSQS.Tests
{
    [TestFixture, Category(Category.AmazonSqs)]
    public class AmazonSqsTrivialMessageRoundtrip : SqsFixtureBase
    {
        [Test]
        public async Task CanRoundtripSingleMessageWithTransport()
        {
            var queueName = TestConfig.GetName("roundtrippin-single");
            var transport = AmazonSqsTransportFactory.CreateTransport(queueName, TimeSpan.FromSeconds(30));
            
            Using(transport);

            const string positiveGreeting = "hej meeeeed dig min vennnnn!!!!!!111";

            var transportMessage = new TransportMessage(NewFineHeaders(), Encoding.UTF8.GetBytes(positiveGreeting));

            using (var scope = new RebusTransactionScope())
            {
                Console.WriteLine($"Sending message to '{queueName}'");
                await transport.Send(queueName, transportMessage, scope.TransactionContext);
                await scope.CompleteAsync();
            }

            var receivedMessage = await transport.WaitForNextMessage();

            Assert.That(Encoding.UTF8.GetString(receivedMessage.Body), Is.EqualTo(positiveGreeting));
        }

        [Test]
        public async Task CanRoundtripSingleMessageWithBus()
        {
            var brilliantQueueName = TestConfig.GetName("roundtrippin-single-bus");
            var transport = AmazonSqsTransportFactory.CreateTransport(brilliantQueueName, TimeSpan.FromSeconds(30));
            
            Using(transport);

            using (var activator = new BuiltinHandlerActivator())
            {
                var gotTheMessage = new ManualResetEvent(false);

                activator.Handle<string>(async message =>
                {
                    gotTheMessage.Set();
                });

                Configure.With(activator)
                    .Transport(t => t.Register(c => transport))
                    .Start();

                await activator.Bus.SendLocal("HAIIIIIIIIIIIIIIIIII!!!!111");

                gotTheMessage.WaitOrDie(TimeSpan.FromSeconds(5));
            }
        }

        static Dictionary<string, string> NewFineHeaders()
        {
            return new Dictionary<string, string>
            {
                {Headers.MessageId, Guid.NewGuid().ToString() }
            };
        }
    }
}