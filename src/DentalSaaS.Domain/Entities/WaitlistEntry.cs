using DentalSaaS.Domain.Enums;

namespace DentalSaaS.Domain.Entities;

public class WaitlistEntry
{
    public int Id { get; set; }
    public int? PatientId { get; set; }
    public virtual Patient? Patient { get; set; }

    public string PatientName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty; // Celular para aviso inmediato por WhatsApp
    public string? Email { get; set; }

    public int? PreferredDentistId { get; set; }
    public virtual DentistProfile? PreferredDentist { get; set; }

    public int ServiceCatalogId { get; set; }
    public virtual ServiceCatalog ServiceCatalog { get; set; } = null!;

    public DateTime RequestedDate { get; set; }
    public PreferredTimeRange PreferredTimeRange { get; set; } = PreferredTimeRange.Anytime;

    public WaitlistStatus Status { get; set; } = WaitlistStatus.Active;

    public DateTime? NotifiedAt { get; set; }
    public DateTime? ClaimExpiresAt { get; set; }
    public Guid ClaimToken { get; set; } = Guid.NewGuid(); // Token para reservar de inmediato con un toque

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual ICollection<NotificationLog> NotificationLogs { get; set; } = new List<NotificationLog>();
}
