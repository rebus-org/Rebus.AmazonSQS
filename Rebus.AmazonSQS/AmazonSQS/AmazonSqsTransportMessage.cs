using System.Collections.Generic;
using Newtonsoft.Json;

namespace Rebus.AmazonSQS;

class AmazonSQSTransportMessage
{
    [JsonProperty(PropertyName = "headers")]
    public Dictionary<string, string> Headers { get; set; }

    [JsonProperty(PropertyName = "body")]
    public string Body { get; set; }

    public AmazonSQSTransportMessage()
        : this(null, null)
    {}

    public AmazonSQSTransportMessage(Dictionary<string, string> headers, string body)
    {
        Headers = headers ?? new Dictionary<string, string>();
        Body = body ?? string.Empty;
    }
}