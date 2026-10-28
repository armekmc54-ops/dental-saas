using System.ComponentModel.DataAnnotations;
using DentalSaaS.Domain.Enums;

namespace DentalSaaS.Domain.Entities;

public class Appointment
{
    public int Id { get; set; }

    [MaxLength(32)]
    public string AppointmentCode { get; set; } = string.Empty; // Ej: "DEN-8491-X"

    public int PatientId { get; set; }
    public virtual Patient Patient { get; set; } = null!;

    public int DentistId { get; set; }
    public virtual DentistProfile Dentist { get; set; } = null!;

    public int DentalChairId { get; set; }
    public virtual DentalChair DentalChair { get; set; } = null!;

    public int ServiceCatalogId { get; set; }
    public virtual ServiceCatalog ServiceCatalog { get; set; } = null!;

    public DateTime StartDateTime { get; set; }
    public DateTime EndDateTime { get; set; }

    public AppointmentStatus Status { get; set; } = AppointmentStatus.Scheduled;

    public string? CancellationReason { get; set; }
    public DateTime? CancelledAt { get; set; }

    // Magic Link de Autogestión para el paciente (sin requerir login)
    public Guid MagicToken { get; set; } = Guid.NewGuid();
    public DateTime MagicTokenExpiresAt { get; set; }

    // Flags rápidos del wizard público
    public bool IsEmergency { get; set; } = false;
    public int PainLevel { get; set; } = 0; // Escala EVA del 0 al 10
    public string? PatientBookingNotes { get; set; } // Notas que deja el paciente al reservar
    public string? QuickAllergyAlertNote { get; set; } // Alergias declaradas en el paso 4

    // Notas de evolución clínica (exclusivo odontólogo)
    public string? ClinicalEvolutionNotes { get; set; }

    // Timestamps de control de flujo en recepción
    public DateTime? WaitingRoomCheckInAt { get; set; }
    public DateTime? InChairCheckInAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    // Reglas de negocio de confirmación y cancelación
    public DateTime ConfirmationDeadline => StartDateTime.AddMinutes(-15);
    public DateTime CancellationDeadline => StartDateTime.AddDays(-1);
    public bool IsConfirmationExpired => DateTime.UtcNow > ConfirmationDeadline && Status == AppointmentStatus.Scheduled;
    public bool CanPatientCancelDirectly => DateTime.UtcNow <= CancellationDeadline;
    public bool NeedsReschedule { get; set; } = false;

    // Concurrency Token (RowVersion) para evitar condiciones de carrera al agendar
    [Timestamp]
    public byte[]? RowVersion { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Relaciones
    public virtual ICollection<PaymentRecord> PaymentRecords { get; set; } = new List<PaymentRecord>();
    public virtual ICollection<NotificationLog> NotificationLogs { get; set; } = new List<NotificationLog>();
}
