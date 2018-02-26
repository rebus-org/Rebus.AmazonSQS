using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Amazon.Runtime;
using NUnit.Framework;
using Rebus.Logging;
using Rebus.Messages;

namespace Rebus.AmazonSQS.Tests.Bugs
{
    [TestFixture]
    public class DelayForMessagesCanBeCalculatedLessThanZero : SqsFixtureBase
    {
        private AmazonSQSTransport transport;

        protected override void SetUp()
        {
            transport = new AmazonSQSTransport(
                string.Empty,
                new BasicAWSCredentials(string.Empty, string.Empty),
                new Amazon.SQS.AmazonSQSConfig(),
                new NullLoggerFactory(),
                null,
                new Config.AmazonSQSTransportOptions() { CreateQueues = false }
            );
        }

        [Test]
        public void WhenMessageDefferedUntilBeforeCurrentTimeThenDelayIsZero()
        {
            var headers = new Dictionary<string, string>
                          {
                              {Headers.MessageId, Guid.NewGuid().ToString()},
                              {Headers.CorrelationId, Guid.NewGuid().ToString()},
                              {Headers.DeferredUntil, DateTimeOffset.Now.AddDays(-1).ToString("o")}
                          };
            var delay = transport.GetDelaySeconds(headers);

            Assert.IsTrue(delay >= 0);
        }
    }
}
