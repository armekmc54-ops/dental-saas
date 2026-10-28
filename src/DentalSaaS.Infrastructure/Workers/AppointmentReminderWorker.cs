using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using DentalSaaS.Application.Interfaces;
using DentalSaaS.Domain.Entities;
using DentalSaaS.Domain.Enums;
using DentalSaaS.Infrastructure.Persistence;

namespace DentalSaaS.Infrastructure.Workers;

public class AppointmentReminderWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AppointmentReminderWorker> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(2); // Intervalo de escaneo

    public AppointmentReminderWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<AppointmentReminderWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(">>> [Dental SaaS Worker] Servicio de Automatización de Citas y Recordatorios iniciado.");

        // Usamos PeriodicTimer moderno de .NET para control preciso de ciclos en segundo plano
        using var timer = new PeriodicTimer(_checkInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessAllAutomationJobsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Dental SaaS Worker] Error no controlado durante el ciclo de automatización.");
            }

            // Esperar el siguiente ciclo o cancelación
            if (!await timer.WaitForNextTickAsync(stoppingToken))
            {
                break;
            }
        }

        _logger.LogInformation(">>> [Dental SaaS Worker] Servicio de Automatización detenido.");
    }

    private async Task ProcessAllAutomationJobsAsync(CancellationToken ct)
    {
        // Creamos un nuevo Scope por iteración para evitar DbContext singleton y fugas de memoria
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var notificationSender = scope.ServiceProvider.GetRequiredService<INotificationSender>();

        var now = DateTime.UtcNow;

        // 1. Ejecutar Job 1: Recordatorio 24 horas antes (Confirmación con Magic Link)
        await RunJob24HourRemindersAsync(context, notificationSender, now, ct);

        // 2. Ejecutar Job 2: Recordatorio Express 2 horas antes (Ubicación Google Maps)
        await RunJob2HourExpressRemindersAsync(context, notificationSender, now, ct);

        // 3. Ejecutar Job 3: Waitlist Auto-Filler (Liberación de slots y ofertas de lista de espera)
        await RunJobWaitlistAutoFillerAsync(context, notificationSender, now, ct);
    }

    #region Job 1: Recordatorio 24 Horas Antes
    private async Task RunJob24HourRemindersAsync(
        ApplicationDbContext context,
        INotificationSender notificationSender,
        DateTime now,
        CancellationToken ct)
    {
        var windowStart = now.AddHours(23);
        var windowEnd = now.AddHours(25);

        // Buscar citas pendientes de confirmación en la ventana de mañana (23h a 25h)
        var pendingAppointments = await context.Appointments
            .Include(a => a.Patient)
            .Include(a => a.Dentist).ThenInclude(d => d.User)
            .Include(a => a.ServiceCatalog)
            .Include(a => a.NotificationLogs)
            .Where(a => a.Status == AppointmentStatus.Scheduled &&
                        a.StartDateTime >= windowStart &&
                        a.StartDateTime <= windowEnd &&
                        !a.NotificationLogs.Any(nl => nl.NotificationType == NotificationType.Reminder24h &&
                                                      (nl.Status == NotificationStatus.Sent || nl.Status == NotificationStatus.Delivered)))
            .ToListAsync(ct);

        if (pendingAppointments.Count == 0) return;

        _logger.LogInformation("[Job 24h] Procesando {Count} recordatorios para citas de mañana.", pendingAppointments.Count);

        foreach (var appointment in pendingAppointments)
        {
            var magicUrl = $"https://clinicadental.com/cita/gestionar/{appointment.MagicToken}";
            var message = new NotificationMessage
            {
                RecipientName = appointment.Patient.FullName,
                RecipientContact = appointment.Patient.PhoneNumber,
                Channel = NotificationChannel.WhatsApp,
                Type = NotificationType.Reminder24h,
                Subject = "Recordatorio de Cita Odontológica para Mañana",
                BodyText = $"Hola {appointment.Patient.FirstName}, te recordamos tu cita de {appointment.ServiceCatalog.Name} mañana a las {appointment.StartDateTime:hh:mm tt} con Dr(a). {appointment.Dentist.User.LastName}. Por favor confirma o gestiona tu asistencia con un toque aquí: {magicUrl}",
                ActionUrl = magicUrl,
                ActionButtonText = "Confirmar Asistencia"
            };

            var sendResult = await notificationSender.SendAsync(message, ct);

            // Registrar en el log de auditoría y monitoreo
            var log = new NotificationLog
            {
                AppointmentId = appointment.Id,
                RecipientName = appointment.Patient.FullName,
                RecipientContact = appointment.Patient.PhoneNumber,
                Channel = NotificationChannel.WhatsApp,
                NotificationType = NotificationType.Reminder24h,
                Status = sendResult.Success ? NotificationStatus.Sent : NotificationStatus.Failed,
                ExternalMessageId = sendResult.ExternalMessageId,
                ErrorDetails = sendResult.ErrorMessage,
                MessageBody = message.BodyText,
                ScheduledFor = now,
                SentAt = sendResult.Success ? sendResult.SentAt : null
            };

            context.NotificationLogs.Add(log);
        }

        await context.SaveChangesAsync(ct);
    }
    #endregion

    #region Job 2: Recordatorio Express 2 Horas Antes
    private async Task RunJob2HourExpressRemindersAsync(
        ApplicationDbContext context,
        INotificationSender notificationSender,
        DateTime now,
        CancellationToken ct)
    {
        var windowStart = now.AddMinutes(105); // 1h 45m
        var windowEnd = now.AddMinutes(135);   // 2h 15m

        // Buscar citas confirmadas o agendadas en la ventana de las próximas 2 horas
        var upcomingAppointments = await context.Appointments
            .Include(a => a.Patient)
            .Include(a => a.DentalChair)
            .Include(a => a.ServiceCatalog)
            .Include(a => a.NotificationLogs)
            .Where(a => (a.Status == AppointmentStatus.Confirmed || a.Status == AppointmentStatus.Scheduled) &&
                        a.StartDateTime >= windowStart &&
                        a.StartDateTime <= windowEnd &&
                        !a.NotificationLogs.Any(nl => nl.NotificationType == NotificationType.Reminder2h &&
                                                      (nl.Status == NotificationStatus.Sent || nl.Status == NotificationStatus.Delivered)))
            .ToListAsync(ct);

        if (upcomingAppointments.Count == 0) return;

        _logger.LogInformation("[Job 2h Express] Enviando {Count} avisos express con ubicación en tiempo real.", upcomingAppointments.Count);

        const string googleMapsLocation = "https://maps.google.com/?q=Clinica+Odontologica+Especializada";

        foreach (var appointment in upcomingAppointments)
        {
            var message = new NotificationMessage
            {
                RecipientName = appointment.Patient.FullName,
                RecipientContact = appointment.Patient.PhoneNumber,
                Channel = NotificationChannel.WhatsApp,
                Type = NotificationType.Reminder2h,
                Subject = "Tu consulta dental comienza en 2 horas",
                BodyText = $"¡Hola {appointment.Patient.FirstName}! Tu cita para {appointment.ServiceCatalog.Name} es hoy a las {appointment.StartDateTime:hh:mm tt} en {appointment.DentalChair.Name}. Por favor llega 10 minutos antes. Cómo llegar en Google Maps: {googleMapsLocation}",
                ActionUrl = googleMapsLocation,
                ActionButtonText = "Abrir Google Maps"
            };

            var sendResult = await notificationSender.SendAsync(message, ct);

            context.NotificationLogs.Add(new NotificationLog
            {
                AppointmentId = appointment.Id,
                RecipientName = appointment.Patient.FullName,
                RecipientContact = appointment.Patient.PhoneNumber,
                Channel = NotificationChannel.WhatsApp,
                NotificationType = NotificationType.Reminder2h,
                Status = sendResult.Success ? NotificationStatus.Sent : NotificationStatus.Failed,
                ExternalMessageId = sendResult.ExternalMessageId,
                ErrorDetails = sendResult.ErrorMessage,
                MessageBody = message.BodyText,
                ScheduledFor = now,
                SentAt = sendResult.Success ? sendResult.SentAt : null
            });
        }

        await context.SaveChangesAsync(ct);
    }
    #endregion

    #region Job 3: Waitlist Auto-Filler y Cancelaciones
    private async Task RunJobWaitlistAutoFillerAsync(
        ApplicationDbContext context,
        INotificationSender notificationSender,
        DateTime now,
        CancellationToken ct)
    {
        // Paso A: Expirar ofertas de lista de espera que superaron el tiempo límite (30 min) sin ser reclamadas
        var expiredWaitlistEntries = await context.WaitlistEntries
            .Where(w => w.Status == WaitlistStatus.Notified && w.ClaimExpiresAt.HasValue && w.ClaimExpiresAt < now)
            .ToListAsync(ct);

        if (expiredWaitlistEntries.Count > 0)
        {
            foreach (var expired in expiredWaitlistEntries)
            {
                expired.Status = WaitlistStatus.Expired;
                _logger.LogInformation("[Waitlist] Entrada {Id} de {Name} ha expirado sin confirmación.", expired.Id, expired.PatientName);
            }
            await context.SaveChangesAsync(ct);
        }

        // Paso B: Detectar citas recientemente canceladas (en las últimas 2 horas) que tengan fecha futura hoy o mañana
        var recentCancellations = await context.Appointments
            .Include(a => a.ServiceCatalog)
            .Include(a => a.Dentist).ThenInclude(d => d.User)
            .Where(a => a.Status == AppointmentStatus.Cancelled &&
                        a.CancelledAt.HasValue &&
                        a.CancelledAt >= now.AddHours(-2) &&
                        a.StartDateTime > now)
            .ToListAsync(ct);

        if (recentCancellations.Count == 0) return;

        foreach (var cancelledAppointment in recentCancellations)
        {
            var cancelledDate = cancelledAppointment.StartDateTime.Date;
            var isMorning = cancelledAppointment.StartDateTime.Hour < 13;
            var targetRange = isMorning ? PreferredTimeRange.Morning : PreferredTimeRange.Afternoon;

            // Buscar el primer paciente en espera para esa fecha y rango horario (o Anytime)
            var waitlistCandidate = await context.WaitlistEntries
                .Where(w => w.Status == WaitlistStatus.Active &&
                            w.RequestedDate.Date == cancelledDate &&
                            (w.PreferredTimeRange == PreferredTimeRange.Anytime || w.PreferredTimeRange == targetRange) &&
                            (w.PreferredDentistId == null || w.PreferredDentistId == cancelledAppointment.DentistId))
                .OrderBy(w => w.CreatedAt) // FIFO: Primero en inscribirse, primero en recibir la oferta
                .FirstOrDefaultAsync(ct);

            if (waitlistCandidate == null) continue;

            // Ofrecer el cupo liberado al paciente
            var claimUrl = $"https://clinicadental.com/cita/waitlist/claim/{waitlistCandidate.ClaimToken}";
            var message = new NotificationMessage
            {
                RecipientName = waitlistCandidate.PatientName,
                RecipientContact = waitlistCandidate.PhoneNumber,
                Channel = NotificationChannel.WhatsApp,
                Type = NotificationType.WaitlistAlert,
                Subject = "¡Cupo disponible liberado en Clínica Dental!",
                BodyText = $"¡Hola {waitlistCandidate.PatientName}! Se acaba de liberar un espacio hoy a las {cancelledAppointment.StartDateTime:hh:mm tt} con Dr(a). {cancelledAppointment.Dentist.User.LastName}. Tienes 30 minutos para reservarlo antes de que pase al siguiente paciente: {claimUrl}",
                ActionUrl = claimUrl,
                ActionButtonText = "Reclamar Cupo Ahora"
            };

            var sendResult = await notificationSender.SendAsync(message, ct);

            // Actualizar estado de la entrada en la lista de espera
            waitlistCandidate.Status = WaitlistStatus.Notified;
            waitlistCandidate.NotifiedAt = now;
            waitlistCandidate.ClaimExpiresAt = now.AddMinutes(30);

            context.NotificationLogs.Add(new NotificationLog
            {
                WaitlistEntryId = waitlistCandidate.Id,
                RecipientName = waitlistCandidate.PatientName,
                RecipientContact = waitlistCandidate.PhoneNumber,
                Channel = NotificationChannel.WhatsApp,
                NotificationType = NotificationType.WaitlistAlert,
                Status = sendResult.Success ? NotificationStatus.Sent : NotificationStatus.Failed,
                ExternalMessageId = sendResult.ExternalMessageId,
                ErrorDetails = sendResult.ErrorMessage,
                MessageBody = message.BodyText,
                ScheduledFor = now,
                SentAt = sendResult.Success ? sendResult.SentAt : null
            });

            _logger.LogInformation(
                "[Waitlist Auto-Filler] Alerta enviada a {PatientName} ({Phone}) para el horario liberado {Time}",
                waitlistCandidate.PatientName, waitlistCandidate.PhoneNumber, cancelledAppointment.StartDateTime);
        }

        await context.SaveChangesAsync(ct);
    }
    #endregion
}
