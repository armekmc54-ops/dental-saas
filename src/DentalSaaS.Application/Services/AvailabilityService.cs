using Microsoft.EntityFrameworkCore;
using DentalSaaS.Application.DTOs;
using DentalSaaS.Application.Interfaces;
using DentalSaaS.Domain.Entities;
using DentalSaaS.Domain.Enums;

namespace DentalSaaS.Application.Services;

public class AvailabilityService : IAvailabilityService
{
    private readonly IApplicationDbContext _context;

    public AvailabilityService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<DailyAvailabilityResultDto> GetDailyAvailabilityAsync(
        SlotSearchRequest request,
        CancellationToken ct = default)
    {
        var targetDate = request.Date.Date;
        var localNow = DateTime.Now;
        var today = localNow.Date;

        var result = new DailyAvailabilityResultDto
        {
            Date = targetDate
        };

        // Si la fecha solicitada ya pasó (días anteriores), no hay disponibilidad
        if (targetDate < today)
        {
            return result;
        }

        // 1. Obtener servicio y calcular duración con buffer de desinfección
        var service = await _context.ServiceCatalogs
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.ServiceId && s.IsActive, ct);

        if (service == null)
        {
            return result;
        }

        int totalSlotDurationMinutes = service.DurationMinutes + service.BufferTimeMinutes;
        int gridStepMinutes = request.SlotIntervalMinutes > 0 ? request.SlotIntervalMinutes : 30;

        // 2. Obtener sillones dentales activos
        var activeChairs = await _context.DentalChairs
            .AsNoTracking()
            .Where(c => c.IsActive)
            .ToListAsync(ct);

        if (activeChairs.Count == 0)
        {
            return result;
        }

        // 3. Determinar qué odontólogos evaluar
        var dentistsQuery = _context.DentistProfiles
            .AsNoTracking()
            .Include(d => d.User)
            .Include(d => d.WorkSchedules)
            .Where(d => d.IsActive);

        if (request.PreferredDentistId.HasValue)
        {
            dentistsQuery = dentistsQuery.Where(d => d.Id == request.PreferredDentistId.Value);
        }

        var dentists = await dentistsQuery.ToListAsync(ct);
        if (dentists.Count == 0)
        {
            return result;
        }

        var dentistIds = dentists.Select(d => d.Id).ToList();

        // 4. Obtener excepciones / bloqueos / vacaciones de los doctores en esa fecha
        var dayStart = targetDate;
        var dayEnd = targetDate.AddDays(1);

        var scheduleExceptions = await _context.ScheduleExceptions
            .AsNoTracking()
            .Where(e => dentistIds.Contains(e.DentistId) && e.StartDateTime < dayEnd && e.EndDateTime > dayStart)
            .ToListAsync(ct);

        // 5. Obtener citas existentes activas en la clínica para ese día (para dentistas y sillones)
        var activeAppointments = await _context.Appointments
            .AsNoTracking()
            .Where(a => a.StartDateTime < dayEnd && a.EndDateTime > dayStart && a.Status != AppointmentStatus.Cancelled)
            .ToListAsync(ct);

        var targetDayOfWeek = targetDate.DayOfWeek;
        var now = DateTime.Now;

        // Diccionario para consolidar slots por horario de inicio (para asignación inteligente multi-doctor)
        var candidateSlotsMap = new Dictionary<DateTime, List<TimeSlotDto>>();

