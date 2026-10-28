using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using DentalSaaS.Application.DTOs;
using DentalSaaS.Application.Interfaces;
using DentalSaaS.Application.Services;
using DentalSaaS.Domain.Entities;
using DentalSaaS.Domain.Enums;
using DentalSaaS.Infrastructure;
using DentalSaaS.Infrastructure.Persistence;
using DentalSaaS.Web.Hubs;

// Habilitar compatibilidad con timestamps en PostgreSQL (Npgsql)
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// 1. Inyección de dependencias de infraestructura y servicios
builder.Services.AddDentalSaaSInfrastructure(builder.Configuration);

// 2. SignalR para notificaciones y alertas clínicas en tiempo real
builder.Services.AddSignalR();

// 3. Autenticación y Autorización
builder.Services.AddAuthentication()
    .AddCookie(options =>
    {
        options.LoginPath = "/admin/login";
        options.AccessDeniedPath = "/admin/denied";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("SuperAdminOnly", policy => policy.RequireRole(UserRoles.SuperAdmin));
    options.AddPolicy("DentistOrAdmin", policy => policy.RequireRole(UserRoles.SuperAdmin, UserRoles.Dentist));
    options.AddPolicy("StaffOnly", policy => policy.RequireRole(UserRoles.SuperAdmin, UserRoles.Dentist, UserRoles.Receptionist));
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()
              .SetIsOriginAllowed(_ => true);
    });
});

var app = builder.Build();

app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

// 4. Semilla de Base de Datos al iniciar
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();

    await DbInitializer.SeedAsync(context, userManager, roleManager);
}

// 5. Hub de SignalR
app.MapHub<ClinicHub>("/hubs/clinic");

// ==========================================
// 6. ENDPOINTS DE LA API DEL SISTEMA DENTAL
// ==========================================

// Salud del sistema
app.MapGet("/api/health", () => Results.Ok(new
{
    System = "Dental SaaS Core API",
    Version = "2.0-Phase2",
    Status = "Healthy",
    Realtime = "SignalR Active",
    Timestamp = DateTime.UtcNow
}));

// A. Consulta pública de disponibilidad (Motor matemático)
app.MapGet("/api/availability", async (
    [FromQuery] DateTime date,
    [FromQuery] int serviceId,
    [FromQuery] int? dentistId,
    [FromServices] IAvailabilityService availabilityService,
    CancellationToken ct) =>
{
    var request = new SlotSearchRequest
    {
        Date = date,
        ServiceId = serviceId,
        PreferredDentistId = dentistId,
        SlotIntervalMinutes = 30
    };

    var result = await availabilityService.GetDailyAvailabilityAsync(request, ct);
    return Results.Ok(result);
});

// B. Agendamiento público sin login (Con control de concurrencia y alerta a recepción)
app.MapPost("/api/appointments/book", async (
    [FromBody] AppointmentBookingDto dto,
    [FromServices] IAvailabilityService availabilityService,
    [FromServices] IHubContext<ClinicHub, IClinicClient> hubContext,
    CancellationToken ct) =>
{
    var result = await availabilityService.BookAppointmentAsync(dto, ct);
    if (!result.Success)
    {
        return Results.BadRequest(result);
    }

    // Notificar a recepción vía SignalR en tiempo real
    await hubContext.Clients.All.NewAppointmentCreated(new
    {
        AppointmentId = result.AppointmentId,
        AppointmentCode = result.AppointmentCode,
        PatientName = $"{dto.FirstName} {dto.LastName}",
        ServiceName = result.ServiceName,
        DentistName = result.DentistName,
        StartDateTime = result.StartDateTime
    });

    return Results.Ok(result);
});

