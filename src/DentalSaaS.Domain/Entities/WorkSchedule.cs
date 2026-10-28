namespace DentalSaaS.Domain.Entities;

public class WorkSchedule
{
    public int Id { get; set; }
    public int DentistId { get; set; }
    public virtual DentistProfile Dentist { get; set; } = null!;

    public DayOfWeek DayOfWeek { get; set; }
    public TimeSpan StartTime { get; set; } = new TimeSpan(9, 0, 0);   // 09:00 AM
    public TimeSpan EndTime { get; set; } = new TimeSpan(18, 0, 0);    // 06:00 PM
    public bool HasLunchBreak { get; set; } = true;
    public TimeSpan? LunchBreakStart { get; set; } = new TimeSpan(13, 0, 0); // 01:00 PM
    public TimeSpan? LunchBreakEnd { get; set; } = new TimeSpan(14, 0, 0);   // 02:00 PM
    public bool IsWorkingDay { get; set; } = true;
}

public class ScheduleException
{
    public int Id { get; set; }
    public int DentistId { get; set; }
    public virtual DentistProfile Dentist { get; set; } = null!;

    public DateTime StartDateTime { get; set; }
    public DateTime EndDateTime { get; set; }
    public string Reason { get; set; } = string.Empty; // "Vacaciones", "Congreso", "Permiso Personal", etc.
    public bool IsFullDay { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
