using Xunit;
using Rebus.Tests.Contracts.Transports;

namespace Rebus.AmazonSQS.Tests
{
    [Trait("Category", Category.AmazonSqs)]
    public class AmazonSqsSimpleSend : BasicSendReceive<AmazonSqsTransportFactory> { }
}