// C. Inscripción en Lista de Espera (Waitlist)
app.MapPost("/api/waitlist/join", async (
    [FromBody] WaitlistJoinDto dto,
    [FromServices] IAvailabilityService availabilityService,
    CancellationToken ct) =>
{
    var success = await availabilityService.JoinWaitlistAsync(dto, ct);
    return success ? Results.Ok(new { Success = true, Message = "Inscrito con éxito en la lista de espera." })
                   : Results.BadRequest(new { Success = false, Message = "No fue posible unirse a la lista de espera." });
});

// D. Portal de Autogestión del Paciente (Magic Link)
app.MapGet("/api/appointments/magic/{token:guid}", async (
    Guid token,
    [FromServices] ApplicationDbContext context) =>
{
    var appointment = await context.Appointments
        .Include(a => a.Patient)
        .Include(a => a.Dentist).ThenInclude(d => d.User)
        .Include(a => a.DentalChair)
        .Include(a => a.ServiceCatalog)
        .FirstOrDefaultAsync(a => a.MagicToken == token);

    if (appointment == null)
    {
        return Results.NotFound(new { Message = "El enlace de gestión es inválido o ha expirado." });
    }

    return Results.Ok(new
    {
        appointment.Id,
        appointment.AppointmentCode,
        PatientName = appointment.Patient.FullName,
        DoctorName = $"Dr(a). {appointment.Dentist.User.FullName}",
        DoctorSpecialty = appointment.Dentist.Specialty,
        ChairName = appointment.DentalChair.Name,
        ServiceName = appointment.ServiceCatalog.Name,
        Price = appointment.ServiceCatalog.Price,
        appointment.StartDateTime,
        appointment.EndDateTime,
        Status = appointment.Status.ToString(),
        IsConfirmed = appointment.Status == AppointmentStatus.Confirmed
    });
});

// E. Confirmación 1-clic desde Magic Link
app.MapPost("/api/appointments/magic/{token:guid}/confirm", async (
    Guid token,
    [FromServices] ApplicationDbContext context,
    [FromServices] IHubContext<ClinicHub, IClinicClient> hubContext) =>
{
    var appointment = await context.Appointments
        .Include(a => a.Patient)
        .FirstOrDefaultAsync(a => a.MagicToken == token);

    if (appointment == null) return Results.NotFound(new { Message = "Cita no encontrada." });
    if (appointment.Status == AppointmentStatus.Cancelled) return Results.BadRequest(new { Message = "Esta cita fue cancelada previamente." });

    appointment.Status = AppointmentStatus.Confirmed;
    appointment.UpdatedAt = DateTime.UtcNow;
    await context.SaveChangesAsync();

    // Notificar actualización en vivo al tablero Kanban
    await hubContext.Clients.All.AppointmentStatusChanged(new
    {
        AppointmentId = appointment.Id,
        NewStatus = "Confirmed",
        PatientName = appointment.Patient.FullName
    });

    return Results.Ok(new { Success = true, Message = "¡Tu cita ha sido confirmada con éxito! Te esperamos." });
});

// F. Tablero Kanban / Sala de Espera en Vivo
app.MapGet("/api/appointments/kanban", async (
    [FromQuery] DateTime? date,
    [FromServices] ApplicationDbContext context) =>
{
    var targetDate = (date ?? DateTime.UtcNow).Date;
    var dayStart = targetDate;
    var dayEnd = targetDate.AddDays(1);

    var appointments = await context.Appointments
        .AsNoTracking()
        .Include(a => a.Patient).ThenInclude(p => p.MedicalAlerts)
        .Include(a => a.Dentist).ThenInclude(d => d.User)
        .Include(a => a.DentalChair)
        .Include(a => a.ServiceCatalog)
        .Where(a => a.StartDateTime >= dayStart && a.StartDateTime < dayEnd)
        .OrderBy(a => a.StartDateTime)
        .Select(a => new
        {
            a.Id,
            a.AppointmentCode,
            PatientId = a.Patient.Id,
            PatientName = a.Patient.FullName,
            PatientPhone = a.Patient.PhoneNumber,
            DentistId = a.DentistId,
            DentistName = $"Dr(a). {a.Dentist.User.FullName}",
            DentistColorHex = a.Dentist.CalendarColorHex,
            DentalChairId = a.DentalChairId,
            DentalChairName = a.DentalChair.Name,
            ServiceId = a.ServiceCatalogId,
            ServiceName = a.ServiceCatalog.Name,
            ServicePrice = a.ServiceCatalog.Price,
            a.StartDateTime,
            a.EndDateTime,
            Status = a.Status.ToString(),
            a.IsEmergency,
            a.PainLevel,
            a.QuickAllergyAlertNote,
            a.ClinicalEvolutionNotes,
            a.WaitingRoomCheckInAt,
            a.InChairCheckInAt,
            HasCriticalAlerts = a.Patient.MedicalAlerts.Any(m => m.Severity == MedicalAlertSeverity.Critical),
            CriticalAlerts = a.Patient.MedicalAlerts.Where(m => m.Severity == MedicalAlertSeverity.Critical).Select(m => m.Description).ToList()
        })
        .ToListAsync();

    return Results.Ok(appointments);
});

