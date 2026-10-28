using DentalSaaS.Domain.Enums;

namespace DentalSaaS.Domain.Entities;

public class PaymentRecord
{
    public int Id { get; set; }
    public int AppointmentId { get; set; }
    public virtual Appointment Appointment { get; set; } = null!;

    public decimal Amount { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Paid;

    public string? TransactionReference { get; set; }
    public string? InvoiceNumber { get; set; }
    public DateTime PaidAt { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }
}

public class NotificationLog
{
    public int Id { get; set; }

    public int? AppointmentId { get; set; }
    public virtual Appointment? Appointment { get; set; }

    public int? WaitlistEntryId { get; set; }
    public virtual WaitlistEntry? WaitlistEntry { get; set; }

    public string RecipientName { get; set; } = string.Empty;
    public string RecipientContact { get; set; } = string.Empty; // Teléfono o Email

    public NotificationChannel Channel { get; set; } = NotificationChannel.WhatsApp;
    public NotificationType NotificationType { get; set; }
    public NotificationStatus Status { get; set; } = NotificationStatus.Queued;

    public string? MessageBody { get; set; }
    public string? ExternalMessageId { get; set; } // ID retornado por Twilio, Meta WhatsApp Cloud API o SMTP
    public string? ErrorDetails { get; set; }
    public int RetryCount { get; set; } = 0;

    public DateTime ScheduledFor { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
}
