using DentalSaaS.Application.DTOs;

namespace DentalSaaS.Application.Interfaces;

public interface IAvailabilityService
{
    /// <summary>
    /// Calcula de forma matemática y en tiempo real todos los slots libres de un día determinado,
    /// considerando horario de odontólogo, descansos, excepciones/vacaciones, tiempo de desinfección (buffer)
    /// y capacidad de sillones dentales.
    /// </summary>
    Task<DailyAvailabilityResultDto> GetDailyAvailabilityAsync(SlotSearchRequest request, CancellationToken ct = default);

    /// <summary>
    /// Registra una cita pública de forma segura, garantizando bloqueo de concurrencia (evitando doble reserva),
    /// vinculación del paciente, generación de Magic Link seguro y registro de alertas médicas.
    /// </summary>
    Task<AppointmentBookingResult> BookAppointmentAsync(AppointmentBookingDto dto, CancellationToken ct = default);

    /// <summary>
    /// Permite a un paciente inscribirse en la lista de espera cuando no hay cupos para la fecha deseada.
    /// </summary>
    Task<bool> JoinWaitlistAsync(WaitlistJoinDto dto, CancellationToken ct = default);

    /// <summary>
    /// Bloquea un horario específico o el día completo para un dentista (reunión, cirugía privada, permiso).
    /// </summary>
    Task<ScheduleExceptionResultDto> BlockScheduleAsync(BlockScheduleRequestDto dto, CancellationToken ct = default);

    /// <summary>
    /// Libera un horario previamente bloqueado, abriendo nuevamente la disponibilidad en tiempo real.
    /// </summary>
    Task<bool> ReleaseScheduleExceptionAsync(int exceptionId, CancellationToken ct = default);

    /// <summary>
    /// Obtiene las excepciones y bloqueos de agenda de un dentista para una fecha específica.
    /// </summary>
    Task<List<ScheduleExceptionDto>> GetDentistExceptionsAsync(int dentistId, DateTime date, CancellationToken ct = default);
}
