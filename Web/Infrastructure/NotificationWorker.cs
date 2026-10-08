using System;
using System.Configuration;
using System.Diagnostics;
using System.Threading;
using System.Web.Hosting;
using CourierService.Data;
using CourierService.Services.Notifications;

namespace CourierService.Web.Infrastructure
{
    /// <summary>
    /// Background worker for the notification queue (T25). Started once from Application_Start, it checks
    /// dbo.NotificationQueue every few seconds (5 by default) and sends whatever is due through NotificationProcessor.
    ///
    /// It runs on a timer thread, never inside a staff request, so the request that changed a status has already
    /// returned by the time anything is sent. It registers with ASP.NET (IRegisteredObject) so that when the app shuts
    /// down or recycles, the current batch can finish before the app goes away.
    ///
    /// Optional appSettings (the defaults are used when a key is missing):
    ///   Notifications.WorkerEnabled     true
    ///   Notifications.PollSeconds       5
    ///   Notifications.MaxAttempts       3
    ///   Notifications.RetryDelaySeconds 60
    /// </summary>
    public sealed class NotificationWorker : IRegisteredObject
    {
        private static readonly object StartLock = new object();
        private static NotificationWorker _instance;

        // When the database is down every run fails; log the first error and then only every 12th (once a minute at 5 s)
        private const int LogEveryNthRepeatedError = 12;

        private readonly Func<NotificationProcessor> _createProcessor;
        private readonly Timer _timer;
        private int _running;
        private int _unregistered;
        private int _consecutiveErrors;
        private volatile bool _stopping;

        private NotificationWorker(Func<NotificationProcessor> createProcessor, TimeSpan pollInterval)
        {
            _createProcessor = createProcessor;
            _timer = new Timer(Tick, null, pollInterval, pollInterval);
        }

        /// <summary>Starts the worker. Safe to call more than once; only the first call starts it.</summary>
        public static void Start()
        {
            if (!Setting("Notifications.WorkerEnabled", true))
            {
                Trace.TraceInformation("Notification worker is switched off (Notifications.WorkerEnabled).");
                return;
            }

            lock (StartLock)
            {
                if (_instance != null)
                {
                    return;
                }

                var pollSeconds = Math.Max(1, Setting("Notifications.PollSeconds", 5));
                var maxAttempts = Math.Max(1, Setting("Notifications.MaxAttempts", NotificationProcessor.DefaultMaxAttempts));
                var retryDelay = TimeSpan.FromSeconds(Math.Max(0, Setting("Notifications.RetryDelaySeconds", 60)));

                _instance = new NotificationWorker(
                    () => CourierServices.NotificationProcessor(new SqlConnectionFactory(), maxAttempts, retryDelay),
                    TimeSpan.FromSeconds(pollSeconds));

                HostingEnvironment.RegisterObject(_instance);
                Trace.TraceInformation("Notification worker started: every {0} s, up to {1} attempts, {2} s between attempts.",
                    pollSeconds, maxAttempts, (int)retryDelay.TotalSeconds);
            }
        }

        /// <summary>Called by ASP.NET on shutdown: first with immediate = false, then with true if we haven't finished.</summary>
        public void Stop(bool immediate)
        {
            _stopping = true;
            _timer.Change(Timeout.Infinite, Timeout.Infinite);

            // A batch is still running and we're allowed to wait: Tick unregisters when it finishes
            if (!immediate && Volatile.Read(ref _running) == 1)
            {
                return;
            }

            Unregister();
        }

        private void Tick(object state)
        {
            if (_stopping)
            {
                return;
            }

            // Skip this tick if the previous run is still busy (a slow SMTP server, say)
            if (Interlocked.CompareExchange(ref _running, 1, 0) != 0)
            {
                return;
            }

            try
            {
                var processed = _createProcessor().ProcessDue();
                if (processed > 0)
                {
                    Trace.TraceInformation("Notification worker processed {0} item(s).", processed);
                }

                _consecutiveErrors = 0;
            }
            catch (Exception ex)
            {
                // Never let an exception escape a timer thread: it would take the whole app down
                _consecutiveErrors++;
                if (_consecutiveErrors == 1 || _consecutiveErrors % LogEveryNthRepeatedError == 0)
                {
                    Trace.TraceError("Notification worker run failed ({0} in a row), will try again: {1}: {2}",
                        _consecutiveErrors, ex.GetType().Name, ex.Message);
                }
            }
            finally
            {
                Interlocked.Exchange(ref _running, 0);
                if (_stopping)
                {
                    Unregister();
                }
            }
        }

        private void Unregister()
        {
            if (Interlocked.Exchange(ref _unregistered, 1) == 1)
            {
                return;
            }

            _timer.Dispose();
            HostingEnvironment.UnregisterObject(this);
        }

        private static int Setting(string key, int fallback)
        {
            int value;
            return int.TryParse(ConfigurationManager.AppSettings[key], out value) ? value : fallback;
        }

        private static bool Setting(string key, bool fallback)
        {
            bool value;
            return bool.TryParse(ConfigurationManager.AppSettings[key], out value) ? value : fallback;
        }
    }
}