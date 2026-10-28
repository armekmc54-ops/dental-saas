using DentalSaaS.Domain.Enums;

namespace DentalSaaS.Domain.Entities;

public class Patient
{
    public int Id { get; set; }

    // Folio Único de Expediente Médico: FOLIO-00001 a FOLIO-N
    public string FolioNumber { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;

    // CURP Oficial (18 caracteres) para expediente clínico
    public string? Curp { get; set; }
    public string? IdentificationNumber { get; set; }

    public string PhoneNumber { get; set; } = string.Empty; // Celular WhatsApp
    public string? Email { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? Gender { get; set; } // "M", "F", "Otro"
    public string? Address { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }
    public string? GeneralNotes { get; set; }

    // Datos de Facturación Fiscal (CFDI)
    public bool RequiresInvoice { get; set; } = false;
    public string? TaxId { get; set; } // RFC (12 o 13 caracteres)
    public string? LegalName { get; set; } // Razón Social
    public string? TaxRegime { get; set; } // Régimen Fiscal
    public string? TaxZipCode { get; set; } // Código Postal Fiscal
    public string? CfdiUsage { get; set; } = "D01"; // D01: Honorarios médicos, dentales y gastos hospitalarios

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public string FullName => $"{FirstName} {LastName}".Trim();

    public int? Age => DateOfBirth.HasValue
        ? DateTime.UtcNow.Year - DateOfBirth.Value.Year - (DateTime.UtcNow.DayOfYear < DateOfBirth.Value.DayOfYear ? 1 : 0)
        : null;

    // Relaciones
    public virtual ICollection<MedicalAlert> MedicalAlerts { get; set; } = new List<MedicalAlert>();
    public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
    public virtual ICollection<OdontogramRecord> OdontogramRecords { get; set; } = new List<OdontogramRecord>();
    public virtual ICollection<WaitlistEntry> WaitlistEntries { get; set; } = new List<WaitlistEntry>();
    public virtual ICollection<PatientDocument> Documents { get; set; } = new List<PatientDocument>();
    public virtual ICollection<Prescription> Prescriptions { get; set; } = new List<Prescription>();
}

public class MedicalAlert
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public virtual Patient Patient { get; set; } = null!;

    public MedicalAlertType AlertType { get; set; }
    public string Description { get; set; } = string.Empty; // Ej: "Alergia Severa a Penicilina", "Hipertensión Arterial"
    public MedicalAlertSeverity Severity { get; set; } = MedicalAlertSeverity.Medium;
    public bool IsPermanent { get; set; } = true;
    public DateTime ReportedAt { get; set; } = DateTime.UtcNow;
    public string? ActionProtocol { get; set; } // Protocolo clínico ej: "Prohibido amoxicilina; usar eritromicina o clindamicina"
}
