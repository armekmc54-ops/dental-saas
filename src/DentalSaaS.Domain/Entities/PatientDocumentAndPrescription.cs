namespace DentalSaaS.Domain.Entities;

public enum PatientDocumentType
{
    RadiografiaPanoramica = 1,
    RadiografiaPeriapical = 2,
    HistorialPapelEscaneado = 3, // Migración de expediente físico a digital
    ConsentimientoInformado = 4,
    EstudioLaboratorio = 5,
    FotografiaIntraoral = 6,
    Otro = 7
}

public class PatientDocument
{
    public int Id { get; set; }

    public int PatientId { get; set; }
    public virtual Patient Patient { get; set; } = null!;

    public string Title { get; set; } = string.Empty; // Ej: "Radiografía Panorámica Inicial", "Expediente Antiguo 2018-2024"
    public PatientDocumentType DocumentType { get; set; } = PatientDocumentType.HistorialPapelEscaneado;

    public string OriginalFileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/pdf";
    public long FileSizeBytes { get; set; }

    public string? Notes { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}

public class Prescription
{
    public int Id { get; set; }
    public string PrescriptionFolio { get; set; } = string.Empty; // Ej: "REC-00042"

    public int PatientId { get; set; }
    public virtual Patient Patient { get; set; } = null!;

    public int DentistId { get; set; }
    public virtual DentistProfile Dentist { get; set; } = null!;

    public int? AppointmentId { get; set; }
    public virtual Appointment? Appointment { get; set; }

    public string Diagnosis { get; set; } = string.Empty;
    public string? GeneralInstructions { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual ICollection<PrescriptionItem> Items { get; set; } = new List<PrescriptionItem>();
}

public class PrescriptionItem
{
    public int Id { get; set; }
    public int PrescriptionId { get; set; }
    public virtual Prescription Prescription { get; set; } = null!;

    public string MedicineName { get; set; } = string.Empty; // Ej: "Ibuprofeno"
    public string Dosage { get; set; } = string.Empty;       // Ej: "600 mg"
    public int FrequencyHours { get; set; } = 8;             // Ej: Cada 8 horas
    public int DurationDays { get; set; } = 5;               // Ej: Durante 5 días
    public string Instructions { get; set; } = string.Empty; // Ej: "Tomar con alimentos. No tomar en ayunas."
    public DateTime StartDate { get; set; } = DateTime.UtcNow;

    public string ReminderScheduleDescription => $"Cada {FrequencyHours} horas durante {DurationDays} días";
}
