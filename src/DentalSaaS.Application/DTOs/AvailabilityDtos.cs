using DentalSaaS.Domain.Enums;

namespace DentalSaaS.Application.DTOs;

public class SlotSearchRequest
{
    public DateTime Date { get; set; }
    public int ServiceId { get; set; }
    public int? PreferredDentistId { get; set; } // Null = "Cualquier doctor disponible (Asignación inteligente)"
    public int? PreferredDentalChairId { get; set; }
    public int SlotIntervalMinutes { get; set; } = 30; // Granularidad del grid (ej. 15 o 30 min)
}

public class TimeSlotDto
{
    public DateTime StartDateTime { get; set; }
    public DateTime EndDateTime { get; set; }
    public int ServiceDurationMinutes { get; set; }
    public int BufferMinutes { get; set; }
    public int DentistId { get; set; }
    public string DentistName { get; set; } = string.Empty;
    public string DentistSpecialty { get; set; } = string.Empty;
    public string DentistColorHex { get; set; } = string.Empty;
    public int DentalChairId { get; set; }
    public string DentalChairName { get; set; } = string.Empty;
    public PreferredTimeRange Period => StartDateTime.Hour < 13 ? PreferredTimeRange.Morning : PreferredTimeRange.Afternoon;
    public string FormattedTime => StartDateTime.ToString("hh:mm tt");
    public string FormattedEndTime => EndDateTime.ToString("hh:mm tt");
}

public class DailyAvailabilityResultDto
{
    public DateTime Date { get; set; }
    public bool IsDayFull => MorningSlots.Count == 0 && AfternoonSlots.Count == 0;
    public bool CanJoinWaitlist => IsDayFull;
    public List<TimeSlotDto> MorningSlots { get; set; } = new();
    public List<TimeSlotDto> AfternoonSlots { get; set; } = new();
    public int TotalAvailableSlots => MorningSlots.Count + AfternoonSlots.Count;
}

public class AppointmentBookingDto
{
    // Datos del servicio y horario
    public int ServiceId { get; set; }
    public int? PreferredDentistId { get; set; } // Opcional, si viene null el sistema asigna el doctor disponible
    public DateTime StartDateTime { get; set; }

    // Datos del paciente (Wizard público sin login)
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty; // WhatsApp
    public string? Email { get; set; }
    public string? IdentificationNumber { get; set; }
    public string? Curp { get; set; } // CURP 18 caracteres
    public DateTime? DateOfBirth { get; set; }

    // Datos Fiscales opcionales para Facturación (CFDI)
    public bool RequiresInvoice { get; set; } = false;
    public string? TaxId { get; set; } // RFC
    public string? LegalName { get; set; } // Razón Social
    public string? TaxRegime { get; set; } // Régimen Fiscal
    public string? TaxZipCode { get; set; } // CP Fiscal
    public string? CfdiUsage { get; set; } = "D01";

    // Alerta médica rápida & motivo
    public bool HasMedicalAlertOrAllergy { get; set; }
    public string? MedicalAlertDescription { get; set; } // Ej: "Alergia a la penicilina", "Hipertensión"
    public bool HasCurrentPain { get; set; }
    public int PainLevel { get; set; } = 0; // 0 a 10
    public string? BookingNotes { get; set; }
}

public class AppointmentBookingResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int? AppointmentId { get; set; }
    public string? AppointmentCode { get; set; }
    public string? FolioNumber { get; set; }
    public Guid? MagicToken { get; set; }
    public string? MagicManageUrl { get; set; }
    public DateTime? StartDateTime { get; set; }
    public DateTime? EndDateTime { get; set; }
    public string? DentistName { get; set; }
    public string? DentalChairName { get; set; }
    public string? ServiceName { get; set; }
    public decimal ServicePrice { get; set; }
}

public class WaitlistJoinDto
{
    public string PatientName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Email { get; set; }
    public int ServiceId { get; set; }
    public int? PreferredDentistId { get; set; }
    public DateTime RequestedDate { get; set; }
    public PreferredTimeRange PreferredTimeRange { get; set; } = PreferredTimeRange.Anytime;
}

public class BlockScheduleRequestDto
{
    public int DentistId { get; set; }
    public DateTime StartDateTime { get; set; }
    public DateTime EndDateTime { get; set; }
    public string Reason { get; set; } = "Bloqueo Clínico / Procedimiento";
    public bool IsFullDay { get; set; } = false;
}

public class ScheduleExceptionDto
{
    public int Id { get; set; }
    public int DentistId { get; set; }
    public string DentistName { get; set; } = string.Empty;
    public DateTime StartDateTime { get; set; }
    public DateTime EndDateTime { get; set; }
    public string Reason { get; set; } = string.Empty;
    public bool IsFullDay { get; set; }
    public string FormattedTimeRange => $"{StartDateTime:hh:mm tt} - {EndDateTime:hh:mm tt}";
}

public class ScheduleExceptionResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? ExceptionId { get; set; }
    public ScheduleExceptionDto? Exception { get; set; }
}
