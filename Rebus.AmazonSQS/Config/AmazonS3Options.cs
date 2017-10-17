namespace Rebus.Config
{
    /// <summary>
    /// Holds all of the exposed S3 options which can be applied when using the extended SQS client.
    /// </summary>
    public class AmazonS3Options
    {
        /// <summary>
        /// Sets the name of the S3 bucket to be used for sending large messages using the extended SQS client.
        /// </summary>
        public string BucketName { get; set; }
    }
}