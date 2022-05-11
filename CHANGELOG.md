# Changelog

## 2.0.0-a1
* Test release

## 2.0.0-a4
* Test release again

## 2.0.0-b01
* Test release

## 2.0.0-b02
* More convenient namespaces

## 2.0.0
* Update AWS Core and SQS deps to 3.3.0
* Release 2.0.0

## 2.1.0
* Add configuration extension that accepts `AWSCredentials` - thanks [MooseMagnet]

## 3.0.0
* Update to Rebus 3

## 4.0.0
* Update to Rebus 4
* Add .NET Core support (netstandard 1.3)
* Change message format to break out of SQS's limitation of being able to transfer only 10 headers - thanks [mvandevy]
* Fix csproj - thanks [robvanpamel]
* Add ability to use an external timeout manager or Rebus' ordinary timeout managers to overcome SQS limitations
* Add ability to skip queue creation - thanks [robvanpamel]
* Remember to configure visibility timeout for queues created by Rebus - thanks [micdah]

## 4.0.1
* Additional one-way client configuration overloads - thanks [jonathanyong81]

## 4.0.2
* Fix bug that did not limit size of sent message batches to 10

## 4.0.3
* Do not create queues when configuration says not to - thanks [robvanpamel]

## 4.1.0
* Add configurable factory method for customizing which implementation of `IAmazonSQS` is used - thanks [dietermijle]

## 4.1.1
* Fixed issue where invalid delay value was being sent to SQS - thanks [ajacksms] :)

## 5.0.0-b03
* Change all use of `AmazonSQSClient` to use instance returned from the options passed to the transport, and keep the instance for its entire lifetime
* Enable falling back to EC2 roles by leaving out credentials when configuring Rebus
* Allow for using FIFO queues by picking up `MessageGroupId` and `MessageDeduplicationId` from Rebus headers and setting them on queue request entries - thanks [knepe]

## 6.0.0
* Update to Rebus 6

## 6.1.0
* Update aqssdk.sqs to 3.3.103 and Rebus to 6.3.1 - thanks [jcmdsbr]

## 6.1.1
* Update aqssdk.sqs to 3.3.103.21 - thanks [jcmdsbr]

## 6.1.2
* Fix bug that would cause deferred messages to not adhere to Rebus' routing configuration

## 6.2.0
* Make outgoing message batch size configurable - thanks [dietermijle]

## 6.3.0
* Update awssdk.sqs dependency to 3.5.1.20
* Remove dependency on that funny security token nuggie

## 6.3.1
* Fix bug that would cause inability to manually dead-letter messages

## 6.3.2
* Pass `CancellationToken.None` to receive call of SQS client, to avoid leaving unacked message on the server when shutting down - thanks [xhafan]

## 6.4.0
* Add ability to provide a function that customizes the visibility timeout when message delivery has failed

## 6.5.0
* Update awssdk.sqs dep to 3.7.1 because that gives a lot of flexibility

---

[ajacksms]: https://github.com/ajacksms
[dietermijle]: https://github.com/dietermijle
[jcmdsbr]: https://github.com/jcmdsbr
[jonathanyong81]: https://github.com/jonathanyong81
[knepe]: https://github.com/knepe
[micdah]: https://github.com/micdah
[MooseMagnet]: https://github.com/MooseMagnet
[mvandevy]: https://github.com/mvandevy
[robvanpamel]: https://github.com/robvanpamel
[xhafan]: https://github.com/xhafan