// G. Control de Flujo: Marcar Paciente "En Sala de Espera" (Dispara alerta visual/sonora a doctor)
app.MapPost("/api/appointments/{id:int}/checkin", async (
    int id,
    [FromServices] ApplicationDbContext context,
    [FromServices] IHubContext<ClinicHub, IClinicClient> hubContext) =>
{
    var appointment = await context.Appointments
        .Include(a => a.Patient)
        .Include(a => a.Dentist).ThenInclude(d => d.User)
        .Include(a => a.DentalChair)
        .Include(a => a.ServiceCatalog)
        .FirstOrDefaultAsync(a => a.Id == id);

    if (appointment == null) return Results.NotFound();

    appointment.Status = AppointmentStatus.WaitingRoom;
    appointment.WaitingRoomCheckInAt = DateTime.UtcNow;
    appointment.UpdatedAt = DateTime.UtcNow;
    await context.SaveChangesAsync();

    // Notificación SignalR en tiempo real dirigida a la vista del doctor
    await hubContext.Clients.All.PatientArrivedInWaitingRoom(new
    {
        AppointmentId = appointment.Id,
        PatientId = appointment.PatientId,
        PatientName = appointment.Patient.FullName,
        DentistId = appointment.DentistId,
        DentistName = $"Dr(a). {appointment.Dentist.User.FullName}",
        ServiceName = appointment.ServiceCatalog.Name,
        DentalChairName = appointment.DentalChair.Name,
        CheckInTime = appointment.WaitingRoomCheckInAt,
        PainLevel = appointment.PainLevel,
        IsEmergency = appointment.IsEmergency,
        QuickAllergy = appointment.QuickAllergyAlertNote
    });

    return Results.Ok(new { Success = true, Message = $"{appointment.Patient.FullName} ha sido ingresado a Sala de Espera." });
});

// H. Control de Flujo: Doctor Llama al Paciente al Sillón
app.MapPost("/api/appointments/{id:int}/calltochair", async (
    int id,
    [FromServices] ApplicationDbContext context,
    [FromServices] IHubContext<ClinicHub, IClinicClient> hubContext) =>
{
    var appointment = await context.Appointments
        .Include(a => a.Patient)
        .Include(a => a.Dentist).ThenInclude(d => d.User)
        .Include(a => a.DentalChair)
        .FirstOrDefaultAsync(a => a.Id == id);

    if (appointment == null) return Results.NotFound();

    appointment.Status = AppointmentStatus.InChair;
    appointment.InChairCheckInAt = DateTime.UtcNow;
    appointment.UpdatedAt = DateTime.UtcNow;
    await context.SaveChangesAsync();

    await hubContext.Clients.All.PatientCalledToChair(new
    {
        AppointmentId = appointment.Id,
        PatientName = appointment.Patient.FullName,
        DoctorName = $"Dr(a). {appointment.Dentist.User.FullName}",
        ChairName = appointment.DentalChair.Name
    });

    return Results.Ok(new { Success = true });
});

