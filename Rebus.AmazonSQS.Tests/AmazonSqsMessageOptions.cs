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
        private AmazonSqsTransportFactory _transportFactory;

        protected override void SetUp()
        {
            base.SetUp();
            _transportFactory = new AmazonSqsTransportFactory();
        }

        [Test]
        public async Task WhenCoreHeadersShouldBeCollapsed_ThenHeadersAreConcatenatedIntoOneAttribute()
        {
            //arrange
            var inputqueueName = TestConfig.GetName($"inputQueue-{DateTime.Now.Ticks}");
            var inputQueue = _transportFactory.Create(inputqueueName, true);

            var inputqueueName2 = TestConfig.GetName($"outputQueue-{DateTime.Now.Ticks}");
            var outputQueue = _transportFactory.Create(inputqueueName2, true);

            await WithContext(async context =>
            {
                var transportMessage = MessageWith("hej");
                Assert.That(transportMessage.Headers.ContainsKey(Headers.MessageId));
                Assert.That(transportMessage.Headers.ContainsKey(Headers.CorrelationId));

                // send the message of to the bus, which will make sure that all core headers
                // are collapsed into a single header
                await outputQueue.Send(inputqueueName, MessageWith("hej"), context);
            });

            var cancellationToken = new CancellationTokenSource().Token;

            await WithContext(async context =>
            {
                var transportMessage = await inputQueue.Receive(context, cancellationToken);

                Assert.That(transportMessage, Is.Not.Null, "Expected to receive the message that we just sent");

                // inspect the message whether it contains the original headers which are now again exploded
                Assert.That(transportMessage.Headers.ContainsKey(Headers.MessageId));
                Assert.That(transportMessage.Headers.ContainsKey(Headers.CorrelationId));
            });
        }

        protected override void TearDown()
        {
            base.TearDown();
            _transportFactory.CleanUp(true);
        }
    }
}