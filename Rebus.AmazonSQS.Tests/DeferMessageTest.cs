using System;
using System.Threading;
using System.Threading.Tasks;
using Amazon;
using Amazon.SQS;
using Xunit;
using Rebus.Activation;
using Rebus.Config;
using Rebus.Tests.Contracts;
using Rebus.Tests.Contracts.Extensions;

#pragma warning disable 1998

namespace Rebus.AmazonSQS.Tests
{
    [Trait("Category", Category.AmazonSqs)]
    public class DeferMessageTest : SqsFixtureBase
    {
        BuiltinHandlerActivator _activator;
        RebusConfigurer _configurer;

        public DeferMessageTest() {
            var connectionInfo = AmazonSqsTransportFactory.ConnectionInfo;

            var accessKeyId = connectionInfo.AccessKeyId;
            var secretAccessKey = connectionInfo.SecretAccessKey;
            var amazonSqsConfig = new AmazonSQSConfig
            {
                RegionEndpoint = RegionEndpoint.GetBySystemName(AmazonSqsTransportFactory.ConnectionInfo.RegionEndpoint)
            };

            var queueName = TestConfig.GetName("defertest");

            AmazonSqsManyMessagesTransportFactory.PurgeQueue(queueName);

            _activator = Using(new BuiltinHandlerActivator());

            _configurer = Configure.With(_activator)
                .Transport(t => t.UseAmazonSqs(accessKeyId, secretAccessKey, amazonSqsConfig, queueName))
                .Options(o => o.LogPipeline());
        }

        [Fact]
        public async Task CanDeferMessage()
        {
            var gotTheMessage = new ManualResetEvent(false);

            var receiveTime = DateTime.MaxValue;

            _activator.Handle<string>(async str =>
            {
                receiveTime = DateTime.UtcNow;
                gotTheMessage.Set();
            });

            var bus = _configurer.Start();
            var sendTime = DateTime.UtcNow;

            await bus.Defer(TimeSpan.FromSeconds(10), "hej med dig!");

            gotTheMessage.WaitOrDie(TimeSpan.FromSeconds(20));

            var elapsed = receiveTime - sendTime;

            Assert.True(elapsed > TimeSpan.FromSeconds(8));
        }
    }
}