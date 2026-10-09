namespace CourierService.Services.Notifications
{
    /// <summary>One ready-to-send notification, built from a queue item and its package.</summary>
    public class NotificationMessage
    {
        public int NotificationQueueId { get; set; }
        public int PackageId { get; set; }
        public string F20Identifier { get; set; }
        public string Channel { get; set; }
        public string TemplateKey { get; set; }

        /// <summary>Email address or phone number. Personal data: never write it to a log (SR-03).</summary>
        public string To { get; set; }

        public string Subject { get; set; }
        public string Body { get; set; }
    }

    /// <summary>What a sender reports back to the worker.</summary>
    public class NotificationSendResult
    {
        private NotificationSendResult()
        {
        }

        public bool Success { get; private set; }

        /// <summary>A failure that trying again won't fix, e.g. an address that can't exist. The worker stops retrying.</summary>
        public bool Permanent { get; private set; }

        /// <summary>Why it failed, for the log. Must not contain the recipient's address or name (SR-03).</summary>
        public string Error { get; private set; }

        public static NotificationSendResult Sent()
        {
            return new NotificationSendResult { Success = true };
        }

        public static NotificationSendResult Failed(string error, bool permanent = false)
        {
            return new NotificationSendResult { Success = false, Error = error, Permanent = permanent };
        }
    }
}