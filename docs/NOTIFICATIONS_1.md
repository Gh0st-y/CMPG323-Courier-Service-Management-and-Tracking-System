# Notifications (T25 queue and worker, T26 email, T28 log)

How a status change turns into an email or SMS, and where T27 and T29 plug in.

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
| `Smtp.Host` | `localhost` in Web.config | Mail server. Leave it empty to use the Output-window stand-in instead of sending |
| `Smtp.Port` | `25` | |
| `Smtp.FromAddress` | `courier-noreply@f20.local` in Web.config | Sender address |
| `Smtp.FromName` | `F20 Courier Service` | Sender name |
| `Smtp.UseSsl` | `false` | |
| `Smtp.UserName`, `Smtp.Password` | none | Only if the server needs a login. Never commit a password: put it in Web.config.Local |
| `Smtp.TimeoutSeconds` | `10` | How long to wait for the mail server before counting a failed attempt |

Email subjects come from `dbo.AppConfig`: `Notification.ReadyForCollection.Subject` and `Notification.Collected.Subject`.

## Where the other tasks plug in

- **T26 SMTP sender (done):** `SmtpNotificationSender` sends through `System.Net.Mail.SmtpClient` using the `Smtp.*` settings. An invalid address or a 5xx answer about the recipient is a permanent failure; a server that is down, busy, slow or not set up is retried. With `Smtp.Host` empty, `CourierServices.NotificationSenders()` uses the Output-window stand-in instead.
- **T27 templates:** replace `PlainTextNotificationComposer` with a template-based `INotificationComposer` and swap it in `CourierServices.NotificationProcessor()`.
- **T28 notification log (done):** every attempt, sent or failed, adds a `dbo.NotificationLog` row (see "Notification log" below). The package detail page lists them with the reason for any failure.
- **T29 SMS adapter:** an `INotificationSender` with `Channel = NotificationChannels.Sms`, registered in `CourierServices.NotificationSenders()` when `Sms.Enabled` is true.
- **T49 resend (done):** `POST /api/packages/{f20Identifier}/notifications/resend` (Supervisor, SystemAdmin) puts the package's latest notification on a channel back to `Pending` with no attempts, if it failed, and writes a `NotificationResent` audit entry. The worker sends it on its next run. The Resend button on the detail page is FE2's follow-up; see docs/API_CONTRACT.md for the responses.

## Notification log (T28, IR-003, DR-012)

Every attempt the worker makes writes one row to `dbo.NotificationLog`, after the queue row is updated:

| Column | Value |
|---|---|
| PackageId | The package, so the detail page can list its notifications (DR-012) |
| Channel | Email or SMS |
| RecipientAddress | The address it went to, or empty if the recipient had none. Masked on screen |
| Subject | The subject line |
| Status | Sent or Failed |
| ErrorDetail | Why it failed, e.g. "The recipient has no email address." or "The mail server could not be used (GeneralFailure, ConnectionRefused). Will try again (attempt 1 of 3)." Empty when sent |
| SentAtUtc | When the attempt was made (set by the database) |

So a notification that fails twice and then goes through has three rows: Failed, Failed, Sent. The queue row only keeps the latest state.

- Text longer than its column is cut to fit, so a long error can't make the insert fail.
- If the log row can't be written, the worker only traces a warning. The attempt is already recorded on the queue row, and failing there would mean sending the email again.
- A queue row whose package no longer exists isn't logged, because the log row needs a package.
- `GET /api/packages/{f20Identifier}/detail` returns them newest first as `notifications: [{channel, recipientAddress (masked), subject, status, errorDetail, sentAtUtc}]`.

## Email during development

Nothing is sent to real people in development. Run a local test mail server that catches every email and shows it in a web page. Web.config already points at it (`Smtp.Host` localhost, port 25).

- **smtp4dev** (needs the .NET SDK, which Visual Studio installs): run `dotnet tool install -g Rnwood.Smtp4dev` once, then `smtp4dev` whenever you need it. Emails appear at http://localhost:5000.
- **Papercut SMTP**: install it from its GitHub releases page and start it. Emails appear in its window.

If no test mail server is running, nothing breaks: each email is tried 3 times, 60 s apart, and then marked Failed in `dbo.NotificationQueue`. Status changes are never affected.

## Rules

- Never log or put in an error message the recipient's name, email address or phone number (SR-03). The worker logs the queue id, template, channel and F20 identifier only. When a sender throws, only the exception type is recorded, because some mail exceptions repeat the server's reply, which can contain the address.
- Don't send anything from a status change listener. Listeners run inside the transaction; slow work there holds locks and a failure rolls back the staff member's change.

## Known limits

- One worker per web application. Two app instances against the same database could both pick up the same row. That is fine for the single-server deployment; claiming rows with `UPDLOCK, READPAST` would be needed for more.
- Delivery is at least once: if the database goes down between sending and marking the row Sent, the row is sent again on the next run.
- Collected times in the plain-text messages are shown in UTC. T27 decides how to show local time.