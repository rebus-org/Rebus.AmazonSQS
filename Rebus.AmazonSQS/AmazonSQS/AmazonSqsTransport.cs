﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SQS;
using Amazon.SQS.Model;
using Rebus.Bus;
using Rebus.Config;
using Rebus.Exceptions;
using Rebus.Extensions;
using Rebus.Internals;
using Rebus.Logging;
using Rebus.Messages;
using Rebus.Threading;
using Rebus.Time;
using Rebus.Transport;
using Message = Amazon.SQS.Model.Message;
// ReSharper disable EmptyGeneralCatchClause
#pragma warning disable 1998

namespace Rebus.AmazonSQS;

/// <summary>
/// Implementation of <see cref="ITransport"/> that uses Amazon Simple Queue Service to move messages around
/// </summary>
public class AmazonSqsTransport : ITransport, IInitializable, IDisposable
{
    const string OutgoingMessagesItemsKey = "SQS_OutgoingMessages";
    const string MessageGroupIdHeader = "MessageGroupId";
    const string MessageDeduplicationIdHeader = "MessageDeduplicationId";

    readonly AmazonSQSTransportMessageSerializer _serializer = new();
    readonly ConcurrentDictionary<string, string> _queueUrls = new();
    readonly IAsyncTaskFactory _asyncTaskFactory;
    readonly AmazonSQSTransportOptions _options;
    readonly IRebusTime _rebusTime;
    readonly ILog _log;
    readonly IAmazonSQS _client;

    TimeSpan _peekLockDuration = TimeSpan.FromMinutes(5);
    TimeSpan _peekLockRenewalInterval = TimeSpan.FromMinutes(4);
    string _queueUrl;
    bool _disposed;

    /// <summary>
    /// Constructs the transport with the specified settings
    /// </summary>
    public AmazonSqsTransport(string inputQueueAddress, IRebusLoggerFactory rebusLoggerFactory, IAsyncTaskFactory asyncTaskFactory, AmazonSQSTransportOptions options, IRebusTime rebusTime)
    {
        if (rebusLoggerFactory == null) throw new ArgumentNullException(nameof(rebusLoggerFactory));

        _options = options ?? throw new ArgumentNullException(nameof(options));
        _rebusTime = rebusTime ?? throw new ArgumentNullException(nameof(rebusTime));
        _asyncTaskFactory = asyncTaskFactory ?? throw new ArgumentNullException(nameof(asyncTaskFactory));
        _log = rebusLoggerFactory.GetLogger<AmazonSqsTransport>();

        if (inputQueueAddress != null)
        {
            if (inputQueueAddress.Contains("/") && !Uri.IsWellFormedUriString(inputQueueAddress, UriKind.Absolute))
            {
                var message = $"The input queue address '{inputQueueAddress}' is not valid - please either use a simple queue name (eg. 'my-queue') or a full URL for the queue endpoint (e.g. 'https://sqs.eu-central-1.amazonaws.com/234234234234234/somqueue').";

                throw new ArgumentException(message, nameof(inputQueueAddress));
            }
        }

        Address = inputQueueAddress;

        _log.Info("Initializing SQS client");

        _client = _options.ClientFactory();
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

        if (_options.CreateQueues)
        {
            CreateQueue(Address);
        }

        _queueUrl = GetInputQueueUrl();
    }