// I. Control de Flujo: Finalizar y Cobrar
app.MapPost("/api/appointments/{id:int}/complete", async (
    int id,
    [FromBody] PaymentRecord? paymentInfo,
    [FromServices] ApplicationDbContext context,
    [FromServices] IHubContext<ClinicHub, IClinicClient> hubContext) =>
{
    var appointment = await context.Appointments
        .Include(a => a.ServiceCatalog)
        .FirstOrDefaultAsync(a => a.Id == id);

    if (appointment == null) return Results.NotFound();

    appointment.Status = AppointmentStatus.Completed;
    appointment.CompletedAt = DateTime.UtcNow;
    appointment.UpdatedAt = DateTime.UtcNow;

    if (paymentInfo != null)
    {
        context.PaymentRecords.Add(new PaymentRecord
        {
            AppointmentId = appointment.Id,
            Amount = paymentInfo.Amount > 0 ? paymentInfo.Amount : appointment.ServiceCatalog.Price,
            PaymentMethod = paymentInfo.PaymentMethod,
            PaymentStatus = PaymentStatus.Paid,
            TransactionReference = paymentInfo.TransactionReference,
            PaidAt = DateTime.UtcNow,
            Notes = paymentInfo.Notes
        });
    }

    await context.SaveChangesAsync();

    await hubContext.Clients.All.AppointmentStatusChanged(new
    {
        AppointmentId = appointment.Id,
        NewStatus = "Completed"
    });

    return Results.Ok(new { Success = true });
});

// J. Reagendamiento / Mover cita (Drag and drop)
app.MapPost("/api/appointments/{id:int}/move", async (
    int id,
    [FromBody] MoveAppointmentDto dto,
    [FromServices] ApplicationDbContext context,
    [FromServices] IHubContext<ClinicHub, IClinicClient> hubContext) =>
{
    var appointment = await context.Appointments.FindAsync(id);
    if (appointment == null) return Results.NotFound();

    appointment.StartDateTime = dto.NewStartDateTime;
    appointment.EndDateTime = dto.NewEndDateTime;
    if (dto.NewDentistId.HasValue) appointment.DentistId = dto.NewDentistId.Value;
    if (dto.NewDentalChairId.HasValue) appointment.DentalChairId = dto.NewDentalChairId.Value;
    appointment.UpdatedAt = DateTime.UtcNow;

    await context.SaveChangesAsync();

    await hubContext.Clients.All.AppointmentStatusChanged(new
    {
        AppointmentId = appointment.Id,
        NewStatus = appointment.Status.ToString(),
        Moved = true
    });

    return Results.Ok(new { Success = true });
});

// K. Odontograma Interactivo: Obtener snapshot activo
app.MapGet("/api/patients/{patientId:int}/odontogram", async (
    int patientId,
    [FromServices] IOdontogramService odontogramService,
    CancellationToken ct) =>
{
    var snapshot = await odontogramService.GetActiveOdontogramAsync(patientId, ct);
    return Results.Ok(snapshot);
});

// L. Odontograma Interactivo: Guardar cambios y calcular presupuesto automático
app.MapPost("/api/patients/{patientId:int}/odontogram", async (
    int patientId,
    [FromBody] SaveOdontogramRequest request,
    [FromServices] IOdontogramService odontogramService,
    [FromServices] IHubContext<ClinicHub, IClinicClient> hubContext,
    CancellationToken ct) =>
{
    request.PatientId = patientId;
    var result = await odontogramService.SaveSnapshotAsync(request, ct);

    await hubContext.Clients.All.OdontogramUpdated(new
    {
        PatientId = patientId,
        TotalBudget = result.TotalEstimatedCost,
        SnapshotDate = result.SnapshotDate
    });

    return Results.Ok(result);
});

