using Rebus.Tests.Contracts.Transports;
using Xunit;

namespace Rebus.AmazonSQS.Tests
{
    [Trait("Category", Category.AmazonSqs)]
    public class AmazonSqsManyMessages : TestManyMessages<AmazonSqsManyMessagesTransportFactory>
    {
    }
}