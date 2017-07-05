using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Runtime;
using Amazon.SQS;
using Amazon.SQS.Model;
using Rebus.Bus;
using Rebus.Config;
using Rebus.Exceptions;
using Rebus.Extensions;
using Rebus.Logging;
using Rebus.Messages;
using Rebus.Threading;
using Rebus.Time;
using Rebus.Transport;
using Message = Amazon.SQS.Model.Message;
#pragma warning disable 1998

namespace Rebus.AmazonSQS
{
    /// <summary>
    /// Implementation of <see cref="ITransport"/> that uses Amazon Simple Queue Service to move messages around
    /// </summary>
    public class AmazonSQSTransport : ITransport, IInitializable
    {
        const string ClientContextKey = "SQS_Client";
        const string OutgoingMessagesItemsKey = "SQS_OutgoingMessages";

        readonly AmazonSQSTransportMessageSerializer _serializer = new AmazonSQSTransportMessageSerializer();
        readonly AWSCredentials _credentials;
        readonly AmazonSQSConfig _amazonSqsConfig;
        readonly IAsyncTaskFactory _asyncTaskFactory;
        readonly AmazonSQSTransportOptions _options;
        readonly ILog _log;

        TimeSpan _peekLockDuration = TimeSpan.FromMinutes(5);
        TimeSpan _peekLockRenewalInterval = TimeSpan.FromMinutes(4);
        string _queueUrl;

        /// <summary>
        /// Constructs the transport with the specified settings
        /// </summary>
        public AmazonSQSTransport(string inputQueueAddress, AWSCredentials credentials, AmazonSQSConfig amazonSqsConfig, IRebusLoggerFactory rebusLoggerFactory, IAsyncTaskFactory asyncTaskFactory, AmazonSQSTransportOptions options = null)
        {
            if (credentials == null) throw new ArgumentNullException(nameof(credentials));
            if (amazonSqsConfig == null) throw new ArgumentNullException(nameof(amazonSqsConfig));
            if (rebusLoggerFactory == null) throw new ArgumentNullException(nameof(rebusLoggerFactory));

            _log = rebusLoggerFactory.GetLogger<AmazonSQSTransport>();

            if (inputQueueAddress != null)
            {
                if (inputQueueAddress.Contains("/") && !Uri.IsWellFormedUriString(inputQueueAddress, UriKind.Absolute))
                {
                    var message = $"The input queue address '{inputQueueAddress}' is not valid - please either use a simple queue name (eg. 'my-queue') or a full URL for the queue endpoint (e.g. 'https://sqs.eu-central-1.amazonaws.com/234234234234234/somqueue').";

                    throw new ArgumentException(message, nameof(inputQueueAddress));
                }
            }

            Address = inputQueueAddress;

            _credentials = credentials;
            _amazonSqsConfig = amazonSqsConfig;
            _asyncTaskFactory = asyncTaskFactory;
            _options = options ?? new AmazonSQSTransportOptions();
        }

        /// <summary>
        /// Public initialization method that allows for configuring the peek lock duration. Mostly useful for tests.
        /// </summary>
        public void Initialize(TimeSpan peeklockDuration)
        {
            _peekLockDuration = peeklockDuration;
            _peekLockRenewalInterval = TimeSpan.FromMinutes(_peekLockDuration.TotalMinutes * 0.8);

            Initialize();
        }

        /// <summary>
        /// Initializes the transport by creating the input queue
        /// </summary>
        public void Initialize()
        {
            if (Address == null) return;
            if (_options.CreateQueues) CreateQueue(Address);
            _queueUrl = GetInputQueueUrl();
        }

        string GetInputQueueUrl()
        {
            try
            {
                using (var scope = new RebusTransactionScope())
                {
                    var inputQueueUrl = GetDestinationQueueUrlByName(Address, scope.TransactionContext);

                    return inputQueueUrl;
                }
            }
            catch (Exception exception)
            {
                throw new RebusApplicationException(exception, $"Could not get URL of own input queue '{Address}'");
            }
        }

