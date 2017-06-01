using Newtonsoft.Json;

namespace Rebus.AmazonSQS
{
    class AmazonSQSTransportMessageSerializer
    {
        public string Serialize(AmazonSQSTransportMessage message)
        {
            return JsonConvert.SerializeObject(message);
        }

        public AmazonSQSTransportMessage Deserialize(string value)
        {
            if (value == null) return null;
            return JsonConvert.DeserializeObject<AmazonSQSTransportMessage>(value);
        }
    }
}