    string GetInputQueueUrl()
    {
        try
        {
            return GetDestinationQueueUrlByName(Address);
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
        if (!_options.CreateQueues) return;

        _log.Info("Creating queue {queueName}", address);

        var queueName = GetQueueNameFromAddress(address);

        AsyncHelpers.RunSync(async () =>
        {
            // Check if queue exists
            try
            {
                // See http://docs.aws.amazon.com/sdkfornet/v3/apidocs/items/SQS/TSQSGetQueueUrlRequest.html for options
                var request = new GetQueueUrlRequest(queueName);
                var response = await _client.GetQueueUrlAsync(request);

                if (response.HttpStatusCode != HttpStatusCode.OK)
                {
                    throw new Exception($"Could not check for existing queue '{queueName}' - got HTTP {response.HttpStatusCode}");
                }

                // See http://docs.aws.amazon.com/sdkfornet/v3/apidocs/items/SQS/TSQSSetQueueAttributesRequest.html for options
                var setAttributesResponse = await _client.SetQueueAttributesAsync(response.QueueUrl, new Dictionary<string, string>
                {
                    ["VisibilityTimeout"] = ((int)_peekLockDuration.TotalSeconds).ToString(CultureInfo.InvariantCulture)
                });
                if (setAttributesResponse.HttpStatusCode != HttpStatusCode.OK)
                {
                    throw new Exception($"Could not set attributes for queue '{queueName}' - got HTTP {setAttributesResponse.HttpStatusCode}");
                }
            }
            catch (QueueDoesNotExistException)
            {
                // See http://docs.aws.amazon.com/sdkfornet/v3/apidocs/items/SQS/TSQSCreateQueueRequest.html for options
                var createQueueRequest = new CreateQueueRequest(queueName)
                {
                    Attributes = new Dictionary<string, string>
                    {
                        ["VisibilityTimeout"] = ((int) _peekLockDuration.TotalSeconds).ToString(CultureInfo.InvariantCulture)
                    }
                };

                var response = await _client.CreateQueueAsync(createQueueRequest);

                if (response.HttpStatusCode != HttpStatusCode.OK)
                {
                    throw new Exception($"Could not create queue '{queueName}' - got HTTP {response.HttpStatusCode}");
                }
            }
        });
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
            var stopwatch = Stopwatch.StartNew();

            AsyncHelpers.RunSync(async () =>
            {
                while (true)
                {
                    var request = new ReceiveMessageRequest(_queueUrl) { MaxNumberOfMessages = 10 };
                    var response = await _client.ReceiveMessageAsync(request);
                    if (response.Messages?.Any() is not true) break;

                    var entries = response.Messages
                        .Select(m => new DeleteMessageBatchRequestEntry(m.MessageId, m.ReceiptHandle))
                        .ToList();

                    var deleteResponse = await _client.DeleteMessageBatchAsync(_queueUrl, entries);
                    if (deleteResponse.Failed?.Any() is not true) continue;

                    var errors = string.Join(Environment.NewLine,
                        deleteResponse.Failed.Select(f => $"{f.Message} ({f.Id})"));

                    throw new RebusApplicationException($@"Error {deleteResponse.HttpStatusCode} while purging: {errors}");
                }

            });

            _log.Info("Purging {queueName} took {elapsedSeconds} s", Address, stopwatch.Elapsed.TotalSeconds);
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
            var messagesToSend = new ConcurrentQueue<OutgoingMessage>();

            context.OnCommit(async _ => await SendOutgoingMessages(messagesToSend));

            return messagesToSend;
        });

