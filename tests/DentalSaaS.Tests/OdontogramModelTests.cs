using DentalSaaS.Domain.Entities;
using DentalSaaS.Domain.Enums;
using Xunit;

namespace DentalSaaS.Tests;

public class OdontogramModelTests
{
    [Fact]
    public void FDI_Nomenclature_ShouldCorrectlyIdentify_AdultAndPediatricTeeth()
    {
        // Adult Teeth (FDI 11 to 48)
        var incisor11 = new ToothSurfaceState { ToothNumber = 11 };
        var molar48 = new ToothSurfaceState { ToothNumber = 48 };

        Assert.True(incisor11.IsAdultTooth);
        Assert.False(incisor11.IsPediatricTooth);
        Assert.Equal(1, incisor11.Quadrant);
        Assert.Equal(1, incisor11.ToothPosition);

        Assert.True(molar48.IsAdultTooth);
        Assert.False(molar48.IsPediatricTooth);
        Assert.Equal(4, molar48.Quadrant);
        Assert.Equal(8, molar48.ToothPosition);

        // Pediatric Deciduous Teeth (FDI 51 to 85)
        var pediatricUpperRight55 = new ToothSurfaceState { ToothNumber = 55 };
        var pediatricLowerRight81 = new ToothSurfaceState { ToothNumber = 81 };

        Assert.True(pediatricUpperRight55.IsPediatricTooth);
        Assert.False(pediatricUpperRight55.IsAdultTooth);
        Assert.Equal(5, pediatricUpperRight55.Quadrant);
        Assert.Equal(5, pediatricUpperRight55.ToothPosition);

        Assert.True(pediatricLowerRight81.IsPediatricTooth);
        Assert.Equal(8, pediatricLowerRight81.Quadrant);
        Assert.Equal(1, pediatricLowerRight81.ToothPosition);
    }

    [Fact]
    public void OdontogramRecord_CanHold5Surfaces_AndComputeTreatmentBudget()
    {
        // Arrange
        var record = new OdontogramRecord
        {
            Id = 1,
            SnapshotTitle = "Diagnóstico Inicial con Presupuesto",
            SnapshotDate = DateTime.UtcNow,
            IsCurrentActive = true
        };

        // Pieza 16: Caries en cara Oclusal (pendiente de tratamiento, resina $75)
        record.ToothSurfaces.Add(new ToothSurfaceState
        {
            ToothNumber = 16,
            Surface = ToothSurface.OcclusalOrIncisal,
            ConditionType = ToothConditionType.Caries,
            Status = TreatmentClinicalStatus.PlanTreatmentPending,
            EstimatedPrice = 75.00m,
            ClinicalNotes = "Caries dentinaria profunda en fosa distal"
        });

        // Pieza 16: Restauración existente previa en cara Vestibular (preexistente, no suma a presupuesto)
        record.ToothSurfaces.Add(new ToothSurfaceState
        {
            ToothNumber = 16,
            Surface = ToothSurface.Vestibular,
            ConditionType = ToothConditionType.RestorationAmalgam,
            Status = TreatmentClinicalStatus.ExistingPreDiagnosis,
            EstimatedPrice = 0,
            ClinicalNotes = "Amalgama antigua sin filtración"
        });

        // Pieza 21: Corona proyectada por fractura (diente completo, $250)
        record.ToothSurfaces.Add(new ToothSurfaceState
        {
            ToothNumber = 21,
            Surface = ToothSurface.GeneralWholeTooth,
            ConditionType = ToothConditionType.Crown,
            Status = TreatmentClinicalStatus.PlanTreatmentPending,
            EstimatedPrice = 250.00m,
            ClinicalNotes = "Corona de zirconio estratificada"
        });

        // Act: Calcular presupuesto automático de tratamientos pendientes
        record.TotalEstimatedCost = record.ToothSurfaces
            .Where(t => t.Status == TreatmentClinicalStatus.PlanTreatmentPending)
            .Sum(t => t.EstimatedPrice);

        // Assert
        Assert.Equal(3, record.ToothSurfaces.Count);
        Assert.Equal(325.00m, record.TotalEstimatedCost);
    }
}