// M. Odontograma: Historial de snapshots para comparación temporal
app.MapGet("/api/patients/{patientId:int}/odontogram/history", async (
    int patientId,
    [FromServices] IOdontogramService odontogramService,
    CancellationToken ct) =>
{
    var history = await odontogramService.GetHistoryAsync(patientId, ct);
    return Results.Ok(history);
});

// N. Servicios y Doctores para UI
app.MapGet("/api/services", async ([FromServices] ApplicationDbContext context) =>
{
    var services = await context.ServiceCatalogs
        .Where(s => s.IsActive)
        .OrderBy(s => s.Id)
        .ToListAsync();
    return Results.Ok(services);
});

app.MapGet("/api/dentists", async ([FromServices] ApplicationDbContext context) =>
{
    var dentists = await context.DentistProfiles
        .Include(d => d.User)
        .Where(d => d.IsActive)
        .Select(d => new
        {
            d.Id,
            FullName = $"Dr(a). {d.User.FullName}",
            d.Specialty,
            d.LicenseNumber,
            d.Biography,
            d.CalendarColorHex,
            d.User.AvatarUrl
        })
        .ToListAsync();
    return Results.Ok(dentists);
});

app.MapGet("/api/dental-chairs", async ([FromServices] ApplicationDbContext context) =>
{
    var chairs = await context.DentalChairs
        .Where(c => c.IsActive)
        .OrderBy(c => c.Id)
        .ToListAsync();
    return Results.Ok(chairs);
});

// GESTIÓN DE HORARIOS EN VIVO: Bloquear y Liberar Horarios de Dentistas
app.MapGet("/api/dentists/{dentistId:int}/exceptions", async (
    int dentistId,
    [FromQuery] DateTime? date,
    [FromServices] IAvailabilityService availabilityService,
    CancellationToken ct) =>
{
    var targetDate = date ?? DateTime.Now;
    var exceptions = await availabilityService.GetDentistExceptionsAsync(dentistId, targetDate, ct);
    return Results.Ok(exceptions);
});

app.MapPost("/api/dentists/block-slot", async (
    [FromBody] BlockScheduleRequestDto dto,
    [FromServices] IAvailabilityService availabilityService,
    [FromServices] IHubContext<ClinicHub, IClinicClient> hubContext,
    CancellationToken ct) =>
{
    var result = await availabilityService.BlockScheduleAsync(dto, ct);
    if (!result.Success)
    {
        return Results.BadRequest(result);
    }

    await hubContext.Clients.All.AvailabilityChanged(new
    {
        DentistId = dto.DentistId,
        Action = "Blocked",
        Start = dto.StartDateTime,
        End = dto.EndDateTime,
        Reason = dto.Reason
    });

    return Results.Ok(result);
});

app.MapDelete("/api/dentists/exceptions/{id:int}", async (
    int id,
    [FromServices] IAvailabilityService availabilityService,
    [FromServices] IHubContext<ClinicHub, IClinicClient> hubContext,
    CancellationToken ct) =>
{
    var success = await availabilityService.ReleaseScheduleExceptionAsync(id, ct);
    if (!success)
    {
        return Results.NotFound(new { Success = false, Message = "El bloqueo de horario no fue encontrado." });
    }

    await hubContext.Clients.All.AvailabilityChanged(new
    {
        ExceptionId = id,
        Action = "Released"
    });

    return Results.Ok(new { Success = true, Message = "Horario liberado con éxito. El espacio vuelve a estar disponible para agendar." });
});

// O. Cola de Notificaciones (Monitoreo para Recepción)
app.MapGet("/api/notifications/queue", async ([FromServices] ApplicationDbContext context) =>
{
    var logs = await context.NotificationLogs
        .OrderByDescending(n => n.ScheduledFor)
        .Take(50)
        .Select(n => new
        {
            n.Id,
            n.RecipientName,
            n.RecipientContact,
            Channel = n.Channel.ToString(),
            Type = n.NotificationType.ToString(),
            Status = n.Status.ToString(),
            n.ScheduledFor,
            n.SentAt,
            n.ErrorDetails,
            n.RetryCount,
            n.MessageBody
        })
        .ToListAsync();
    return Results.Ok(logs);
});

