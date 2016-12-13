using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Rebus.Activation;
using Rebus.Config;
using Rebus.Messages;
using Rebus.Tests.Contracts;
using Rebus.Tests.Contracts.Extensions;
using Xunit;

#pragma warning disable 1998

namespace Rebus.AmazonSQS.Tests
{
    [Trait("Category", Category.AmazonSqs)]
    public class AmazonSqsTrivialMessageRoundtrip : SqsFixtureBase
    {
        AmazonSqsTransport _transport;
        string _brilliantQueueName;

        public AmazonSqsTrivialMessageRoundtrip()
        {
            _brilliantQueueName = TestConfig.GetName("roundtrippin");
            _transport = AmazonSqsTransportFactory.CreateTransport(_brilliantQueueName, TimeSpan.FromSeconds(30));
            _transport.Purge();
        }

        [Fact]
        public async Task CanRoundtripSingleMessageWithTransport()
        {
            const string positiveGreeting = "hej meeeeed dig min vennnnn!!!!!!111";

            await WithContext(async context =>
            {
                var message = new TransportMessage(NewFineHeaders(), Encoding.UTF8.GetBytes(positiveGreeting));

                await _transport.Send(_brilliantQueueName, message, context);
            });

            var receivedMessage = await _transport.WaitForNextMessage();

            Assert.Equal(positiveGreeting, Encoding.UTF8.GetString(receivedMessage.Body));
        }

        [Fact]
        public async Task CanRoundtripSingleMessageWithBus()
        {
            using (var activator = new BuiltinHandlerActivator())
            {
                var gotTheMessage = new ManualResetEvent(false);

                activator.Handle<string>(async message =>
                {
                    gotTheMessage.Set();
                });

                Configure.With(activator)
                    .Transport(t => t.Register(c => _transport))
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