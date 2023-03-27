using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.SQS.Model;
using NUnit.Framework;
using Rebus.Activation;
using Rebus.Config;
using Rebus.Logging;
using Rebus.Tests.Contracts;
#pragma warning disable 1998

namespace Rebus.AmazonSQS.Tests.Bugs;

[TestFixture]
public class CannotSendTooBigMessages : SqsFixtureBase
{
    string _queueName;

    protected override void SetUp()
    {
        _queueName = TestConfig.GetName("queue");

        Using(new QueuePurger(_queueName));
    }

    [Test]
    public async Task ThrowsLikeItShould()
    {
        var activator = new BuiltinHandlerActivator();

        activator.Handle<string>(async _ => { });

        Using(activator);

        var connectionInfo = AmazonSqsTransportFactory.ConnectionInfo;

        var bus = Configure.With(activator)
            .Logging(l => l.Console(LogLevel.Info))
            .Transport(t => t.UseAmazonSQS(connectionInfo.AccessKeyId, connectionInfo.SecretAccessKey, connectionInfo.RegionEndpoint, _queueName))
            .Start();

        var exception = Assert.ThrowsAsync<BatchRequestTooLongException>(async () =>
        {
            await bus.SendLocal(string.Concat(Enumerable.Repeat("DET HER ER BARE EN NORMAL STRENG", 10000)));
        });

        Console.WriteLine(exception);
    }

    [Test]
    public async Task ThrowsLikeItShould_ConcreteYetAnonymousModel()
    {
        var activator = new BuiltinHandlerActivator();

        activator.Handle<SomeKindOfRequest>(async _ => { });

        Using(activator);

        var connectionInfo = AmazonSqsTransportFactory.ConnectionInfo;

        var bus = Configure.With(activator)
            .Logging(l => l.Console(LogLevel.Info))
            .Transport(t => t.UseAmazonSQS(connectionInfo.AccessKeyId, connectionInfo.SecretAccessKey, connectionInfo.RegionEndpoint, _queueName))
            .Start();

        var exception = Assert.ThrowsAsync<BatchRequestTooLongException>(async () =>
        {
            await bus.SendLocal(new SomeKindOfRequest
            {
                SomeKindOfRequestModel = new SomeKindOfRequestModel
                {
                    HereWeHaveItems = Enumerable.Range(0, 300)
                        .Select(n => new SomeKindOfRequestModelBase.SomeKindOfItemModel
                        {
                            SubItems = Enumerable.Range(0, 10)
                                .Select(i => new SomeKindOfRequestModelBase.SubItemModel
                                {
                                    SubSubItems = Enumerable.Range(0, 5)
                                        .Select(l => new SomeKindOfRequestModelBase.SubSubItemModel
                                        {
                                            Number = l,
                                            Name = $"bucket-{n}-{i}-{l}",
                                        })
                                        .ToList(),

                                    Name = $"buckerino-{n}-{i}",
                                    Text = $"THIS IS TITLE {i}"
                                })
                                .ToList(),
                        })
                        .ToList()
                },

                SomeKindOfListOfStrings = Enumerable.Range(0, 20)
                    .Select(n => $"THIS NO {n}")
                    .ToList()
            });
        });

        Console.WriteLine(exception);
    }

    public class SomeKindOfRequest
    {
        public SomeKindOfRequestModel SomeKindOfRequestModel { get; set; }
        public List<string> SomeKindOfListOfStrings { get; set; }
    }

    public class SomeKindOfRequestModel : SomeKindOfRequestModelBase
    {
        public DateTimeOffset SomeTimeUtc { get; set; }
        public DateTimeOffset? AnotherTimeUtc { get; set; }
    }

    public enum SomeKindOfEnumerationOfSomething { Whatever }

    public class SomeKindOfRequestModelBase
    {
        public string ThisIsString { get; set; }

        public Guid ThisIsGuid { get; set; }

        public string Name { get; set; }

        public DateTimeOffset ThisIsDateTimeOffset { get; set; }

        public List<SomeKindOfItemModel> HereWeHaveItems { get; set; }

        public class SomeKindOfItemModel
        {
            public List<SubItemModel> SubItems { get; set; }
        }

        public class SubItemModel
        {
            public string Text { get; set; }

            public string Name { get; set; }

            public List<SubSubItemModel> SubSubItems { get; set; }
        }

        public class SubSubItemModel
        {
            public int Number { get; set; }

            public string Name { get; set; }
        }
    }
}