// ========================================================
// P. ASISTENTE IA CLÍNICO (Auditoría Estricta por Folio)
// ========================================================
app.MapPost("/api/ai/audit", async (
    [FromBody] AiAuditRequest req,
    [FromServices] IClinicalAiService aiService,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.Folio))
    {
        return Results.BadRequest(new { Message = "El Folio del expediente es obligatorio para realizar una auditoría segura." });
    }

    var result = await aiService.AuditPatientByFolioAsync(req.Folio, req.Query ?? "Auditoría de alergias y antecedentes", ct);
    return Results.Ok(result);
});

// Q. Expediente Clínico Completo por Folio (FOLIO-00001 a N)
app.MapGet("/api/patients/by-folio/{folio}", async (
    string folio,
    [FromServices] ApplicationDbContext context) =>
{
    var cleanFolio = folio.Trim().ToUpper();
    if (!cleanFolio.StartsWith("FOLIO-") && int.TryParse(cleanFolio, out int num))
    {
        cleanFolio = $"FOLIO-{num:D5}";
    }

    var patient = await context.Patients
        .AsNoTracking()
        .Include(p => p.MedicalAlerts)
        .Include(p => p.Prescriptions).ThenInclude(pr => pr.Items)
        .Include(p => p.Documents)
        .Include(p => p.Appointments).ThenInclude(a => a.ServiceCatalog)
        .Include(p => p.OdontogramRecords).ThenInclude(o => o.ToothSurfaces)
        .FirstOrDefaultAsync(p => p.FolioNumber == cleanFolio);

    if (patient == null)
    {
        return Results.NotFound(new { Message = $"No existe ningún expediente con el folio {cleanFolio}" });
    }

    var activePrescriptions = patient.Prescriptions
        .OrderByDescending(pr => pr.CreatedAt)
        .Select(pr => new
        {
            pr.Id,
            pr.PrescriptionFolio,
            pr.Diagnosis,
            pr.GeneralInstructions,
            pr.CreatedAt,
            Items = pr.Items.Select(i => new
            {
                i.Id,
                i.MedicineName,
                i.Dosage,
                i.FrequencyHours,
                i.DurationDays,
                i.Instructions,
                i.StartDate,
                Schedule = i.ReminderScheduleDescription
            }).ToList()
        }).ToList();

    var documents = patient.Documents.Select(d => new
    {
        d.Id,
        d.Title,
        Type = d.DocumentType.ToString(),
        d.OriginalFileName,
        d.RelativePath,
        d.FileSizeBytes,
        d.UploadedAt,
        d.Notes
    }).ToList();

    return Results.Ok(new
    {
        patient.Id,
        patient.FolioNumber,
        patient.FirstName,
        patient.LastName,
        FullName = patient.FullName,
        patient.Curp,
        patient.PhoneNumber,
        patient.Email,
        patient.DateOfBirth,
        patient.Age,
        patient.RequiresInvoice,
        patient.TaxId,
        patient.LegalName,
        patient.TaxRegime,
        patient.TaxZipCode,
        patient.CfdiUsage,
        MedicalAlerts = patient.MedicalAlerts.Select(m => new
        {
            m.Id,
            m.Description,
            Severity = m.Severity.ToString(),
            m.ActionProtocol
        }).ToList(),
        Prescriptions = activePrescriptions,
        Documents = documents,
        Appointments = patient.Appointments.OrderByDescending(a => a.StartDateTime).Select(a => new
        {
            a.Id,
            a.AppointmentCode,
            ServiceName = a.ServiceCatalog.Name,
            a.StartDateTime,
            Status = a.Status.ToString(),
            a.ClinicalEvolutionNotes
        }).ToList()
    });
});