        /// <summary>
        /// Creates the queue with the given name
        /// </summary>
        public void CreateQueue(string address)
        {
            _log.Info("Creating queue {queueName} on region {regionEndpoint}", address, _amazonSqsConfig.RegionEndpoint);

            using (var client = new AmazonSQSClient(_credentials, _amazonSqsConfig))
            {
                var queueName = GetQueueNameFromAddress(address);

                // See http://docs.aws.amazon.com/sdkfornet/v3/apidocs/items/SQS/TSQSCreateQueueRequest.html for options
                var createQueueRequest = new CreateQueueRequest(queueName)
                {
                    Attributes =
                    {
                        ["VisibilityTimeout"] = ((int) _peekLockDuration.TotalSeconds).ToString(CultureInfo.InvariantCulture)
                    }
                };
                var task = client.CreateQueueAsync(createQueueRequest);
                AsyncHelpers.RunSync(() => task);
                var response = task.Result;

                if (response.HttpStatusCode != HttpStatusCode.OK)
                {
                    throw new Exception($"Could not create queue '{queueName}' - got HTTP {response.HttpStatusCode}");
                }
            }
        }

        /// <summary>
        /// Deletes all messages from the input queue (which is done by receiving them in batches and deleting them, as long as it takes)
        /// </summary>
        public void Purge()
        {
            if (Address == null) return;

            _log.Info("Purging queue {queueName}", Address);

            try
            {
                // we purge the queue by receiving all messages as fast as we can...
                //the native purge function is not used because it is only allowed to use it
                // once every 60 s
                using (var client = new AmazonSQSClient(_credentials, _amazonSqsConfig))
                {
                    var stopwatch = Stopwatch.StartNew();

                    while (true)
                    {
                        var request = new ReceiveMessageRequest(_queueUrl) { MaxNumberOfMessages = 10 };
                        var receiveTask = client.ReceiveMessageAsync(request);
                        AsyncHelpers.RunSync(() => receiveTask);
                        var response = receiveTask.Result;

                        if (!response.Messages.Any()) break;

                        var deleteTask = client.DeleteMessageBatchAsync(_queueUrl, response.Messages
                            .Select(m => new DeleteMessageBatchRequestEntry(m.MessageId, m.ReceiptHandle))
                            .ToList());

                        AsyncHelpers.RunSync(() => deleteTask);

                        var deleteResponse = deleteTask.Result;

                        if (deleteResponse.Failed.Any())
                        {
                            var errors = string.Join(Environment.NewLine,
                                deleteResponse.Failed.Select(f => $"{f.Message} ({f.Id})"));

                            throw new RebusApplicationException($@"Error {deleteResponse.HttpStatusCode} while purging: {errors}");
                        }
                    }

                    _log.Info("Purging {queueName} took {elapsedSeconds} s", Address, stopwatch.Elapsed.TotalSeconds);
                }
            }
            catch (AmazonSQSException exception) when (exception.StatusCode == HttpStatusCode.BadRequest)
            {
                if (exception.Message.Contains("queue does not exist")) return;

                throw;
            }
            catch (Exception exception)
            {
                throw new RebusApplicationException(exception, $"Error while purging {Address}");
            }
        }

        class OutgoingMessage
        {
            public string DestinationAddress { get; }
            public TransportMessage TransportMessage { get; }

            public OutgoingMessage(string destinationAddress, TransportMessage transportMessage)
            {
                DestinationAddress = destinationAddress;
                TransportMessage = transportMessage;
            }
        }

        /// <inheritdoc />
        public async Task Send(string destinationAddress, TransportMessage message, ITransactionContext context)
        {
            if (destinationAddress == null) throw new ArgumentNullException(nameof(destinationAddress));
            if (message == null) throw new ArgumentNullException(nameof(message));
            if (context == null) throw new ArgumentNullException(nameof(context));

            var outgoingMessages = context.GetOrAdd(OutgoingMessagesItemsKey, () =>
            {
                var sendMessageBatchRequestEntries = new ConcurrentQueue<OutgoingMessage>();

                context.OnCommitted(() => SendOutgoingMessages(sendMessageBatchRequestEntries, context));

                return sendMessageBatchRequestEntries;
            });

            outgoingMessages.Enqueue(new OutgoingMessage(destinationAddress, message));
        }

