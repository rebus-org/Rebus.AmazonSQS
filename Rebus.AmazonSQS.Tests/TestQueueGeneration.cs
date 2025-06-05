using System;
using System.Net;
using System.Threading.Tasks;
using Amazon.Runtime;
using Amazon.SQS;
using Amazon.SQS.Model;
using NUnit.Framework;

namespace Rebus.AmazonSQS.Tests;

[TestFixture]
public class TestQueueGeneration
{
    [Test]
    public async Task Run()
    {
        var info = AmazonSqsTransportFactory.ConnectionInfo;
        var credentials = new BasicAWSCredentials(info.AccessKeyId, info.SecretAccessKey);
        var config = new AmazonSQSConfig{RegionEndpoint = info.RegionEndpoint};

        using var client = new AmazonSQSClient(credentials, config);
        
        var queueName = "test1";
        var response = await client.CreateQueueAsync(new CreateQueueRequest(queueName));

        if (response.HttpStatusCode != HttpStatusCode.OK)
        {
            throw new Exception($"Could not create queue '{queueName}' - got HTTP {response.HttpStatusCode}");
        }
    }
}