using System;
using System.Threading;
using System.Threading.Tasks;
using Amazon;
using Amazon.SQS;
using NUnit.Framework;
using Rebus.Activation;
using Rebus.Config;
using Rebus.Tests.Contracts.Extensions;

namespace Rebus.AmazonSQS.Tests.Bugs
{
    [TestFixture]
    public class CanStartWithDefaultCredsAndRegion : SqsFixtureBase
    {
        [Test]
        [Ignore("Can apparently only be run when EC2 creds are present")]
        public async Task Yeas()
        {
            var gotTheString = new ManualResetEvent(false);

            var activator = new BuiltinHandlerActivator();

            Using(activator);

            activator.Handle<string>(async str => gotTheString.Set());

            Configure.With(activator)
                .Transport(t =>
                {
                    var config = new AmazonSQSConfig { RegionEndpoint = RegionEndpoint.EUWest2 };
                    
                    t.UseAmazonSQS("queue", config);
                })
                .Start();

            await activator.Bus.SendLocal("HEJ MED DIG MIN VEN");

            gotTheString.WaitOrDie(TimeSpan.FromSeconds(3), "Did not get the string within 3 s timeout");
        }
    }
}