        foreach (var dentist in dentists)
        {
            // Validar si el doctor trabaja este día de la semana
            var schedule = dentist.WorkSchedules.FirstOrDefault(w => w.DayOfWeek == targetDayOfWeek && w.IsWorkingDay);
            if (schedule == null)
            {
                continue;
            }

            var workDayStart = targetDate.Add(schedule.StartTime);
            var workDayEnd = targetDate.Add(schedule.EndTime);

            // Intervalo de almuerzo / colación
            DateTime? lunchStart = schedule.HasLunchBreak && schedule.LunchBreakStart.HasValue
                ? targetDate.Add(schedule.LunchBreakStart.Value)
                : null;
            DateTime? lunchEnd = schedule.HasLunchBreak && schedule.LunchBreakEnd.HasValue
                ? targetDate.Add(schedule.LunchBreakEnd.Value)
                : null;

            // Iterar por la grilla de horarios disponibles
            var currentSlotStart = workDayStart;
            while (currentSlotStart.AddMinutes(totalSlotDurationMinutes) <= workDayEnd)
            {
                var currentSlotEnd = currentSlotStart.AddMinutes(totalSlotDurationMinutes);

                // Si es hoy, solo descartar si el horario ya concluyó hace más de 10 minutos
                if (targetDate == today && currentSlotEnd <= now.AddMinutes(-10))
                {
                    currentSlotStart = currentSlotStart.AddMinutes(gridStepMinutes);
                    continue;
                }

                // A. Validar solapamiento con descanso / almuerzo
                if (lunchStart.HasValue && lunchEnd.HasValue)
                {
                    if (currentSlotStart < lunchEnd.Value && currentSlotEnd > lunchStart.Value)
                    {
                        currentSlotStart = currentSlotStart.AddMinutes(gridStepMinutes);
                        continue;
                    }
                }

                // B. Validar excepciones / permisos del doctor
                bool hasExceptionOverlap = scheduleExceptions.Any(e =>
                    e.DentistId == dentist.Id &&
                    currentSlotStart < e.EndDateTime &&
                    currentSlotEnd > e.StartDateTime);

                if (hasExceptionOverlap)
                {
                    currentSlotStart = currentSlotStart.AddMinutes(gridStepMinutes);
                    continue;
                }

                // C. Validar citas activas del doctor
                bool hasDentistAppointmentOverlap = activeAppointments.Any(a =>
                    a.DentistId == dentist.Id &&
                    currentSlotStart < a.EndDateTime &&
                    currentSlotEnd > a.StartDateTime);

                if (hasDentistAppointmentOverlap)
                {
                    currentSlotStart = currentSlotStart.AddMinutes(gridStepMinutes);
                    continue;
                }

                // D. Validar disponibilidad física de sillones dentales
                var occupiedChairIds = activeAppointments
                    .Where(a => currentSlotStart < a.EndDateTime && currentSlotEnd > a.StartDateTime)
                    .Select(a => a.DentalChairId)
                    .ToHashSet();

                var freeChair = activeChairs.FirstOrDefault(c => !occupiedChairIds.Contains(c.Id));
                if (freeChair == null)
                {
                    // No hay sillón físico disponible en este intervalo
                    currentSlotStart = currentSlotStart.AddMinutes(gridStepMinutes);
                    continue;
                }

                // Slot válido encontrado para este doctor
                var slotDto = new TimeSlotDto
                {
                    StartDateTime = currentSlotStart,
                    EndDateTime = currentSlotStart.AddMinutes(service.DurationMinutes), // Hora de fin de consulta para el paciente
                    ServiceDurationMinutes = service.DurationMinutes,
                    BufferMinutes = service.BufferTimeMinutes,
                    DentistId = dentist.Id,
                    DentistName = $"Dr(a). {dentist.User.FullName}",
                    DentistSpecialty = dentist.Specialty,
                    DentistColorHex = dentist.CalendarColorHex,
                    DentalChairId = freeChair.Id,
                    DentalChairName = freeChair.Name
                };

                if (!candidateSlotsMap.ContainsKey(currentSlotStart))
                {
                    candidateSlotsMap[currentSlotStart] = new List<TimeSlotDto>();
                }
                candidateSlotsMap[currentSlotStart].Add(slotDto);

                currentSlotStart = currentSlotStart.AddMinutes(gridStepMinutes);
            }
        }

        // Si el paciente seleccionó "Cualquier doctor disponible", balanceamos la carga seleccionando
        // al doctor con menos citas ese día para ese slot
        var finalizedSlots = new List<TimeSlotDto>();

