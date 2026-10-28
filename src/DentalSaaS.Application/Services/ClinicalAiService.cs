using Microsoft.EntityFrameworkCore;
using DentalSaaS.Application.Interfaces;
using DentalSaaS.Domain.Enums;

namespace DentalSaaS.Application.Services;

public class ClinicalAiResponseDto
{
    public bool Success { get; set; }
    public string FolioNumber { get; set; } = string.Empty;
    public string PatientName { get; set; } = string.Empty;
    public string? Curp { get; set; }
    public int? Age { get; set; }
    public bool HasCriticalAllergies { get; set; }
    public List<string> CriticalAlerts { get; set; } = new();
    public List<string> ContraindicationsAndWarnings { get; set; } = new();
    public string AiAnalysisMarkdown { get; set; } = string.Empty;
    public DateTime AuditedAt { get; set; } = DateTime.UtcNow;
}

public interface IClinicalAiService
{
    Task<ClinicalAiResponseDto> AuditPatientByFolioAsync(string folio, string userQuery, CancellationToken ct = default);
}

public class ClinicalAiService : IClinicalAiService
{
    private readonly IApplicationDbContext _context;

    public ClinicalAiService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ClinicalAiResponseDto> AuditPatientByFolioAsync(
        string folio,
        string userQuery,
        CancellationToken ct = default)
    {
        var cleanFolio = folio.Trim().ToUpper();
        if (!cleanFolio.StartsWith("FOLIO-") && int.TryParse(cleanFolio, out int num))
        {
            cleanFolio = $"FOLIO-{num:D5}";
        }

        // 1. BÚSQUEDA DETERMINISTA ESTRICTA (Cero alucinación - Anclaje por Folio)
        var patient = await _context.Patients
            .AsNoTracking()
            .Include(p => p.MedicalAlerts)
            .Include(p => p.Prescriptions).ThenInclude(pr => pr.Items)
            .Include(p => p.Appointments).ThenInclude(a => a.ServiceCatalog)
            .Include(p => p.OdontogramRecords).ThenInclude(o => o.ToothSurfaces)
            .FirstOrDefaultAsync(p => p.FolioNumber == cleanFolio, ct);

        if (patient == null)
        {
            return new ClinicalAiResponseDto
            {
                Success = false,
                FolioNumber = cleanFolio,
                AiAnalysisMarkdown = $"⛔ **ERROR CRÍTICO DE SEGURIDAD**: No se encontró ningún expediente clínico registrado con el **{cleanFolio}**. No se emiten recomendaciones clínicas sin validación previa del expediente."
            };
        }

        var response = new ClinicalAiResponseDto
        {
            Success = true,
            FolioNumber = patient.FolioNumber,
            PatientName = patient.FullName,
            Curp = patient.Curp,
            Age = patient.Age
        };

        // 2. EXTRACCIÓN DE ALERTAS MÉDICAS CRÍTICAS DIRECTAMENTE DE LA BD
        var allergies = patient.MedicalAlerts
            .Where(m => m.AlertType == MedicalAlertType.Allergy || m.Severity == MedicalAlertSeverity.Critical)
            .ToList();

        response.HasCriticalAllergies = allergies.Count > 0;
        foreach (var a in allergies)
        {
            response.CriticalAlerts.Add($"⚠️ {a.Description} (Protocolo: {a.ActionProtocol ?? "Verificar antes de medicar"})");
        }

        // 3. MOTOR DE VALIDACIÓN FARMACOLÓGICA Y CONTRAINDICACIONES
        var queryLower = userQuery.ToLower();
        var warnings = new List<string>();

        // Regla: Penicilina y Beta-lactámicos
        bool isPenicillinAllergic = patient.MedicalAlerts.Any(m =>
            m.Description.Contains("penicilina", StringComparison.OrdinalIgnoreCase) ||
            m.Description.Contains("amoxicilina", StringComparison.OrdinalIgnoreCase));

        if (isPenicillinAllergic)
        {
            warnings.Add("⛔ **CONTRAINDICACIÓN ABSOLUTA**: Paciente alérgico a PENICILINA. Prohibido prescribir Amoxicilina, Ampicilina, Augmentin o Cefalosporinas de 1ra generación.");
            warnings.Add("✅ **ALTERNATIVA SEGURA**: Clindamicina 300mg cada 6 hrs o Azitromicina 500mg cada 24 hrs.");
        }

        // Regla: Hipertensión y Anestésicos
        bool isHypertensive = patient.MedicalAlerts.Any(m =>
            m.Description.Contains("hipertens", StringComparison.OrdinalIgnoreCase) ||
            m.Description.Contains("presion", StringComparison.OrdinalIgnoreCase));

        if (isHypertensive)
        {
            warnings.Add("⚠️ **PRECAUCIÓN ANESTÉSICA**: Paciente hipertenso. Evitar anestésicos locales con vasoconstrictor adrenérgico en dosis elevadas (Epinefrina). Se recomienda Mepivacaína al 3% o Prilocaína.");
        }

        // Regla: Embarazo
        bool isPregnant = patient.MedicalAlerts.Any(m =>
            m.AlertType == MedicalAlertType.Pregnancy ||
            m.Description.Contains("embarazo", StringComparison.OrdinalIgnoreCase));

        if (isPregnant)
        {
            warnings.Add("🤰 **PRECAUCIÓN OBSTÉTRICA**: Paciente en estado de gestación. Contraindicados AINEs en 3er trimestre, Tetraciclinas y radiografías sin delantal plomado con collarín tiroideo.");
        }

        response.ContraindicationsAndWarnings = warnings;

        // 4. GENERACIÓN DEL INFORME ESTRUCTURADO Y TRANSPARENTE
        var lastAppointment = patient.Appointments.OrderByDescending(a => a.StartDateTime).FirstOrDefault();
        var activePrescription = patient.Prescriptions.OrderByDescending(p => p.CreatedAt).FirstOrDefault();
        var activeOdontogram = patient.OdontogramRecords.OrderByDescending(o => o.SnapshotDate).FirstOrDefault();

        var md = $"### 📋 Auditoría Clínica Certificada - {patient.FolioNumber}\n\n";
        md += $"**Paciente:** {patient.FullName} | **Edad:** {patient.Age ?? 0} años | **CURP:** `{patient.Curp ?? "No registrada"}`\n\n";

        if (response.CriticalAlerts.Count > 0)
        {
            md += "#### 🚨 SEMÁFORO DE ALERTAS Y ALERGIAS REGISTRADAS:\n";
            foreach (var alert in response.CriticalAlerts)
            {
                md += $"- **{alert}**\n";
            }
            md += "\n";
        }
        else
        {
            md += "✅ **No se registran alergias severas en el expediente.**\n\n";
        }

        if (warnings.Count > 0)
        {
            md += "#### 🛑 RECOMENDACIONES DE SEGURIDAD FARMACOLÓGICA:\n";
            foreach (var w in warnings)
            {
                md += $"{w}\n\n";
            }
        }

        if (activePrescription != null)
        {
            md += $"#### 💊 Receta Médica Reciente ({activePrescription.PrescriptionFolio}):\n";
            md += $"*Diagnóstico:* {activePrescription.Diagnosis}\n";
            foreach (var item in activePrescription.Items)
            {
                md += $"- **{item.MedicineName}** ({item.Dosage}): {item.Instructions} - {item.ReminderScheduleDescription}\n";
            }
            md += "\n";
        }

        if (activeOdontogram != null)
        {
            int pendingTreatments = activeOdontogram.ToothSurfaces.Count(t => t.Status == TreatmentClinicalStatus.PlanTreatmentPending);
            md += $"#### 🦷 Odontograma Activo ({activeOdontogram.SnapshotDate:dd/MM/yyyy}):\n";
            md += $"- Total presupuestado pendiente: **${activeOdontogram.TotalEstimatedCost:F2}**\n";
            md += $"- Dientes con intervención requerida: **{pendingTreatments} piezas**\n";
        }

        if (lastAppointment != null)
        {
            md += $"\n**Última Cita:** {lastAppointment.StartDateTime:dd/MM/yyyy HH:mm} - *{lastAppointment.ServiceCatalog?.Name}* ({lastAppointment.Status})\n";
            if (!string.IsNullOrWhiteSpace(lastAppointment.ClinicalEvolutionNotes))
            {
                md += $"- *Nota de Evolución:* \"{lastAppointment.ClinicalEvolutionNotes}\"\n";
            }
        }

        response.AiAnalysisMarkdown = md;
        return response;
    }
}
