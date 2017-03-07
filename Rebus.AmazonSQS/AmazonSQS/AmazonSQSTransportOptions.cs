using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rebus.AmazonSQS
{
    public class AmazonSQSTransportOptions
    {
        /// <summary>
        /// Flag set to collapse all Rebus Core headers into a single header in order to limit the number of 
        /// headers set on a message. This flag can be enabled in case you want to add more headers as Rebus already
        /// adds 9 header while the SQS limit is 10. In case you add a single header and the message needs to be 
        /// moved to the error queue, then this fails.
        /// </summary>
        public bool CollapseCoreHeaders { get; set; }

        /// <summary>
        /// Sets the WaitTimeSeconds on the ReceiveMessage. The default setting is 1, which enables long
        /// polling for a single second. The number of seconds can be set up to 20 seconds. 
        /// In case no long polling is desired, then set the value to 0.
        /// </summary>
        public int ReceiveWaitTimeSeconds { get; set; }

        public AmazonSQSTransportOptions()
        {
            CollapseCoreHeaders = false;
            ReceiveWaitTimeSeconds = 1;
        }
    }
}
