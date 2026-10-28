namespace DentalSaaS.Domain.Entities;

public class ServiceCatalog
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty; // Ej: "Valoración Inicial", "Limpieza ultrasónica"
    public string? Description { get; set; }
    public string Category { get; set; } = "General"; // Prevención, Restauradora, Ortodoncia, Urgencia, etc.
    public int DurationMinutes { get; set; } = 30; // Tiempo efectivo de consulta
    public int BufferTimeMinutes { get; set; } = 10; // Tiempo de desinfección / preparación del sillón
    public decimal Price { get; set; }
    public bool IsActive { get; set; } = true;
    public string? IconName { get; set; } = "tooth";
    public string? BadgeColor { get; set; } = "#10B981";

    public int TotalSlotDurationMinutes => DurationMinutes + BufferTimeMinutes;

    public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
    public virtual ICollection<ToothSurfaceState> AssociatedToothSurfaces { get; set; } = new List<ToothSurfaceState>();
}
