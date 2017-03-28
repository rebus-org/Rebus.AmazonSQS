using System;
using System.Linq;

namespace Rebus.AmazonSQS.Tests
{
    public class ConnectionInfo
    {
        internal string AccessKeyId;
        internal string SecretAccessKey;
        internal string RegionEndpoint;
        /// <summary>
        /// Expects format Key=Value¤Key=Value¤Key=Value
        /// Ie. AccessKeyId=xxxxx¤SecretAccessKey=yyyy¤BaseQueueUrl=asdasdas¤RegionEndpoint=asdasd
        /// </summary>
        /// <param name="textString"></param>
        /// <returns></returns>
        internal static ConnectionInfo CreateFromString(string textString)
        {
            Console.WriteLine("Parsing connectionInfo from string: {0}", textString);
            
            var keyValuePairs = textString.Split("; ".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            
            Console.WriteLine(@"Found {0} pairs. Expected 3 on the form

AccessKeyId=blabla; SecretAccessKey=blablalba; RegionEndpoint=something

", keyValuePairs.Length);
            try
            {
                var keysAndValues = keyValuePairs.ToDictionary((kv) => kv.Split('=')[0], (kv) => kv.Split('=')[1]);
                return new ConnectionInfo
                {
                    AccessKeyId = keysAndValues["AccessKeyId"],
                    SecretAccessKey = keysAndValues["SecretAccessKey"],
                    RegionEndpoint = keysAndValues["RegionEndpoint"]
                };

            }
            catch (Exception exception)
            {

                Console.WriteLine("Could not extract keys and values from textstring. Ensure that the key and values are split by = \nand that the Key values used are: AccessKeyId, SecretAccessKey and BaseQueueUrl");
                Console.WriteLine("\nException message: {0}", exception.Message);

                throw;
            }
        }

    }
}