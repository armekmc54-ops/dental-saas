using Microsoft.EntityFrameworkCore;
using DentalSaaS.Domain.Entities;

namespace DentalSaaS.Application.Interfaces;

public interface IApplicationDbContext
{
    DbSet<ApplicationUser> Users { get; }
    DbSet<ApplicationRole> Roles { get; }
    DbSet<DentistProfile> DentistProfiles { get; }
    DbSet<DentalChair> DentalChairs { get; }
    DbSet<ServiceCatalog> ServiceCatalogs { get; }
    DbSet<WorkSchedule> WorkSchedules { get; }
    DbSet<ScheduleException> ScheduleExceptions { get; }
    DbSet<Patient> Patients { get; }
    DbSet<MedicalAlert> MedicalAlerts { get; }
    DbSet<Appointment> Appointments { get; }
    DbSet<WaitlistEntry> WaitlistEntries { get; }
    DbSet<PaymentRecord> PaymentRecords { get; }
    DbSet<NotificationLog> NotificationLogs { get; }
    DbSet<OdontogramRecord> OdontogramRecords { get; }
    DbSet<ToothSurfaceState> ToothSurfaceStates { get; }
    DbSet<PatientDocument> PatientDocuments { get; }
    DbSet<Prescription> Prescriptions { get; }
    DbSet<PrescriptionItem> PrescriptionItems { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
