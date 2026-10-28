namespace DentalSaaS.Domain.Entities;

public class DentalChair
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty; // Ej: "Sillón 1 - Quirófano", "Sillón 2 - Ortodoncia"
    public string RoomNumber { get; set; } = string.Empty; // Ej: "Consultorio A"
    public bool IsActive { get; set; } = true;
    public string? Description { get; set; }

    public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
}
