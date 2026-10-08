using System;
using System.Collections.Generic;
using System.Linq;
using CourierService.Domain;
using CourierService.Domain.Entities;
using CourierService.Domain.Models;
using CourierService.Domain.Repositories;
using CourierService.Services.Notifications;

namespace CourierService.Tests.Notifications
{
    /// <summary>An in-memory dbo.NotificationQueue that behaves like the real one for the worker's purposes.</summary>
    internal sealed class FakeNotificationQueue : INotificationRepository
    {
        private int _nextId = 1;

        public FakeNotificationQueue(Func<DateTime> utcNow = null)
        {
            UtcNow = utcNow ?? (() => DateTime.UtcNow);
        }

        public Func<DateTime> UtcNow { get; set; }

        public List<NotificationQueueItem> Items { get; } = new List<NotificationQueueItem>();

        public List<IUnitOfWork> UnitsOfWorkUsed { get; } = new List<IUnitOfWork>();

        public DateTime? LastRetryBeforeUtc { get; private set; }

        public NotificationQueueItem Add(int packageId, string channel = NotificationChannels.Email,
            string templateKey = NotificationTemplateKeys.ReadyForCollection, int attemptCount = 0, DateTime? lastAttemptAtUtc = null)
        {
            var item = new NotificationQueueItem
            {
                NotificationQueueId = _nextId++,
                PackageId = packageId,
                Channel = channel,
                TemplateKey = templateKey,
                Status = "Pending",
                AttemptCount = attemptCount,
                EnqueuedAtUtc = UtcNow(),
                LastAttemptAtUtc = lastAttemptAtUtc
            };
            Items.Add(item);
            return item;
        }

        public void Enqueue(NotificationQueueItem item, IUnitOfWork unitOfWork = null)
        {
            UnitsOfWorkUsed.Add(unitOfWork);
            Add(item.PackageId, item.Channel, item.TemplateKey);
        }

        public NotificationQueueItem GetNextPending()
        {
            return Items.FirstOrDefault(i => i.Status == "Pending");
        }

        public NotificationQueueItem GetNextDue(DateTime retryBeforeUtc, IUnitOfWork unitOfWork = null)
        {
            LastRetryBeforeUtc = retryBeforeUtc;
            return Items
                .Where(i => i.Status == "Pending" && (i.LastAttemptAtUtc == null || i.LastAttemptAtUtc <= retryBeforeUtc))
                .OrderBy(i => i.EnqueuedAtUtc).ThenBy(i => i.NotificationQueueId)
                .Select(Copy)
                .FirstOrDefault();
        }

        public void MarkSent(int notificationQueueId, IUnitOfWork unitOfWork = null) => Update(notificationQueueId, "Sent");

        public void MarkFailed(int notificationQueueId, IUnitOfWork unitOfWork = null) => Update(notificationQueueId, "Failed");

        public void RecordFailedAttempt(int notificationQueueId, IUnitOfWork unitOfWork = null) => Update(notificationQueueId, "Pending");

        public NotificationQueueItem Get(int id) => Items.Single(i => i.NotificationQueueId == id);

        public NotificationQueueItem GetLatestForPackage(int packageId, string channel, IUnitOfWork unitOfWork = null)
        {
            UnitsOfWorkUsed.Add(unitOfWork);
            return Items
                .Where(i => i.PackageId == packageId && i.Channel == channel)
                .OrderByDescending(i => i.NotificationQueueId)
                .Select(Copy)
                .FirstOrDefault();
        }

        public bool Requeue(int notificationQueueId, IUnitOfWork unitOfWork = null)
        {
            var item = Get(notificationQueueId);
            if (item.Status != "Failed")
            {
                return false;
            }

            item.Status = "Pending";
            item.AttemptCount = 0;
            item.LastAttemptAtUtc = null;
            return true;
        }

        private void Update(int id, string status)
        {
            var item = Get(id);
            item.Status = status;
            item.AttemptCount++;
            item.LastAttemptAtUtc = UtcNow();
        }