        foreach (var kvp in candidateSlotsMap.OrderBy(k => k.Key))
        {
            var optionsForThisTime = kvp.Value;
            if (optionsForThisTime.Count == 1)
            {
                finalizedSlots.Add(optionsForThisTime[0]);
            }
            else
            {
                // Balanceo inteligente: elegir el doctor que tenga menos citas agendadas hoy
                var selectedSlot = optionsForThisTime
                    .OrderBy(opt => activeAppointments.Count(a => a.DentistId == opt.DentistId))
                    .First();

                finalizedSlots.Add(selectedSlot);
            }
        }

        // Clasificar en Mañana y Tarde
        result.MorningSlots = finalizedSlots
            .Where(s => s.Period == PreferredTimeRange.Morning)
            .OrderBy(s => s.StartDateTime)
            .ToList();

        result.AfternoonSlots = finalizedSlots
            .Where(s => s.Period == PreferredTimeRange.Afternoon)
            .OrderBy(s => s.StartDateTime)
            .ToList();

        return result;
    }

    public async Task<AppointmentBookingResult> BookAppointmentAsync(
        AppointmentBookingDto dto,
        CancellationToken ct = default)
    {
        // 1. Validaciones básicas de entrada
        if (string.IsNullOrWhiteSpace(dto.FirstName) || string.IsNullOrWhiteSpace(dto.LastName))
        {
            return new AppointmentBookingResult { Success = false, ErrorMessage = "El nombre y apellido son obligatorios." };
        }

        if (string.IsNullOrWhiteSpace(dto.PhoneNumber))
        {
            return new AppointmentBookingResult { Success = false, ErrorMessage = "El teléfono WhatsApp es obligatorio para confirmaciones automáticas." };
        }

        if (dto.StartDateTime < DateTime.Now.Date)
        {
            return new AppointmentBookingResult { Success = false, ErrorMessage = "La fecha y hora de la cita no pueden pertenecer a días pasados." };
        }

        // 2. Obtener servicio
        var service = await _context.ServiceCatalogs
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == dto.ServiceId && s.IsActive, ct);

        if (service == null)
        {
            return new AppointmentBookingResult { Success = false, ErrorMessage = "El tratamiento dental seleccionado no está disponible." };
        }

        int totalSlotMinutes = service.DurationMinutes + service.BufferTimeMinutes;
        var slotStart = dto.StartDateTime;
        var slotEndWithBuffer = slotStart.AddMinutes(totalSlotMinutes);
        var effectiveEndDateTime = slotStart.AddMinutes(service.DurationMinutes);

        // 3. Resolver Doctor disponible
        int selectedDentistId;
        DentistProfile? selectedDentist;

        if (dto.PreferredDentistId.HasValue)
        {
            selectedDentist = await _context.DentistProfiles
                .Include(d => d.User)
                .FirstOrDefaultAsync(d => d.Id == dto.PreferredDentistId.Value && d.IsActive, ct);

            if (selectedDentist == null)
            {
                return new AppointmentBookingResult { Success = false, ErrorMessage = "El odontólogo seleccionado no está activo." };
            }
            selectedDentistId = selectedDentist.Id;
        }
        else
        {
            // Asignación inteligente: buscar doctores que atiendan a esa hora y no tengan solapamiento
            var availableDentists = await _context.DentistProfiles
                .Include(d => d.User)
                .Include(d => d.WorkSchedules)
                .Where(d => d.IsActive)
                .ToListAsync(ct);

            var validDentist = availableDentists.FirstOrDefault(d =>
            {
                var sched = d.WorkSchedules.FirstOrDefault(w => w.DayOfWeek == slotStart.DayOfWeek && w.IsWorkingDay);
                if (sched == null) return false;
                var schedStart = slotStart.Date.Add(sched.StartTime);
                var schedEnd = slotStart.Date.Add(sched.EndTime);
                if (slotStart < schedStart || slotEndWithBuffer > schedEnd) return false;

                if (sched.HasLunchBreak && sched.LunchBreakStart.HasValue && sched.LunchBreakEnd.HasValue)
                {
                    var lStart = slotStart.Date.Add(sched.LunchBreakStart.Value);
                    var lEnd = slotStart.Date.Add(sched.LunchBreakEnd.Value);
                    if (slotStart < lEnd && slotEndWithBuffer > lStart) return false;
                }
                return true;
            });

            if (validDentist == null)
            {
                return new AppointmentBookingResult { Success = false, ErrorMessage = "No hay ningún doctor disponible para atender en este horario." };
            }

            selectedDentist = validDentist;
            selectedDentistId = validDentist.Id;
        }

        // 4. REGLA DE CONCURRENCIA ESTRICTA: Validar que el doctor no tenga otra cita solapada
        bool doctorOverlap = await _context.Appointments.AnyAsync(a =>
            a.DentistId == selectedDentistId &&
            a.Status != AppointmentStatus.Cancelled &&
            slotStart < a.EndDateTime &&
            slotEndWithBuffer > a.StartDateTime, ct);

        if (doctorOverlap)
        {
            return new AppointmentBookingResult
            {
                Success = false,
                ErrorMessage = "El horario seleccionado acaba de ser reservado por otro paciente. Por favor selecciona otro horario."
            };
        }

        // 5. REGLA DE CONCURRENCIA ESTRICTA: Asignar un sillón dental libre
        var busyChairIds = await _context.Appointments
            .Where(a => a.Status != AppointmentStatus.Cancelled &&
                        slotStart < a.EndDateTime &&
                        slotEndWithBuffer > a.StartDateTime)
            .Select(a => a.DentalChairId)
            .ToListAsync(ct);

        var assignedChair = await _context.DentalChairs
            .Where(c => c.IsActive && !busyChairIds.Contains(c.Id))
            .FirstOrDefaultAsync(ct);

        if (assignedChair == null)
        {
            return new AppointmentBookingResult
            {
                Success = false,
                ErrorMessage = "Todos los sillones dentales de la clínica están ocupados en este momento. Por favor selecciona otro horario."
            };
        }

        // 6. Buscar o Registrar Paciente (sin requerir cuenta ni contraseña previa)
        var cleanPhone = dto.PhoneNumber.Trim().Replace(" ", "").Replace("-", "");
        var patient = await _context.Patients
            .Include(p => p.MedicalAlerts)
            .FirstOrDefaultAsync(p => p.PhoneNumber == cleanPhone, ct);

        if (patient == null)
        {
            var nextCount = await _context.Patients.CountAsync(ct) + 1;
            patient = new Patient
            {
                FolioNumber = $"FOLIO-{nextCount:D5}",
                FirstName = dto.FirstName.Trim(),
                LastName = dto.LastName.Trim(),
                Curp = dto.Curp?.Trim().ToUpper(),
                PhoneNumber = cleanPhone,
                Email = dto.Email?.Trim(),
                IdentificationNumber = dto.IdentificationNumber?.Trim(),
                DateOfBirth = dto.DateOfBirth,
                RequiresInvoice = dto.RequiresInvoice,
                TaxId = dto.TaxId?.Trim().ToUpper(),
                LegalName = dto.LegalName?.Trim(),
                TaxRegime = dto.TaxRegime?.Trim(),
                TaxZipCode = dto.TaxZipCode?.Trim(),
                CfdiUsage = dto.CfdiUsage?.Trim() ?? "D01",
                CreatedAt = DateTime.UtcNow
            };
            _context.Patients.Add(patient);
            await _context.SaveChangesAsync(ct);
        }
        else
        {
            // Actualizar datos de contacto si se proporcionaron nuevos
            if (!string.IsNullOrWhiteSpace(dto.Email)) patient.Email = dto.Email.Trim();
            if (!string.IsNullOrWhiteSpace(dto.Curp)) patient.Curp = dto.Curp.Trim().ToUpper();
            if (!string.IsNullOrWhiteSpace(dto.IdentificationNumber)) patient.IdentificationNumber = dto.IdentificationNumber.Trim();
            if (dto.DateOfBirth.HasValue) patient.DateOfBirth = dto.DateOfBirth;
            if (dto.RequiresInvoice)
            {
                patient.RequiresInvoice = true;
                if (!string.IsNullOrWhiteSpace(dto.TaxId)) patient.TaxId = dto.TaxId.Trim().ToUpper();
                if (!string.IsNullOrWhiteSpace(dto.LegalName)) patient.LegalName = dto.LegalName.Trim();
                if (!string.IsNullOrWhiteSpace(dto.TaxRegime)) patient.TaxRegime = dto.TaxRegime.Trim();
                if (!string.IsNullOrWhiteSpace(dto.TaxZipCode)) patient.TaxZipCode = dto.TaxZipCode.Trim();
            }
            patient.UpdatedAt = DateTime.UtcNow;
        }

        // 7. Alertas Médicas Rápidas (Semáforo médico)
        if (dto.HasMedicalAlertOrAllergy && !string.IsNullOrWhiteSpace(dto.MedicalAlertDescription))
        {
            var alreadyLogged = patient.MedicalAlerts.Any(m =>
                m.Description.Equals(dto.MedicalAlertDescription.Trim(), StringComparison.OrdinalIgnoreCase));

            if (!alreadyLogged)
            {
                _context.MedicalAlerts.Add(new MedicalAlert
                {
                    PatientId = patient.Id,
                    AlertType = MedicalAlertType.Allergy,
                    Description = dto.MedicalAlertDescription.Trim(),
                    Severity = MedicalAlertSeverity.Critical,
                    IsPermanent = true,
                    ReportedAt = DateTime.UtcNow
                });
            }
        }

        // 8. Generar Código y Magic Token de Autogestión
        var randomCode = Random.Shared.Next(1000, 9999);
        var appointmentCode = $"DEN-{DateTime.UtcNow:yyMM}-{randomCode}";
        var magicToken = Guid.NewGuid();

        var appointment = new Appointment
        {
            AppointmentCode = appointmentCode,
            PatientId = patient.Id,
            DentistId = selectedDentistId,
            DentalChairId = assignedChair.Id,
            ServiceCatalogId = service.Id,
            StartDateTime = slotStart,
            EndDateTime = slotEndWithBuffer,
            Status = AppointmentStatus.Scheduled,
            MagicToken = magicToken,
            MagicTokenExpiresAt = slotStart.AddDays(7),
            IsEmergency = dto.HasCurrentPain || service.Category.Contains("Urgencia"),
            PainLevel = dto.PainLevel,
            PatientBookingNotes = dto.BookingNotes,
            QuickAllergyAlertNote = dto.MedicalAlertDescription,
            CreatedAt = DateTime.UtcNow
        };

        _context.Appointments.Add(appointment);

        // 9. Registrar notificación en cola (Recordatorio de bienvenida y acceso al portal)
        _context.NotificationLogs.Add(new NotificationLog
        {
            Appointment = appointment,
            RecipientName = patient.FullName,
            RecipientContact = patient.PhoneNumber,
            Channel = NotificationChannel.WhatsApp,
            NotificationType = NotificationType.MagicLinkAccess,
            Status = NotificationStatus.Queued,
            ScheduledFor = DateTime.UtcNow,
            MessageBody = $"Hola {patient.FirstName}, tu cita para {service.Name} ha sido agendada con éxito para el {slotStart:dd/MM/yyyy HH:mm}. Gestiona o confirma tu asistencia aquí: /cita/gestionar/{magicToken}"
        });

        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new AppointmentBookingResult
            {
                Success = false,
                ErrorMessage = "Conflicto de concurrencia: el horario fue ocupado simultáneamente. Por favor selecciona otro intervalo."
            };
        }

        return new AppointmentBookingResult
        {
            Success = true,
            AppointmentId = appointment.Id,
            AppointmentCode = appointment.AppointmentCode,
            FolioNumber = patient.FolioNumber,
            MagicToken = magicToken,
            MagicManageUrl = $"/cita/gestionar/{magicToken}",
            StartDateTime = slotStart,
            EndDateTime = effectiveEndDateTime,
            DentistName = $"Dr(a). {selectedDentist.User.FullName}",
            DentalChairName = assignedChair.Name,
            ServiceName = service.Name,
            ServicePrice = service.Price
        };
    }

    public async Task<bool> JoinWaitlistAsync(WaitlistJoinDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.PatientName) || string.IsNullOrWhiteSpace(dto.PhoneNumber))
        {
            return false;
        }

        var entry = new WaitlistEntry
        {
            PatientName = dto.PatientName.Trim(),
            PhoneNumber = dto.PhoneNumber.Trim().Replace(" ", "").Replace("-", ""),
            Email = dto.Email?.Trim(),
            ServiceCatalogId = dto.ServiceId,
            PreferredDentistId = dto.PreferredDentistId,
            RequestedDate = dto.RequestedDate.Date,
            PreferredTimeRange = dto.PreferredTimeRange,
            Status = WaitlistStatus.Active,
            ClaimToken = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        };

        _context.WaitlistEntries.Add(entry);
        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<ScheduleExceptionResultDto> BlockScheduleAsync(
        BlockScheduleRequestDto dto,
        CancellationToken ct = default)
    {
        var dentist = await _context.DentistProfiles
            .Include(d => d.User)
            .FirstOrDefaultAsync(d => d.Id == dto.DentistId, ct);

        if (dentist == null)
        {
            return new ScheduleExceptionResultDto { Success = false, Message = "El odontólogo no fue encontrado." };
        }

        if (dto.EndDateTime <= dto.StartDateTime)
        {
            return new ScheduleExceptionResultDto { Success = false, Message = "La hora de fin debe ser posterior a la hora de inicio." };
        }

        var exception = new ScheduleException
        {
            DentistId = dto.DentistId,
            StartDateTime = dto.StartDateTime,
            EndDateTime = dto.EndDateTime,
            Reason = string.IsNullOrWhiteSpace(dto.Reason) ? "Bloqueo Clínico / Procedimiento Privado" : dto.Reason.Trim(),
            IsFullDay = dto.IsFullDay,
            CreatedAt = DateTime.UtcNow
        };

        _context.ScheduleExceptions.Add(exception);
        await _context.SaveChangesAsync(ct);

        return new ScheduleExceptionResultDto
        {
            Success = true,
            Message = $"Horario bloqueado con éxito para Dr(a). {dentist.User.FullName} ({dto.StartDateTime:hh:mm tt} - {dto.EndDateTime:hh:mm tt}).",
            ExceptionId = exception.Id,
            Exception = new ScheduleExceptionDto
            {
                Id = exception.Id,
                DentistId = dentist.Id,
                DentistName = $"Dr(a). {dentist.User.FullName}",
                StartDateTime = exception.StartDateTime,
                EndDateTime = exception.EndDateTime,
                Reason = exception.Reason,
                IsFullDay = exception.IsFullDay
            }
        };
    }

    public async Task<bool> ReleaseScheduleExceptionAsync(int exceptionId, CancellationToken ct = default)
    {
        var exception = await _context.ScheduleExceptions.FindAsync([exceptionId], ct);
        if (exception == null) return false;

        _context.ScheduleExceptions.Remove(exception);
        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<List<ScheduleExceptionDto>> GetDentistExceptionsAsync(
        int dentistId,
        DateTime date,
        CancellationToken ct = default)
    {
        var dayStart = date.Date;
        var dayEnd = dayStart.AddDays(1);

        return await _context.ScheduleExceptions
            .AsNoTracking()
            .Include(e => e.Dentist).ThenInclude(d => d.User)
            .Where(e => (dentistId == 0 || e.DentistId == dentistId) && e.StartDateTime < dayEnd && e.EndDateTime > dayStart)
            .OrderBy(e => e.StartDateTime)
            .Select(e => new ScheduleExceptionDto
            {
                Id = e.Id,
                DentistId = e.DentistId,
                DentistName = $"Dr(a). {e.Dentist.User.FullName}",
                StartDateTime = e.StartDateTime,
                EndDateTime = e.EndDateTime,
                Reason = e.Reason,
                IsFullDay = e.IsFullDay
            })
            .ToListAsync(ct);
    }
}
