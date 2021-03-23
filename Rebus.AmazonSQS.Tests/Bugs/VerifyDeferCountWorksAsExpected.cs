using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebus.Activation;
using Rebus.AmazonSQS.Tests.Extensions;
using Rebus.Bus;
using Rebus.Bus.Advanced;
using Rebus.Config;
using Rebus.Exceptions;
using Rebus.Handlers;
using Rebus.Messages;
using Rebus.Persistence.InMem;
using Rebus.Pipeline;
using Rebus.Retry.Simple;
using Rebus.Tests.Contracts;
using Rebus.Tests.Contracts.Extensions;
using Rebus.Transport.InMem;

namespace Rebus.AmazonSQS.Tests.Bugs
{
    [TestFixture]
    public class VerifyDeferCountWorksAsExpected : FixtureBase
    {
        [Test]
        public async Task ComeOn_InMemory()
        {
            using var activator = new BuiltinHandlerActivator();

            activator.Register((bús, context) => new TestMessageHandler(context, bús.Advanced.TransportMessage));

            var network = new InMemNetwork();

            var bus = Configure.With(activator)
                .Logging(l => l.None())
                .Transport(t => t.UseInMemoryTransport(network, "whatever-man"))
                .Options(o => o.SimpleRetryStrategy(secondLevelRetriesEnabled: true))
                .Timeouts(t => t.StoreInMemory())
                .Start();

            await bus.SendLocal(new TestMessage("yo!"));

            var message = await network.WaitForNextMessageFrom("error", timeoutSeconds: 25);

            Assert.That(message.DeserializeTo<TestMessage>().Text, Is.EqualTo("yo!"));
        }

        [Test]
        public async Task ComeOn_Sqs()
        {
            var queueName = Guid.NewGuid().ToString("N");

            Using(new QueueDeleter(queueName));

            AmazonSqsManyMessagesTransportFactory.PurgeQueue(queueName);

            using var errorQueueTransport = AmazonSqsTransportFactory.CreateTransport("error", TimeSpan.FromMinutes(1));

            var info = AmazonSqsTransportFactory.ConnectionInfo;

            using var activator = new BuiltinHandlerActivator();

            activator.Register((bús, context) => new TestMessageHandler(context, bús.Advanced.TransportMessage));

            var bus = Configure.With(activator)
                .Logging(l => l.None())
                .Transport(t => t.UseAmazonSQS(info.AccessKeyId, info.SecretAccessKey, info.RegionEndpoint, queueName))
                .Options(o => o.SimpleRetryStrategy(secondLevelRetriesEnabled: true))
                .Start();

            await bus.SendLocal(new TestMessage("yo!"));

            var message = await errorQueueTransport.WaitForNextMessage(timeoutSeconds: 25);

            Assert.That(message.DeserializeTo<TestMessage>().Text, Is.EqualTo("yo!"));
        }

        class TestMessage
        {
            public TestMessage(string text) => Text = text;
            public string Text { get; }
        }

        class TestMessageHandler : IHandleMessages<TestMessage>, IHandleMessages<IFailed<TestMessage>>
        {
            readonly IMessageContext _messageContext;
            readonly ITransportMessageApi _transportMessageApi;

            public TestMessageHandler(IMessageContext messageContext, ITransportMessageApi transportMessageApi)
            {
                _messageContext = messageContext;
                _transportMessageApi = transportMessageApi;
            }

            public Task Handle(TestMessage message)
            {
                var messageId = _messageContext.TransportMessage.GetMessageId();
                Console.WriteLine($"Throwing exception! (msg id {messageId})");
                throw new FailFastException("whatever");
            }

            public Task Handle(IFailed<TestMessage> message)
            {
                var deferCount = Convert.ToInt32(_messageContext.Headers.GetValueOrDefault(Headers.DeferCount));
                var messageId = _messageContext.TransportMessage.GetMessageId();

                if (deferCount >= 3)
                {
                    Console.WriteLine($"Defer count: {deferCount}, manually dead-lettering (msg id {messageId})");
                    return _transportMessageApi.Deadletter($"Failed after {deferCount} attempts\n\n{message.ErrorDescription}");
                }

                Console.WriteLine($"Defer count: {deferCount}, delay seconds: 1 (msg id {messageId})");
                return _transportMessageApi.Defer(TimeSpan.FromSeconds(1));
            }
        }
    }
}