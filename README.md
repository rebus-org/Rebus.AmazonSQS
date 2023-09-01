# Rebus.AmazonSQS

[![install from nuget](https://img.shields.io/nuget/v/Rebus.AmazonSQS.svg?style=flat-square)](https://www.nuget.org/packages/Rebus.AmazonSQS)

Provides an [Amazon SQS](https://aws.amazon.com/sqs/) transport for [Rebus](https://github.com/rebus-org/Rebus).

![](https://raw.githubusercontent.com/rebus-org/Rebus/master/artwork/little_rebusbus2_copy-200x200.png)

---


# Required AWS security policies
The policy required for receiving messages from a SQS queue using "least-privilege principle" is as follows:
```
{
    "Version": "2012-10-17",
    "Statement": [
        {
            "Effect": "Allow",
            "Action": [
                "sqs:DeleteMessage",
                "sqs:GetQueueUrl",
                "sqs:ChangeMessageVisibility",
                "sqs:DeleteMessageBatch",
                "sqs:SendMessageBatch",
                "sqs:SendMessage",
                "sqs:ReceiveMessage",
                "sqs:DeleteQueue",
                "sqs:CreateQueue",
                "sqs:SetQueueAttributes"
            ],
            "Resource": "[Your SQS incoming queue and DLQ arn here]"
        }
    ]
}
```
Note that the last three actions(`sqs:DeleteQueue`, `sqs:CreateQueue` and `sqs:SetQueueAttribute`) is only required if `AmazonSQSTransportOptions.CreateQueue` is set to `true`, otherwise they could/should be omitted. It is the default behaviour that CreateQueue is set to true, hence they are included in the above ACL.

To be able to send to a queue the following permissions are required on the target queue:
```
{
    "Version": "2012-10-17",
    "Statement": [
        {
            "Effect": "Allow",
            "Action": [
                "sqs:GetQueueUrl",
                "sqs:SendMessageBatch",
            ],
            "Resource": "[Your SQS outgoing target queue arn here]"
        }
    ]
}
```