        // The worker gets a copy, as it would from the database, so it can't change the stored row by accident
        private static NotificationQueueItem Copy(NotificationQueueItem i) => new NotificationQueueItem
        {
            NotificationQueueId = i.NotificationQueueId,
            PackageId = i.PackageId,
            Channel = i.Channel,
            TemplateKey = i.TemplateKey,
            Status = i.Status,
            AttemptCount = i.AttemptCount,
            EnqueuedAtUtc = i.EnqueuedAtUtc,
            LastAttemptAtUtc = i.LastAttemptAtUtc
        };
    }

    /// <summary>Only GetById is used by the worker.</summary>
    internal sealed class FakePackages : IPackageRepository
    {
        public Dictionary<int, Package> ById { get; } = new Dictionary<int, Package>();

        public Package Add(int packageId, string f20Identifier, string email = "recipient@courier.test", string phone = "0820000000",
            string fullName = "Test Recipient", string storageLocationCode = "Shelf A-3", DateTime? collectedAtUtc = null)
        {
            var package = new Package
            {
                PackageId = packageId,
                F20Identifier = f20Identifier,
                StorageLocationCode = storageLocationCode,
                CollectedAtUtc = collectedAtUtc,
                Recipient = new Recipient { FullName = fullName, Email = email, PhoneNumber = phone }
            };
            ById[packageId] = package;
            return package;
        }

        public Package GetById(int packageId)
        {
            Package package;
            return ById.TryGetValue(packageId, out package) ? package : null;
        }

        public Package GetByF20Identifier(string f20Identifier) => ById.Values.FirstOrDefault(p => p.F20Identifier == f20Identifier);
        public int Insert(Package package, IUnitOfWork unitOfWork = null) => throw new NotSupportedException();
        public void UpdateStatus(int packageId, PackageStatus newStatus, int? storageLocationId, IUnitOfWork unitOfWork = null) => throw new NotSupportedException();
        public bool TryUpdateStatus(int packageId, PackageStatus newStatus, int? storageLocationId, int? collectedByUserId, byte[] expectedRowVersion, IUnitOfWork unitOfWork = null) => throw new NotSupportedException();
        public PagedResult<Package> Search(PackageSearchCriteria criteria) => throw new NotSupportedException();
    }

    internal sealed class FakeAppConfig : IAppConfigRepository
    {
        public Dictionary<string, string> Values { get; } = new Dictionary<string, string>();

        public string GetValue(string key)
        {
            string value;
            return Values.TryGetValue(key, out value) ? value : null;
        }
    }

    /// <summary>Records what it was asked to send. Answers with the queued results in order, then with Sent.</summary>
    internal sealed class FakeSender : INotificationSender
    {
        public FakeSender(string channel = NotificationChannels.Email)
        {
            Channel = channel;
        }

        public string Channel { get; }

        public List<NotificationMessage> Sent { get; } = new List<NotificationMessage>();

        public Queue<Func<NotificationSendResult>> Results { get; } = new Queue<Func<NotificationSendResult>>();

        public NotificationSendResult Send(NotificationMessage message)
        {
            Sent.Add(message);
            return Results.Count > 0 ? Results.Dequeue()() : NotificationSendResult.Sent();
        }
    }

    /// <summary>An in-memory dbo.NotificationLog (T28). Set Throw to make every write fail.</summary>
    internal sealed class FakeNotificationLog : INotificationLogRepository
    {
        public List<NotificationLogEntry> Entries { get; } = new List<NotificationLogEntry>();

        public Exception Throw { get; set; }

        public void Add(NotificationLogEntry entry, IUnitOfWork unitOfWork = null)
        {
            if (Throw != null)
            {
                throw Throw;
            }

            Entries.Add(entry);
        }
    }

    internal sealed class FakeUnitOfWork : IUnitOfWork
    {
        public System.Data.IDbConnection Connection => null;
        public System.Data.IDbTransaction Transaction => null;
        public void Commit() { }
        public void Dispose() { }
    }
}