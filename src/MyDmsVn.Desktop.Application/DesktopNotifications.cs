using System;

namespace MyDmsVn.Desktop.Application
{
    public enum DesktopNotificationKind
    {
        Information,
        Success,
        Warning,
        Error,
    }

    public sealed class DesktopNotification
    {
        public DesktopNotification(DesktopNotificationKind kind, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentException("A notification message is required.", nameof(message));
            }

            Kind = kind;
            Message = message;
        }

        public DesktopNotificationKind Kind { get; }

        public string Message { get; }
    }

    public interface IDesktopNotificationService
    {
        event EventHandler<DesktopNotification>? NotificationPublished;

        DesktopNotification? LastNotification { get; }

        void Publish(DesktopNotification notification);
    }

    public sealed class DesktopNotificationCenter : IDesktopNotificationService
    {
        public event EventHandler<DesktopNotification>? NotificationPublished;

        public DesktopNotification? LastNotification { get; private set; }

        public void Publish(DesktopNotification notification)
        {
            LastNotification = notification ?? throw new ArgumentNullException(nameof(notification));
            NotificationPublished?.Invoke(this, notification);
        }
    }
}