// R. Subir Documentos al Expediente (Migración Digital de Archivos en Papel)
app.MapPost("/api/patients/{folio}/documents", async (
    string folio,
    HttpRequest request,
    [FromServices] ApplicationDbContext context,
    IWebHostEnvironment env) =>
{
    var cleanFolio = folio.Trim().ToUpper();
    if (!cleanFolio.StartsWith("FOLIO-") && int.TryParse(cleanFolio, out int num))
    {
        cleanFolio = $"FOLIO-{num:D5}";
    }

    var patient = await context.Patients.FirstOrDefaultAsync(p => p.FolioNumber == cleanFolio);
    if (patient == null) return Results.NotFound(new { Message = "Expediente no encontrado." });

    if (!request.HasFormContentType) return Results.BadRequest(new { Message = "Debe enviar un formulario multipart con el archivo." });

    var form = await request.ReadFormAsync();
    var file = form.Files.GetFile("file");
    var title = form["title"].ToString();
    var docTypeStr = form["documentType"].ToString();
    var notes = form["notes"].ToString();

    if (file == null || file.Length == 0) return Results.BadRequest(new { Message = "No se adjuntó ningún archivo válido." });

    var uploadsFolder = Path.Combine(env.WebRootPath, "uploads", cleanFolio);
    Directory.CreateDirectory(uploadsFolder);

    var safeExt = Path.GetExtension(file.FileName).ToLowerInvariant();
    var storedName = $"{Guid.NewGuid():N}{safeExt}";
    var physicalPath = Path.Combine(uploadsFolder, storedName);

    using (var stream = new FileStream(physicalPath, FileMode.Create))
    {
        await file.CopyToAsync(stream);
    }

    var docType = Enum.TryParse<PatientDocumentType>(docTypeStr, out var parsed) ? parsed : PatientDocumentType.HistorialPapelEscaneado;

    var docRecord = new PatientDocument
    {
        PatientId = patient.Id,
        Title = string.IsNullOrWhiteSpace(title) ? file.FileName : title,
        DocumentType = docType,
        OriginalFileName = file.FileName,
        StoredFileName = storedName,
        RelativePath = $"/uploads/{cleanFolio}/{storedName}",
        ContentType = file.ContentType,
        FileSizeBytes = file.Length,
        Notes = notes,
        UploadedAt = DateTime.UtcNow
    };

    context.PatientDocuments.Add(docRecord);
    await context.SaveChangesAsync();

    return Results.Ok(new { Success = true, DocumentId = docRecord.Id, RelativePath = docRecord.RelativePath });
});

// S. Emitir Receta Médica con Recordatorios de Tomas
app.MapPost("/api/prescriptions", async (
    [FromBody] CreatePrescriptionRequest req,
    [FromServices] ApplicationDbContext context) =>
{
    var patient = await context.Patients.FindAsync(req.PatientId);
    if (patient == null) return Results.NotFound();

    var count = await context.Prescriptions.CountAsync() + 1;
    var prescription = new Prescription
    {
        PatientId = req.PatientId,
        DentistId = req.DentistId,
        AppointmentId = req.AppointmentId,
        PrescriptionFolio = $"REC-{count:D5}",
        Diagnosis = req.Diagnosis,
        GeneralInstructions = req.GeneralInstructions,
        CreatedAt = DateTime.UtcNow
    };

    foreach (var item in req.Items)
    {
        prescription.Items.Add(new PrescriptionItem
        {
            MedicineName = item.MedicineName,
            Dosage = item.Dosage,
            FrequencyHours = item.FrequencyHours,
            DurationDays = item.DurationDays,
            Instructions = item.Instructions,
            StartDate = DateTime.UtcNow
        });
    }

    context.Prescriptions.Add(prescription);
    await context.SaveChangesAsync();

    return Results.Ok(new { Success = true, Folio = prescription.PrescriptionFolio });
});

