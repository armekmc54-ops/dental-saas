using Microsoft.EntityFrameworkCore;
using DentalSaaS.Application.DTOs;
using DentalSaaS.Application.Interfaces;
using DentalSaaS.Domain.Entities;
using DentalSaaS.Domain.Enums;

namespace DentalSaaS.Application.Services;

public interface IOdontogramService
{
    Task<OdontogramSnapshotDto> GetActiveOdontogramAsync(int patientId, CancellationToken ct = default);
    Task<OdontogramSnapshotDto> SaveSnapshotAsync(SaveOdontogramRequest request, CancellationToken ct = default);
    Task<List<OdontogramSnapshotSummaryDto>> GetHistoryAsync(int patientId, CancellationToken ct = default);
}

public class OdontogramSnapshotSummaryDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public decimal TotalBudget { get; set; }
    public int AffectedTeethCount { get; set; }
}

public class OdontogramService : IOdontogramService
{
    private readonly IApplicationDbContext _context;

    public OdontogramService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<OdontogramSnapshotDto> GetActiveOdontogramAsync(int patientId, CancellationToken ct = default)
    {
        var patient = await _context.Patients
            .AsNoTracking()
            .Include(p => p.MedicalAlerts)
            .Include(p => p.Appointments)
            .FirstOrDefaultAsync(p => p.Id == patientId, ct);

        if (patient == null)
        {
            throw new ArgumentException("Paciente no encontrado", nameof(patientId));
        }

        var activeAppointment = patient.Appointments
            .OrderByDescending(a => a.StartDateTime)
            .FirstOrDefault(a => a.Status != AppointmentStatus.Cancelled);

        var activeRecord = await _context.OdontogramRecords
            .AsNoTracking()
            .Include(o => o.ToothSurfaces)
            .Where(o => o.PatientId == patientId && o.IsCurrentActive)
            .OrderByDescending(o => o.SnapshotDate)
            .FirstOrDefaultAsync(ct);

        var result = new OdontogramSnapshotDto
        {
            Id = activeRecord?.Id,
            PatientId = patient.Id,
            PatientName = patient.FullName,
            PatientAge = patient.Age,
            QuickAllergyNote = activeAppointment?.QuickAllergyAlertNote,
            PainLevel = activeAppointment?.PainLevel ?? 0,
            SnapshotTitle = activeRecord?.SnapshotTitle ?? "Diagnóstico Inicial de Ingreso",
            SnapshotDate = activeRecord?.SnapshotDate ?? DateTime.UtcNow,
            ClinicalNotes = activeRecord?.ClinicalNotes ?? activeAppointment?.ClinicalEvolutionNotes,
            TotalEstimatedCost = activeRecord?.TotalEstimatedCost ?? 0,
            MedicalAlerts = patient.MedicalAlerts.Select(m => new MedicalAlertSummaryDto
            {
                Id = m.Id,
                Description = m.Description,
                AlertType = m.AlertType,
                Severity = m.Severity,
                ActionProtocol = m.ActionProtocol
            }).ToList()
        };

        if (activeRecord != null)
        {
            result.ToothSurfaces = activeRecord.ToothSurfaces.Select(t => new ToothSurfaceDto
            {
                ToothNumber = t.ToothNumber,
                Surface = t.Surface,
                ConditionType = t.ConditionType,
                Status = t.Status,
                ServiceCatalogId = t.ServiceCatalogId,
                EstimatedPrice = t.EstimatedPrice,
                ClinicalNotes = t.ClinicalNotes
            }).ToList();
        }

        return result;
    }

    public async Task<OdontogramSnapshotDto> SaveSnapshotAsync(SaveOdontogramRequest request, CancellationToken ct = default)
    {
        // 1. Marcar snapshot previo como no activo
        var previousActive = await _context.OdontogramRecords
            .Where(o => o.PatientId == request.PatientId && o.IsCurrentActive)
            .ToListAsync(ct);

        foreach (var prev in previousActive)
        {
            prev.IsCurrentActive = false;
        }

        // 2. Calcular presupuesto automático de piezas con tratamiento pendiente
        decimal totalBudget = request.ToothSurfaces
            .Where(t => t.Status == TreatmentClinicalStatus.PlanTreatmentPending)
            .Sum(t => t.EstimatedPrice);

        // 3. Crear nuevo snapshot histórico
        var newRecord = new OdontogramRecord
        {
            PatientId = request.PatientId,
            DentistId = request.DentistId,
            SnapshotTitle = request.SnapshotTitle,
            SnapshotDate = DateTime.UtcNow,
            ClinicalNotes = request.ClinicalNotes,
            IsCurrentActive = true,
            TotalEstimatedCost = totalBudget
        };

        foreach (var toothDto in request.ToothSurfaces)
        {
            newRecord.ToothSurfaces.Add(new ToothSurfaceState
            {
                ToothNumber = toothDto.ToothNumber,
                Surface = toothDto.Surface,
                ConditionType = toothDto.ConditionType,
                Status = toothDto.Status,
                ServiceCatalogId = toothDto.ServiceCatalogId,
                EstimatedPrice = toothDto.EstimatedPrice,
                ClinicalNotes = toothDto.ClinicalNotes,
                CreatedAt = DateTime.UtcNow
            });
        }

        _context.OdontogramRecords.Add(newRecord);
        await _context.SaveChangesAsync(ct);

        return await GetActiveOdontogramAsync(request.PatientId, ct);
    }

    public async Task<List<OdontogramSnapshotSummaryDto>> GetHistoryAsync(int patientId, CancellationToken ct = default)
    {
        return await _context.OdontogramRecords
            .AsNoTracking()
            .Include(o => o.Dentist).ThenInclude(d => d.User)
            .Include(o => o.ToothSurfaces)
            .Where(o => o.PatientId == patientId)
            .OrderByDescending(o => o.SnapshotDate)
            .Select(o => new OdontogramSnapshotSummaryDto
            {
                Id = o.Id,
                Title = o.SnapshotTitle,
                Date = o.SnapshotDate,
                DoctorName = $"Dr(a). {o.Dentist.User.FullName}",
                TotalBudget = o.TotalEstimatedCost,
                AffectedTeethCount = o.ToothSurfaces.Select(t => t.ToothNumber).Distinct().Count()
            })
            .ToListAsync(ct);
    }
}
