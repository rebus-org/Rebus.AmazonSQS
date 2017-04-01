using Newtonsoft.Json;

namespace Rebus.AmazonSQS
{
    class AmazonSqsTransportMessageSerializer
    {
        public string Serialize(AmazonSqsTransportMessage message)
        {
            return JsonConvert.SerializeObject(message);
        }

        public AmazonSqsTransportMessage Deserialize(string value)
        {
            if (value == null) return null;
            return JsonConvert.DeserializeObject<AmazonSqsTransportMessage>(value);
        }
    }
}
