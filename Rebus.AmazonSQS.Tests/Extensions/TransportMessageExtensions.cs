using System;
using System.Text;
using Newtonsoft.Json;
using Rebus.Messages;

namespace Rebus.AmazonSQS.Tests.Extensions;

static class TransportMessageExtensions
{
    public static T DeserializeTo<T>(this TransportMessage transportMessage)
    {
        var json = Encoding.UTF8.GetString(transportMessage.Body);

        try
        {
            return JsonConvert.DeserializeObject<T>(json);
        }
        catch (Exception exception)
        {
            throw new FormatException($@"Could not parse JSON

{json}

into {typeof(T)}", exception);
        }
    }
}