        outgoingMessages.Enqueue(new OutgoingMessage(GetActualDestinationAddress(destinationAddress, message), message));
    }

    string GetActualDestinationAddress(string destinationAddress, TransportMessage message)
    {
        if (_options.UseNativeDeferredMessages && message.Headers.TryGetValue(Headers.DeferredRecipient, out var deferredRecipient))
        {
            message.Headers.Remove(Headers.DeferredRecipient);
            return deferredRecipient;
        }

        return destinationAddress;
    }

    async Task SendOutgoingMessages(ConcurrentQueue<OutgoingMessage> outgoingMessages)
    {
        if (!outgoingMessages.Any()) return;

        var messagesByDestination = outgoingMessages
            .GroupBy(m => m.DestinationAddress)
            .ToList();

        await Task.WhenAll(
            messagesByDestination
                .Select(async batch =>
                {
                    var entries = batch
                        .Select(GetBatchRequestEntry)
                        .ToList();

                    var destinationUrl = GetDestinationQueueUrlByName(batch.Key);

                    foreach (var batchToSend in entries.Batch(_options.MessageBatchSize))
                    {
                        var request = new SendMessageBatchRequest(destinationUrl, batchToSend);
                        var response = await _client.SendMessageBatchAsync(request);

                        if (response.Failed?.Any() == true)
                        {
                            var failed = response.Failed
                                .Select(f => new AmazonSQSException($"Failed {f.Message} with Id={f.Id}, Code={f.Code}, SenderFault={f.SenderFault}"));

                            throw new AggregateException(failed);
                        }
                    }
                })
        );
    }

    SendMessageBatchRequestEntry GetBatchRequestEntry(OutgoingMessage message)
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

        if (message.DestinationAddress.EndsWith(".fifo"))
        {
            if (headers.TryGetValue(MessageGroupIdHeader, out var messageGroupId))
            {
                entry.MessageGroupId = messageGroupId;
            }

            if (headers.TryGetValue(MessageDeduplicationIdHeader, out var messageDeduplicationId))
            {
                entry.MessageDeduplicationId = messageDeduplicationId;
            }
        }

        return entry;
    }

    int? GetDelaySeconds(IReadOnlyDictionary<string, string> headers)
    {
        if (!_options.UseNativeDeferredMessages) return null;

        if (!headers.TryGetValue(Headers.DeferredUntil, out var deferUntilTime)) return null;

        var deferUntilDateTimeOffset = deferUntilTime.ToDateTimeOffset();

        var delay = (int)Math.Ceiling((deferUntilDateTimeOffset - _rebusTime.Now).TotalSeconds);

        // SQS will only accept delays between 0 and 900 seconds.
        // In the event that the value for deferreduntil is before the current date, the message should be processed immediately. i.e. with a delay of 0 seconds.
        return Math.Max(delay, 0);
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

        var request = new ReceiveMessageRequest(_queueUrl)
        {
            MaxNumberOfMessages = 1,
            WaitTimeSeconds = _options.ReceiveWaitTimeSeconds,
            MessageSystemAttributeNames = new List<string>(new[] { "All" }),
            MessageAttributeNames = new List<string>(new[] { "All" })
        };

        var response = await _client.ReceiveMessageAsync(request, CancellationToken.None);

        if (response.Messages?.Any() is not true) return null;

        var sqsMessage = response.Messages.First();

        var renewalTask = CreateRenewalTaskForMessage(sqsMessage, _client);

        context.OnAck(async _ =>
        {
            renewalTask.Dispose();

            // if we get this far, we don't want to pass on the cancellation token
            // ReSharper disable once MethodSupportsCancellation
            await _client.DeleteMessageAsync(new DeleteMessageRequest(_queueUrl, sqsMessage.ReceiptHandle));
        });

        context.OnNack(async _ =>
        {
            TimeSpan GetDefault(ITransactionContext _) => TimeSpan.FromSeconds(0);
            var timeoutFunction = _options.GetVisibilityTimeoutOnAbort ?? GetDefault;
            var timeout = timeoutFunction(context);
            var timeoutSeconds = (int)timeout.TotalSeconds;

            renewalTask.Dispose();

            await _client.ChangeMessageVisibilityAsync(_queueUrl, sqsMessage.ReceiptHandle, timeoutSeconds, cancellationToken);
        });

        var transportMessage = ExtractTransportMessageFrom(sqsMessage);
        if (MessageIsExpired(transportMessage, sqsMessage))
        {
            // if the message is expired , we don't want to pass on the cancellation token
            // ReSharper disable once MethodSupportsCancellation
            await _client.DeleteMessageAsync(new DeleteMessageRequest(_queueUrl, sqsMessage.ReceiptHandle));
            return null;
        }
        renewalTask.Start();
        return transportMessage;
    }

    IAsyncTask CreateRenewalTaskForMessage(Message message, IAmazonSQS client)
    {
        return _asyncTaskFactory.Create(
            $"RenewPeekLock-{message.MessageId}",
            async () =>
            {
                _log.Info("Renewing peek lock for message with ID {messageId}", message.MessageId);

                var request = new ChangeMessageVisibilityRequest(_queueUrl, message.ReceiptHandle, (int)_peekLockDuration.TotalSeconds);

                await client.ChangeMessageVisibilityAsync(request);
            },
            intervalSeconds: (int)_peekLockRenewalInterval.TotalSeconds,
            prettyInsignificant: true
        );
    }

    bool MessageIsExpired(TransportMessage message, Message sqsMessage)
    {
        if (!message.Headers.TryGetValue(Headers.TimeToBeReceived, out var value)) return false;

        var timeToBeReceived = TimeSpan.Parse(value);

        if (MessageIsExpiredUsingRebusSentTime(message, timeToBeReceived)) return true;
        if (MessageIsExpiredUsingNativeSqsSentTimestamp(sqsMessage, timeToBeReceived)) return true;

        return false;
    }

    bool MessageIsExpiredUsingRebusSentTime(TransportMessage message, TimeSpan timeToBeReceived)
    {
        if (!message.Headers.TryGetValue(Headers.SentTime, out var rebusUtcTimeSentAttributeValue)) return false;

        var rebusUtcTimeSent = DateTimeOffset.ParseExact(rebusUtcTimeSentAttributeValue, "O", null);

        return _rebusTime.Now.UtcDateTime - rebusUtcTimeSent > timeToBeReceived;
    }

    bool MessageIsExpiredUsingNativeSqsSentTimestamp(Message message, TimeSpan timeToBeReceived)
    {
        if (!message.Attributes.TryGetValue("SentTimestamp", out var sentTimeStampString)) return false;

        var sentTime = GetTimeFromUnixTimestamp(sentTimeStampString);

        return _rebusTime.Now.UtcDateTime - sentTime > timeToBeReceived;
    }

    static DateTime GetTimeFromUnixTimestamp(string sentTimeStampString)
    {
        var unixTime = long.Parse(sentTimeStampString);
        var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var sentTime = epoch.AddMilliseconds(unixTime);
        return sentTime;
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

    static byte[] GetBodyBytes(string bodyText)
    {
        return Convert.FromBase64String(bodyText);
    }

    string GetDestinationQueueUrlByName(string address)
    {
        var url = _queueUrls.GetOrAdd(address.ToLowerInvariant(), _ =>
        {
            return AsyncHelpers.GetSync(async () =>
            {
                if (Uri.IsWellFormedUriString(address, UriKind.Absolute))
                {
                    return address;
                }

                var response = await _client.GetQueueUrlAsync(address);

                if (response.HttpStatusCode == HttpStatusCode.OK)
                {
                    return response.QueueUrl;
                }

                throw new RebusApplicationException($"could not find Url for address: {address} - got errorcode: {response.HttpStatusCode}");
            });
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
        AsyncHelpers.RunSync(() => _client.DeleteQueueAsync(_queueUrl));
    }

    /// <summary>
    /// Disposes the Amazon SQS client associated with this transport
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        _log.Info("Disposing SQS client");

        try
        {
            _client?.Dispose();
        }
        finally
        {
            _disposed = true;
        }
    }
}