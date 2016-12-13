using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Rebus.Tests.Contracts;

namespace Rebus.AmazonSQS.Tests
{
    [Trait("Category", Category.AmazonSqs)]
    public class AmazonSqsVisibiltyTimeout : SqsFixtureBase
    {
        [Fact]
        public async Task WhenMessageVisibilityIsRenewed_ThenItsNotVisibleForOthers()
        {
            //arrange
            var peeklockDuration = TimeSpan.FromSeconds(3);

            var transportFactory = new AmazonSqsTransportFactory();
            
            var inputqueueName = TestConfig.GetName("inputQueue");
            var inputQueue = transportFactory.Create(inputqueueName, peeklockDuration);

            var inputqueueName2 = TestConfig.GetName("outputQueue");
            var outputQueue = transportFactory.Create(inputqueueName2);

            await WithContext(async context =>
            {
                await outputQueue.Send(inputqueueName, MessageWith("hej"), context);
            });

            var cancellationToken = new CancellationTokenSource().Token;

            await WithContext(async context =>
            {
                var transportMessage = await inputQueue.Receive(context, cancellationToken);

                Assert.NotNull(transportMessage);

                // pretend that it takes a while to handle the message
                Thread.Sleep(6000);

                // pretend that another thread attempts to receive from the same input queue
                await WithContext(async innerContext =>
                {
                    var innerMessage = await inputQueue.Receive(innerContext, cancellationToken);

                    Assert.Null(innerMessage);
                });
            });
        }
    }
}