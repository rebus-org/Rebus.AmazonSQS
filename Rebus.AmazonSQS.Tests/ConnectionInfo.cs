using System;
using System.Linq;
using Amazon;

namespace Rebus.AmazonSQS.Tests
{
    public class ConnectionInfo
    {
        public string AccessKeyId { get; }
        public string SecretAccessKey { get; }
        public RegionEndpoint RegionEndpoint { get; }

        ConnectionInfo(string accessKeyId, string secretAccessKey, string regionEndpointName)
        {
            AccessKeyId = accessKeyId;
            SecretAccessKey = secretAccessKey;
            RegionEndpoint = GetRegionEndpoint(regionEndpointName);
        }

        public static ConnectionInfo CreateFromString(string textString)
        {
            Console.WriteLine("Parsing connectionInfo from string: {0}", textString);
            
            var keyValuePairs = textString.Split("; ".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            
            try
            {
                var keysAndValues = keyValuePairs
                    .Select(kvp => kvp.Split('='))
                    .ToDictionary(kv => kv.First(), kv => kv.Last());

                return new ConnectionInfo(
                    keysAndValues["AccessKeyId"],
                    keysAndValues["SecretAccessKey"],
                    keysAndValues["RegionEndpoint"]
                );

            }
            catch (Exception exception)
            {
                throw new FormatException($"Could not extract access key ID, secret access key, and region endpoint from this: '{textString}' - expected the form 'AccessKeyId=blabla; SecretAccessKey=blablalba; RegionEndpoint=something'", exception);
            }
        }

        static RegionEndpoint GetRegionEndpoint(string regionEndpointName)
        {
            try
            {
                return RegionEndpoint.GetBySystemName(regionEndpointName);
            }
            catch (Exception exception)
            {
                throw new FormatException($"The region endpoint '{regionEndpointName}' could not be recognized", exception);
            }
        }
    }
}