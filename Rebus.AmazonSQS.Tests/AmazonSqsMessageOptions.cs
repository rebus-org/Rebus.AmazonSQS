using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebus.Messages;
using Rebus.Tests.Contracts;

namespace Rebus.AmazonSQS.Tests
{
    [TestFixture, Category(Category.AmazonSqs)]
    public class AmazonSqsMessageOptions : SqsFixtureBase
    {

        [Test]
        public async Task WhenCoreHeadersShouldBeCollapsed_ThenHeadersAreConcatenatedIntoOneAttribute()
        {
            //arrange
            var transportFactory = new AmazonSqsTransportFactory();

            var inputqueueName = TestConfig.GetName($"inputQueue-{DateTime.Now.Ticks}");
            var inputQueue = transportFactory.Create(inputqueueName, true);

            var inputqueueName2 = TestConfig.GetName($"outputQueue-{DateTime.Now.Ticks}");
            var outputQueue = transportFactory.Create(inputqueueName2, true);

            await WithContext(async context =>
            {
                await outputQueue.Send(inputqueueName, MessageWith("hej"), context);
            });

            var cancellationToken = new CancellationTokenSource().Token;

            await WithContext(async context =>
            {
                var transportMessage = await inputQueue.Receive(context, cancellationToken);

                Assert.That(transportMessage, Is.Not.Null, "Expected to receive the message that we just sent");

                // inspect the message whether it contains the original headers
                Assert.That(transportMessage.Headers.ContainsKey(Headers.MessageId));
                Assert.That(transportMessage.Headers.ContainsKey(Headers.CorrelationId));
            });
        }
    }
}