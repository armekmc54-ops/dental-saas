namespace DentalSaaS.Domain.Entities;

public class DentistProfile
{
    public int Id { get; set; }
    public Guid UserId { get; set; }
    public virtual ApplicationUser User { get; set; } = null!;

    public string Specialty { get; set; } = "Odontología General";
    public string LicenseNumber { get; set; } = string.Empty; // Cédula / Colegiatura
    public string? Biography { get; set; }
    public string CalendarColorHex { get; set; } = "#3B82F6"; // Color distintivo para el calendario
    public int MaxDailyAppointments { get; set; } = 16;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Relaciones
    public virtual ICollection<WorkSchedule> WorkSchedules { get; set; } = new List<WorkSchedule>();
    public virtual ICollection<ScheduleException> ScheduleExceptions { get; set; } = new List<ScheduleException>();
    public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
    public virtual ICollection<OdontogramRecord> OdontogramRecords { get; set; } = new List<OdontogramRecord>();
}
