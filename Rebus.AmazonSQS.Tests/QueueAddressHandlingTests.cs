using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Rebus.Transport;

namespace Rebus.AmazonSQS.Tests
{
    [Trait("Category", Category.AmazonSqs)]
    public class QueueAddressHandlingTests : SqsFixtureBase
    {
        private AmazonSqsTransportFactory _transportFactory;

        public QueueAddressHandlingTests()
        {
            _transportFactory = new AmazonSqsTransportFactory();
        }

        //[Fact]
        //public async Task WhenTheInputAddressIsAFullUrlAndDestinationIsQueueName_ThenItsStillWorks()
        //{
        //    //arrange



        //    var queueName = "test" + Guid.NewGuid().ToString();
        //    var fullUrl = _transportFactory.BaseUrl + queueName;
        //    var outputTransport = _transportFactory.Create(fullUrl);
        //    var destinationQueueName = "testDeux" + Guid.NewGuid().ToString();
        //    var receivingTransport = _transportFactory.Create(destinationQueueName);
        //    //act

        //    await TestSendReceive(outputTransport, destinationQueueName, receivingTransport);


        //    //assert



        //}

        //[Fact]
        //public async Task WhenTheInputIsAQueueNameAndDestinationIsFullUrl_ThenItsStillWorks()
        //{
        //    //arrange



        //    var queueName = "test" + Guid.NewGuid().ToString();

        //    var outputTransport = _transportFactory.Create(queueName);

        //    var destinationFullUrl = _transportFactory.BaseUrl + "testDeux" + Guid.NewGuid().ToString();
        //    var receivingTransport = _transportFactory.Create(destinationFullUrl);
        //    //act

        //    await TestSendReceive(outputTransport, destinationFullUrl, receivingTransport);


        //    //assert



        //}

        //[Fact]
        //public async Task WhenBothInputAndDestinationIsFullUrl_ThenItWorks()
        //{
        //    //arrange

        //    var inputqueue = _transportFactory.BaseUrl + "output" + Guid.NewGuid();
        //    var outputTransport = _transportFactory.Create(inputqueue);

        //    var destinationFullUrl = _transportFactory.BaseUrl + "testDeux" + Guid.NewGuid().ToString(); ;
        //    var receivingTransport = _transportFactory.Create(destinationFullUrl);
        //    //act

        //    await TestSendReceive(outputTransport, destinationFullUrl, receivingTransport);



        //    //assert

        //}

        [Fact]
        public void WhenUsingAQueuNameWithSlash_ThenArgumentExcetiopIsThrown()
        {
            //arrange

            var invalidQueueName = "/inputqueue";

            Assert.Throws<ArgumentException>(() => _transportFactory.Create(invalidQueueName));
            //act

            //assert

        }

        private async Task TestSendReceive(ITransport outputTransport, string destinationQueueUrlOrName, ITransport destinationTransport)
        {
            await WithContext(async (context) => { await outputTransport.Send(destinationQueueUrlOrName, MessageWith("hallo"), context); });

            await WithContext(async context =>
            {
                var received = await destinationTransport.Receive(context, new CancellationTokenSource().Token);

                Assert.Equal("hallo", GetStringBody(received));
            });
        }


        protected override void TearDown()
        {
            base.TearDown();
            _transportFactory.CleanUp(true);



        }
    }
}