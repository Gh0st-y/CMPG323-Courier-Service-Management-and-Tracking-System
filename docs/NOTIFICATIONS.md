# Notifications (T25 queue and worker)

How a status change turns into an email or SMS, and where T26, T27, T28, T29 and T49 plug in.

## Flow

1. Storage staff move a package to Ready for Collection (`POST /api/packages/{id}/status`), or collection staff hand it over (`POST /api/packages/{id}/collect`).
2. `PackageStatusService` calls its listeners inside the status change's transaction. `NotificationEnqueuer` is one of them: it adds a `Pending` row to `dbo.NotificationQueue` with the same unit of work, so the row is committed or rolled back together with the status change. That is all that happens in the staff request, so the request returns straight away and never talks to a mail server.
3. `NotificationWorker` (Web/Infrastructure, started in `Global.asax`) wakes every 5 seconds and runs `NotificationProcessor.ProcessDue()`.
4. The processor takes the oldest due row, loads the package, builds the message with `INotificationComposer` and hands it to the `INotificationSender` for that channel.
5. The result is written back to the queue row:

| Result | Row after |
|---|---|
| Sent | `Status = Sent` |
| Failed, attempts left | stays `Pending`, `AttemptCount + 1`, `LastAttemptAtUtc` set; tried again after 60 s |
| Failed on the 3rd attempt, or a permanent failure | `Status = Failed` |
| No address, no sender for the channel, package missing | `Status = Failed` straight away |

Every attempt adds 1 to `AttemptCount` and sets `LastAttemptAtUtc`, including the one that succeeds.

## Which statuses send what

| New status | TemplateKey | Channels |
|---|---|---|
| Registered, In Storage | none | none |
| Ready for Collection | `ReadyForCollection` | Email, plus SMS when `Sms.Enabled` is true |
| Collected | `Collected` | Email, plus SMS when `Sms.Enabled` is true |

## Settings

Web.config appSettings, all optional (the defaults are used when a key is missing):

| Key | Default | Meaning |
|---|---|---|
| `Notifications.WorkerEnabled` | `true` | Set to `false` to stop the worker, e.g. on a second web server |
| `Notifications.PollSeconds` | `5` | How often the queue is checked |
| `Notifications.MaxAttempts` | `3` | Attempts in total before a row is marked Failed |
| `Notifications.RetryDelaySeconds` | `60` | Wait after a failed attempt |
| `Sms.Enabled` | `false` | Also queue an SMS (DECISIONS.md #6) |

Email subjects come from `dbo.AppConfig`: `Notification.ReadyForCollection.Subject` and `Notification.Collected.Subject`.

## Where the other tasks plug in

- **T26 SMTP sender:** write `SmtpNotificationSender : INotificationSender` with `Channel = NotificationChannels.Email`, reading the `Smtp.*` settings. Return `NotificationSendResult.Failed(error)` for things worth retrying (server down, timeout) and `Failed(error, permanent: true)` for things that won't change (invalid address). Then swap it in for the `TraceNotificationSender` in `CourierServices.NotificationSenders()`. Until then the stand-in writes one line per notification to Visual Studio's Output window.
- **T27 templates:** replace `PlainTextNotificationComposer` with a template-based `INotificationComposer` and swap it in `CourierServices.NotificationProcessor()`.
- **T28 notification log:** the queueing part of T28 is done here. What is left is writing a `dbo.NotificationLog` row for each attempt in `NotificationProcessor.ProcessNext` (it has the message, the result and the error) and showing failures to staff.
- **T29 SMS adapter:** an `INotificationSender` with `Channel = NotificationChannels.Sms`, registered in `CourierServices.NotificationSenders()` when `Sms.Enabled` is true.
- **T49 resend:** set a `Failed` row back to `Pending` (or enqueue a new row with `INotificationRepository.Enqueue`) and the worker picks it up on its next run.

## Rules

- Never log or put in an error message the recipient's name, email address or phone number (SR-03). The worker logs the queue id, template, channel and F20 identifier only. When a sender throws, only the exception type is recorded, because some mail exceptions repeat the server's reply, which can contain the address.
- Don't send anything from a status change listener. Listeners run inside the transaction; slow work there holds locks and a failure rolls back the staff member's change.

## Known limits

- One worker per web application. Two app instances against the same database could both pick up the same row. That is fine for the single-server deployment; claiming rows with `UPDLOCK, READPAST` would be needed for more.
- Delivery is at least once: if the database goes down between sending and marking the row Sent, the row is sent again on the next run.
- Collected times in the plain-text messages are shown in UTC. T27 decides how to show local time.
