using DentalSaaS.Domain.Enums;

namespace DentalSaaS.Application.DTOs;

public class ToothSurfaceDto
{
    public int ToothNumber { get; set; }
    public ToothSurface Surface { get; set; }
    public ToothConditionType ConditionType { get; set; }
    public TreatmentClinicalStatus Status { get; set; }
    public int? ServiceCatalogId { get; set; }
    public decimal EstimatedPrice { get; set; }
    public string? ClinicalNotes { get; set; }
}

public class OdontogramSnapshotDto
{
    public int? Id { get; set; }
    public int PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public int? PatientAge { get; set; }
    public List<MedicalAlertSummaryDto> MedicalAlerts { get; set; } = new();
    public string? QuickAllergyNote { get; set; }
    public int PainLevel { get; set; }
    public string SnapshotTitle { get; set; } = "Consulta Actual";
    public DateTime SnapshotDate { get; set; } = DateTime.UtcNow;
    public string? ClinicalNotes { get; set; }
    public decimal TotalEstimatedCost { get; set; }
    public List<ToothSurfaceDto> ToothSurfaces { get; set; } = new();
}

public class MedicalAlertSummaryDto
{
    public int Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public MedicalAlertType AlertType { get; set; }
    public MedicalAlertSeverity Severity { get; set; }
    public string? ActionProtocol { get; set; }
}

public class SaveOdontogramRequest
{
    public int PatientId { get; set; }
    public int DentistId { get; set; }
    public string SnapshotTitle { get; set; } = "Evolución Clínica";
    public string? ClinicalNotes { get; set; }
    public List<ToothSurfaceDto> ToothSurfaces { get; set; } = new();
}
