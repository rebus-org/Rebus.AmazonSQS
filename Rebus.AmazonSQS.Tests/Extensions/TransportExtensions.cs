using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Rebus.Bus;
using Rebus.Messages;
using Rebus.Transport;

namespace Rebus.AmazonSQS.Tests.Extensions
{
    public static class TransportExtensions
    {
        public static async Task<List<TransportMessage>> ReceiveAll(this ITransport transport, int quietTimeSeconds = 2)
        {
            var quietTime = TimeSpan.FromSeconds(quietTimeSeconds);
            var transportMessages = new List<TransportMessage>();
            var lastMessageReceived = DateTime.Now;

            while (true)
            {
                using (var scope = new RebusTransactionScope())
                {
                    var message = await transport.Receive(scope.TransactionContext, CancellationToken.None);

                    await scope.CompleteAsync();

                    if (message != null)
                    {
                        Console.WriteLine($"Got a message: {message.GetMessageId()}");
                        transportMessages.Add(message);
                        lastMessageReceived = DateTime.Now;
                    }
                    else
                    {
                        Console.WriteLine("Waiting 0.5 s");
                        await Task.Delay(500);
                    }
                }

                var elapsedSinceLastMessageReceived = DateTime.Now - lastMessageReceived;

                if (elapsedSinceLastMessageReceived > quietTime)
                {
                    break;
                }
            }

            return transportMessages;
        }
    }
}