        async Task SendOutgoingMessages(ConcurrentQueue<OutgoingMessage> outgoingMessages, ITransactionContext context)
        {
            if (!outgoingMessages.Any()) return;

            var client = GetClientFromTransactionContext(context);

            var messagesByDestination = outgoingMessages
                .GroupBy(m => m.DestinationAddress)
                .ToList();

            await Task.WhenAll(
                messagesByDestination
                    .Select(async batch =>
                    {
                        var entries = batch
                            .Select(message =>
                            {
                                var transportMessage = message.TransportMessage;
                                var headers = transportMessage.Headers;
                                var messageId = headers[Headers.MessageId];

                                var sqsMessage = new AmazonSQSTransportMessage(transportMessage.Headers, GetBody(transportMessage.Body));

                                var entry = new SendMessageBatchRequestEntry(messageId, _serializer.Serialize(sqsMessage));

                                var delaySeconds = GetDelaySeconds(headers);

                                if (delaySeconds != null)
                                {
                                    entry.DelaySeconds = delaySeconds.Value;
                                }

                                return entry;
                            })
                            .ToList();

                        var destinationUrl = GetDestinationQueueUrlByName(batch.Key, context);
                        var request = new SendMessageBatchRequest(destinationUrl, entries);
                        var response = await client.SendMessageBatchAsync(request);

                        if (response.Failed.Any())
                        {
                            var failed = response.Failed.Select(f => new AmazonSQSException($"Failed {f.Message} with Id={f.Id}, Code={f.Code}, SenderFault={f.SenderFault}"));

                            throw new AggregateException(failed);
                        }
                    })
                );
        }

        int? GetDelaySeconds(IReadOnlyDictionary<string, string> headers)
        {
            if (!_options.UseNativeDeferredMessages) return null;

            string deferUntilTime;
            if (!headers.TryGetValue(Headers.DeferredUntil, out deferUntilTime)) return null;

            var deferUntilDateTimeOffset = deferUntilTime.ToDateTimeOffset();

            var delay = (int)Math.Ceiling((deferUntilDateTimeOffset - RebusTime.Now).TotalSeconds);

            return delay;
        }

        /// <inheritdoc />
        public async Task<TransportMessage> Receive(ITransactionContext context, CancellationToken cancellationToken)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (Address == null)
            {
                throw new InvalidOperationException("This Amazon SQS transport does not have an input queue, hence it is not possible to reveive anything");
            }

            if (string.IsNullOrWhiteSpace(_queueUrl))
            {
                throw new InvalidOperationException("The queue URL is empty - has the transport not been initialized?");
            }

            var client = GetClientFromTransactionContext(context);

            var request = new ReceiveMessageRequest(_queueUrl)
            {
                MaxNumberOfMessages = 1,
                WaitTimeSeconds = _options.ReceiveWaitTimeSeconds,
                AttributeNames = new List<string>(new[] { "All" }),
                MessageAttributeNames = new List<string>(new[] { "All" })
            };

            var response = await client.ReceiveMessageAsync(request, cancellationToken);

            if (!response.Messages.Any()) return null;

            var sqsMessage = response.Messages.First();

            var renewalTask = CreateRenewalTaskForMessage(sqsMessage, client);

            context.OnCompleted(async () =>
            {
                renewalTask.Dispose();

                // if we get this far, we don't want to pass on the cancellation token
                // ReSharper disable once MethodSupportsCancellation
                await client.DeleteMessageAsync(new DeleteMessageRequest(_queueUrl, sqsMessage.ReceiptHandle));
            });

            context.OnAborted(() =>
            {
                renewalTask.Dispose();
                Task.Run(() => client.ChangeMessageVisibilityAsync(_queueUrl, sqsMessage.ReceiptHandle, 0, cancellationToken), cancellationToken).Wait(cancellationToken);
            });

            var transportMessage = ExtractTransportMessageFrom(sqsMessage);
            if (MessageIsExpired(transportMessage, sqsMessage))
            {
                // if the message is expired , we don't want to pass on the cancellation token
                // ReSharper disable once MethodSupportsCancellation
                await client.DeleteMessageAsync(new DeleteMessageRequest(_queueUrl, sqsMessage.ReceiptHandle));
                return null;
            }
            renewalTask.Start();
            return transportMessage;
        }

        IAsyncTask CreateRenewalTaskForMessage(Message message, AmazonSQSClient client)
        {
            return _asyncTaskFactory.Create($"RenewPeekLock-{message.MessageId}",
                async () =>
                {
                    _log.Info("Renewing peek lock for message with ID {messageId}", message.MessageId);

                    await
                        client.ChangeMessageVisibilityAsync(new ChangeMessageVisibilityRequest(_queueUrl,
                            message.ReceiptHandle, (int)_peekLockDuration.TotalSeconds));
                },
                intervalSeconds: (int)_peekLockRenewalInterval.TotalSeconds,
                prettyInsignificant: true);
        }