// T. Cancelación y Reagendamiento con Reglas de Negocio Estrictas
app.MapPost("/api/appointments/{id:int}/cancel-rule", async (
    int id,
    [FromServices] ApplicationDbContext context,
    [FromServices] IHubContext<ClinicHub, IClinicClient> hubContext) =>
{
    var appointment = await context.Appointments
        .Include(a => a.Patient)
        .FirstOrDefaultAsync(a => a.Id == id);

    if (appointment == null) return Results.NotFound();

    // Regla: Cancelación permitida únicamente con mínimo 1 día de anticipación
    if (!appointment.CanPatientCancelDirectly)
    {
        return Results.BadRequest(new
        {
            Success = false,
            ErrorMessage = "Política de la clínica: La cancelación debe realizarse con un mínimo de 24 horas (1 día) de anticipación. Por favor contacta a recepción."
        });
    }

    appointment.Status = AppointmentStatus.Cancelled;
    appointment.CancelledAt = DateTime.UtcNow;
    appointment.CancellationReason = "Cancelada por el paciente dentro del plazo permitido (24h de anticipación).";
    appointment.UpdatedAt = DateTime.UtcNow;
    await context.SaveChangesAsync();

    await hubContext.Clients.All.AppointmentStatusChanged(new
    {
        AppointmentId = appointment.Id,
        NewStatus = "Cancelled",
        PatientName = appointment.Patient.FullName
    });

    return Results.Ok(new { Success = true, Message = "Tu cita ha sido cancelada con éxito. El espacio se ha liberado para la lista de espera." });
});

// U. Regla de Confirmación (Máximo 15 min antes, de lo contrario Reagendar)
app.MapPost("/api/appointments/{id:int}/enforce-confirmation", async (
    int id,
    [FromServices] ApplicationDbContext context,
    [FromServices] IHubContext<ClinicHub, IClinicClient> hubContext) =>
{
    var appointment = await context.Appointments
        .Include(a => a.Patient)
        .FirstOrDefaultAsync(a => a.Id == id);

    if (appointment == null) return Results.NotFound();

    if (appointment.IsConfirmationExpired)
    {
        // Marcar para reagendar automáticamente
        appointment.NeedsReschedule = true;
        appointment.CancellationReason = "No confirmada a tiempo (máximo 15 min antes). Puesta en cola de reagendamiento.";
        await context.SaveChangesAsync();

        await hubContext.Clients.All.AppointmentStatusChanged(new
        {
            AppointmentId = appointment.Id,
            NewStatus = "NeedsReschedule",
            PatientName = appointment.Patient.FullName
        });

        return Results.Ok(new
        {
            Success = true,
            Overdue = true,
            Message = "La cita no fue confirmada con los 15 minutos mínimos de anticipación requeridos. Se ha puesto en lista de reagendamiento."
        });
    }

    return Results.Ok(new { Success = true, Overdue = false, Message = "Cita dentro del tiempo de confirmación." });
});

// Fallback para SPA client-side routing
app.MapFallbackToFile("index.html");

app.Run();

public class MoveAppointmentDto
{
    public DateTime NewStartDateTime { get; set; }
    public DateTime NewEndDateTime { get; set; }
    public int? NewDentistId { get; set; }
    public int? NewDentalChairId { get; set; }
}

public class AiAuditRequest
{
    public string Folio { get; set; } = string.Empty;
    public string? Query { get; set; }
}

public class CreatePrescriptionRequest
{
    public int PatientId { get; set; }
    public int DentistId { get; set; }
    public int? AppointmentId { get; set; }
    public string Diagnosis { get; set; } = string.Empty;
    public string? GeneralInstructions { get; set; }
    public List<PrescriptionItemDto> Items { get; set; } = new();
}

public class PrescriptionItemDto
{
    public string MedicineName { get; set; } = string.Empty;
    public string Dosage { get; set; } = string.Empty;
    public int FrequencyHours { get; set; } = 8;
    public int DurationDays { get; set; } = 5;
    public string Instructions { get; set; } = string.Empty;
}
