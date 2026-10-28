using DentalSaaS.Domain.Enums;

namespace DentalSaaS.Domain.Entities;

public class OdontogramRecord
{
    public int Id { get; set; }

    public int PatientId { get; set; }
    public virtual Patient Patient { get; set; } = null!;

    public int DentistId { get; set; }
    public virtual DentistProfile Dentist { get; set; } = null!;

    public DateTime SnapshotDate { get; set; } = DateTime.UtcNow;
    public string SnapshotTitle { get; set; } = "Estado Inicial al Ingreso";
    public string? ClinicalNotes { get; set; }
    public bool IsCurrentActive { get; set; } = true;

    // Presupuesto automático calculado a partir de las piezas con tratamiento pendiente
    public decimal TotalEstimatedCost { get; set; } = 0;

    public virtual ICollection<ToothSurfaceState> ToothSurfaces { get; set; } = new List<ToothSurfaceState>();
}

public class ToothSurfaceState
{
    public int Id { get; set; }

    public int OdontogramRecordId { get; set; }
    public virtual OdontogramRecord OdontogramRecord { get; set; } = null!;

    // Nomenclatura FDI: 11 al 48 (Adulto) o 51 al 85 (Infantil / Deciduo)
    public int ToothNumber { get; set; }

    // Cara dental afectada o pieza completa / raíz
    public ToothSurface Surface { get; set; }

    // Hallazgo o tratamiento (Caries, Resina, Amalgama, Ausente, Corona, Endodoncia, Extracción, etc.)
    public ToothConditionType ConditionType { get; set; }

    // Estado clínico: Preexistente, Plan de tratamiento pendiente, o Realizado en clínica
    public TreatmentClinicalStatus Status { get; set; } = TreatmentClinicalStatus.ExistingPreDiagnosis;

    // Enlace opcional al catálogo para presupuestación automática instantánea
    public int? ServiceCatalogId { get; set; }
    public virtual ServiceCatalog? ServiceCatalog { get; set; }

    public decimal EstimatedPrice { get; set; } = 0;
    public string? ClinicalNotes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Métodos utilitarios FDI
    public bool IsPediatricTooth => ToothNumber >= 51 && ToothNumber <= 85;
    public bool IsAdultTooth => ToothNumber >= 11 && ToothNumber <= 48;

    public int Quadrant => ToothNumber / 10;
    public int ToothPosition => ToothNumber % 10;
}
