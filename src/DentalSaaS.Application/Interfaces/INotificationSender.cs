using DentalSaaS.Domain.Enums;

namespace DentalSaaS.Application.Interfaces;

public class NotificationMessage
{
    public string RecipientName { get; set; } = string.Empty;
    public string RecipientContact { get; set; } = string.Empty; // Teléfono E.164 o Email
    public NotificationChannel Channel { get; set; } = NotificationChannel.WhatsApp;
    public NotificationType Type { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string BodyText { get; set; } = string.Empty;
    public string? ActionUrl { get; set; }
    public string? ActionButtonText { get; set; }
    public Dictionary<string, string> TemplateParameters { get; set; } = new();
}

public class NotificationSendResult
{
    public bool Success { get; set; }
    public string? ExternalMessageId { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}

public interface INotificationSender
{
    bool SupportsChannel(NotificationChannel channel);
    Task<NotificationSendResult> SendAsync(NotificationMessage message, CancellationToken ct = default);
}