        static bool MessageIsExpired(TransportMessage message, Message sqsMessage)
        {
            string value;
            if (!message.Headers.TryGetValue(Headers.TimeToBeReceived, out value))
                return false;

            var timeToBeReceived = TimeSpan.Parse(value);

            if (MessageIsExpiredUsingRebusSentTime(message, timeToBeReceived)) return true;
            if (MessageIsExpiredUsingNativeSqsSentTimestamp(sqsMessage, timeToBeReceived)) return true;

            return false;
        }

        static bool MessageIsExpiredUsingRebusSentTime(TransportMessage message, TimeSpan timeToBeReceived)
        {
            string rebusUtcTimeSentAttributeValue;
            if (message.Headers.TryGetValue(Headers.SentTime, out rebusUtcTimeSentAttributeValue))
            {
                var rebusUtcTimeSent = DateTimeOffset.ParseExact(rebusUtcTimeSentAttributeValue, "O", null);

                if (RebusTime.Now.UtcDateTime - rebusUtcTimeSent > timeToBeReceived)
                {
                    return true;
                }
            }
            return false;

        }

        static bool MessageIsExpiredUsingNativeSqsSentTimestamp(Message message, TimeSpan timeToBeReceived)
        {
            string sentTimeStampString;
            if (message.Attributes.TryGetValue("SentTimestamp", out sentTimeStampString))
            {
                var sentTime = GetTimeFromUnixTimestamp(sentTimeStampString);
                if (RebusTime.Now.UtcDateTime - sentTime > timeToBeReceived)
                {
                    return true;
                }
            }
            return false;
        }

        static DateTime GetTimeFromUnixTimestamp(string sentTimeStampString)
        {
            var unixTime = long.Parse(sentTimeStampString);
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var sentTime = epoch.AddMilliseconds(unixTime);
            return sentTime;
        }

        AmazonSQSClient GetClientFromTransactionContext(ITransactionContext context)
        {
            return context.GetOrAdd(ClientContextKey, () =>
            {
                var amazonSqsClient = new AmazonSQSClient(_credentials, _amazonSqsConfig);
                context.OnDisposed(amazonSqsClient.Dispose);
                return amazonSqsClient;
            });
        }

        TransportMessage ExtractTransportMessageFrom(Message message)
        {
            var sqsMessage = _serializer.Deserialize(message.Body);
            return new TransportMessage(sqsMessage.Headers, GetBodyBytes(sqsMessage.Body));
        }

        static string GetBody(byte[] bodyBytes)
        {
            return Convert.ToBase64String(bodyBytes);
        }

        byte[] GetBodyBytes(string bodyText)
        {
            return Convert.FromBase64String(bodyText);
        }

        readonly ConcurrentDictionary<string, string> _queueUrls = new ConcurrentDictionary<string, string>();

        string GetDestinationQueueUrlByName(string address, ITransactionContext transactionContext)
        {
            var url = _queueUrls.GetOrAdd(address.ToLowerInvariant(), key =>
            {
                if (Uri.IsWellFormedUriString(address, UriKind.Absolute))
                {
                    return address;
                }

                var client = GetClientFromTransactionContext(transactionContext);
                var task = client.GetQueueUrlAsync(address);

                AsyncHelpers.RunSync(() => task);

                var urlResponse = task.Result;

                if (urlResponse.HttpStatusCode == HttpStatusCode.OK)
                {
                    return urlResponse.QueueUrl;
                }

                throw new RebusApplicationException($"could not find Url for address: {address} - got errorcode: {urlResponse.HttpStatusCode}");
            });

            return url;

        }

        static string GetQueueNameFromAddress(string address)
        {
            if (!Uri.IsWellFormedUriString(address, UriKind.Absolute)) return address;

            var queueFullAddress = new Uri(address);

            return queueFullAddress.Segments[queueFullAddress.Segments.Length - 1];
        }

        /// <summary>
        /// Gets the input queue name
        /// </summary>
        public string Address { get; }

        /// <summary>
        /// Deletes the transport's input queue
        /// </summary>
        public void DeleteQueue()
        {
            using (var client = new AmazonSQSClient(_credentials, _amazonSqsConfig))
            {
                AsyncHelpers.RunSync(() => client.DeleteQueueAsync(_queueUrl));
            }
        }
    }
}
