using System.Reflection;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using DentalSaaS.Application.Interfaces;
using DentalSaaS.Domain.Entities;
using DentalSaaS.Domain.Enums;

namespace DentalSaaS.Infrastructure.Persistence;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<DentistProfile> DentistProfiles => Set<DentistProfile>();
    public DbSet<DentalChair> DentalChairs => Set<DentalChair>();
    public DbSet<ServiceCatalog> ServiceCatalogs => Set<ServiceCatalog>();
    public DbSet<WorkSchedule> WorkSchedules => Set<WorkSchedule>();
    public DbSet<ScheduleException> ScheduleExceptions => Set<ScheduleException>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<MedicalAlert> MedicalAlerts => Set<MedicalAlert>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<WaitlistEntry> WaitlistEntries => Set<WaitlistEntry>();
    public DbSet<PaymentRecord> PaymentRecords => Set<PaymentRecord>();
    public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();
    public DbSet<OdontogramRecord> OdontogramRecords => Set<OdontogramRecord>();
    public DbSet<ToothSurfaceState> ToothSurfaceStates => Set<ToothSurfaceState>();
    public DbSet<PatientDocument> PatientDocuments => Set<PatientDocument>();
    public DbSet<Prescription> Prescriptions => Set<Prescription>();
    public DbSet<PrescriptionItem> PrescriptionItems => Set<PrescriptionItem>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // 1. Configuración de Usuario y Dentista
        builder.Entity<ApplicationUser>(b =>
        {
            b.Property(u => u.FirstName).HasMaxLength(100).IsRequired();
            b.Property(u => u.LastName).HasMaxLength(100).IsRequired();
            b.Property(u => u.AvatarUrl).HasMaxLength(500);

            b.HasOne(u => u.DentistProfile)
             .WithOne(d => d.User)
             .HasForeignKey<DentistProfile>(d => d.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<DentistProfile>(b =>
        {
            b.Property(d => d.Specialty).HasMaxLength(150).IsRequired();
            b.Property(d => d.LicenseNumber).HasMaxLength(80).IsRequired();
            b.Property(d => d.CalendarColorHex).HasMaxLength(16).HasDefaultValue("#3B82F6");
        });

        // 2. Configuración de Sillón Dental y Servicios
        builder.Entity<DentalChair>(b =>
        {
            b.Property(c => c.Name).HasMaxLength(100).IsRequired();
            b.Property(c => c.RoomNumber).HasMaxLength(50).IsRequired();
        });

        builder.Entity<ServiceCatalog>(b =>
        {
            b.Property(s => s.Name).HasMaxLength(150).IsRequired();
            b.Property(s => s.Category).HasMaxLength(100).IsRequired();
            b.Property(s => s.Price).HasPrecision(18, 2);
            b.Property(s => s.BadgeColor).HasMaxLength(16).HasDefaultValue("#10B981");
        });

        // 3. Horarios y Excepciones
        builder.Entity<WorkSchedule>(b =>
        {
            b.HasOne(w => w.Dentist)
             .WithMany(d => d.WorkSchedules)
             .HasForeignKey(w => w.DentistId)
             .OnDelete(DeleteBehavior.Cascade);

            b.HasIndex(w => new { w.DentistId, w.DayOfWeek }).IsUnique();
        });

        builder.Entity<ScheduleException>(b =>
        {
            b.Property(e => e.Reason).HasMaxLength(250).IsRequired();

            b.HasOne(e => e.Dentist)
             .WithMany(d => d.ScheduleExceptions)
             .HasForeignKey(e => e.DentistId)
             .OnDelete(DeleteBehavior.Cascade);

            b.HasIndex(e => new { e.DentistId, e.StartDateTime, e.EndDateTime });
        });

        // 4. Pacientes y Alertas Médicas
        builder.Entity<Patient>(b =>
        {
            b.Property(p => p.FolioNumber).HasMaxLength(30).IsRequired();
            b.Property(p => p.FirstName).HasMaxLength(100).IsRequired();
            b.Property(p => p.LastName).HasMaxLength(100).IsRequired();
            b.Property(p => p.Curp).HasMaxLength(18);
            b.Property(p => p.PhoneNumber).HasMaxLength(30).IsRequired();
            b.Property(p => p.Email).HasMaxLength(150);
            b.Property(p => p.TaxId).HasMaxLength(20);
            b.Property(p => p.LegalName).HasMaxLength(200);
            b.Property(p => p.TaxRegime).HasMaxLength(100);
            b.Property(p => p.TaxZipCode).HasMaxLength(10);
            b.Property(p => p.CfdiUsage).HasMaxLength(50);

            b.HasIndex(p => p.FolioNumber).IsUnique();
            b.HasIndex(p => p.Curp);
            b.HasIndex(p => p.PhoneNumber);
        });

        builder.Entity<PatientDocument>(b =>
        {
            b.Property(d => d.Title).HasMaxLength(200).IsRequired();
            b.Property(d => d.OriginalFileName).HasMaxLength(250).IsRequired();
            b.Property(d => d.StoredFileName).HasMaxLength(250).IsRequired();
            b.Property(d => d.RelativePath).HasMaxLength(500).IsRequired();

            b.HasOne(d => d.Patient)
             .WithMany(p => p.Documents)
             .HasForeignKey(d => d.PatientId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Prescription>(b =>
        {
            b.Property(pr => pr.PrescriptionFolio).HasMaxLength(30).IsRequired();
            b.Property(pr => pr.Diagnosis).HasMaxLength(300).IsRequired();

            b.HasOne(pr => pr.Patient)
             .WithMany(p => p.Prescriptions)
             .HasForeignKey(pr => pr.PatientId)
             .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(pr => pr.Dentist)
             .WithMany()
             .HasForeignKey(pr => pr.DentistId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PrescriptionItem>(b =>
        {
            b.Property(i => i.MedicineName).HasMaxLength(150).IsRequired();
            b.Property(i => i.Dosage).HasMaxLength(100).IsRequired();
            b.Property(i => i.Instructions).HasMaxLength(500).IsRequired();

            b.HasOne(i => i.Prescription)
             .WithMany(p => p.Items)
             .HasForeignKey(i => i.PrescriptionId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<MedicalAlert>(b =>
        {
            b.Property(m => m.Description).HasMaxLength(250).IsRequired();
            b.Property(m => m.ActionProtocol).HasMaxLength(500);

            b.HasOne(m => m.Patient)
             .WithMany(p => p.MedicalAlerts)
             .HasForeignKey(m => m.PatientId)
             .OnDelete(DeleteBehavior.Cascade);

            b.HasIndex(m => new { m.PatientId, m.Severity });
        });

        // 5. Citas (Appointments) - REGLAS DE NEGOCIO Y CONCURRENCIA
        builder.Entity<Appointment>(b =>
        {
            b.Property(a => a.AppointmentCode).HasMaxLength(32).IsRequired();
            b.Property(a => a.CancellationReason).HasMaxLength(300);
            b.Property(a => a.QuickAllergyAlertNote).HasMaxLength(300);
            b.Property(a => a.PatientBookingNotes).HasMaxLength(500);

            // RowVersion para bloqueo optimista de concurrencia al agendar
            b.Property(a => a.RowVersion).IsRowVersion();

            // Índice único para Magic Link del paciente (portal público)
            b.HasIndex(a => a.MagicToken).IsUnique();

            // Relaciones protegidas para no borrar historial médico por error
            b.HasOne(a => a.Patient)
             .WithMany(p => p.Appointments)
             .HasForeignKey(a => a.PatientId)
             .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(a => a.Dentist)
             .WithMany(d => d.Appointments)
             .HasForeignKey(a => a.DentistId)
             .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(a => a.DentalChair)
             .WithMany(c => c.Appointments)
             .HasForeignKey(a => a.DentalChairId)
             .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(a => a.ServiceCatalog)
             .WithMany(s => s.Appointments)
             .HasForeignKey(a => a.ServiceCatalogId)
             .OnDelete(DeleteBehavior.Restrict);

            // Índice compuesto crítico para búsqueda de solapamiento y prevención de doble reserva
            b.HasIndex(a => new { a.DentistId, a.StartDateTime, a.EndDateTime, a.Status })
             .HasDatabaseName("IX_Appointments_Dentist_Slot_Active");

            b.HasIndex(a => new { a.DentalChairId, a.StartDateTime, a.EndDateTime, a.Status })
             .HasDatabaseName("IX_Appointments_Chair_Slot_Active");

            b.HasIndex(a => new { a.StartDateTime, a.Status })
             .HasDatabaseName("IX_Appointments_Date_Status");
        });

        // 6. Lista de Espera (Waitlist)
        builder.Entity<WaitlistEntry>(b =>
        {
            b.Property(w => w.PatientName).HasMaxLength(150).IsRequired();
            b.Property(w => w.PhoneNumber).HasMaxLength(30).IsRequired();
            b.Property(w => w.Email).HasMaxLength(150);

            b.HasOne(w => w.Patient)
             .WithMany(p => p.WaitlistEntries)
             .HasForeignKey(w => w.PatientId)
             .OnDelete(DeleteBehavior.SetNull);

            b.HasOne(w => w.PreferredDentist)
             .WithMany()
             .HasForeignKey(w => w.PreferredDentistId)
             .OnDelete(DeleteBehavior.SetNull);

            b.HasOne(w => w.ServiceCatalog)
             .WithMany()
             .HasForeignKey(w => w.ServiceCatalogId)
             .OnDelete(DeleteBehavior.Restrict);

            b.HasIndex(w => new { w.RequestedDate, w.Status });
            b.HasIndex(w => w.ClaimToken).IsUnique();
        });

        // 7. Pagos y Notificaciones
        builder.Entity<PaymentRecord>(b =>
        {
            b.Property(p => p.Amount).HasPrecision(18, 2);
            b.Property(p => p.TransactionReference).HasMaxLength(100);
            b.Property(p => p.InvoiceNumber).HasMaxLength(50);

            b.HasOne(p => p.Appointment)
             .WithMany(a => a.PaymentRecords)
             .HasForeignKey(p => p.AppointmentId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<NotificationLog>(b =>
        {
            b.Property(n => n.RecipientName).HasMaxLength(150).IsRequired();
            b.Property(n => n.RecipientContact).HasMaxLength(150).IsRequired();
            b.Property(n => n.ExternalMessageId).HasMaxLength(100);
            b.Property(n => n.ErrorDetails).HasMaxLength(1000);

            b.HasOne(n => n.Appointment)
             .WithMany(a => a.NotificationLogs)
             .HasForeignKey(n => n.AppointmentId)
             .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(n => n.WaitlistEntry)
             .WithMany(w => w.NotificationLogs)
             .HasForeignKey(n => n.WaitlistEntryId)
             .OnDelete(DeleteBehavior.Cascade);

            b.HasIndex(n => new { n.Status, n.ScheduledFor })
             .HasDatabaseName("IX_NotificationLogs_Queue_Processing");
        });

        // 8. Odontograma y Superficies Dentales (FDI 11..85)
        builder.Entity<OdontogramRecord>(b =>
        {
            b.Property(o => o.SnapshotTitle).HasMaxLength(150).IsRequired();
            b.Property(o => o.TotalEstimatedCost).HasPrecision(18, 2);

            b.HasOne(o => o.Patient)
             .WithMany(p => p.OdontogramRecords)
             .HasForeignKey(o => o.PatientId)
             .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(o => o.Dentist)
             .WithMany(d => d.OdontogramRecords)
             .HasForeignKey(o => o.DentistId)
             .OnDelete(DeleteBehavior.Restrict);

            b.HasIndex(o => new { o.PatientId, o.IsCurrentActive });
        });

        builder.Entity<ToothSurfaceState>(b =>
        {
            b.Property(t => t.EstimatedPrice).HasPrecision(18, 2);
            b.Property(t => t.ClinicalNotes).HasMaxLength(500);

            b.HasOne(t => t.OdontogramRecord)
             .WithMany(o => o.ToothSurfaces)
             .HasForeignKey(t => t.OdontogramRecordId)
             .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(t => t.ServiceCatalog)
             .WithMany(s => s.AssociatedToothSurfaces)
             .HasForeignKey(t => t.ServiceCatalogId)
             .OnDelete(DeleteBehavior.SetNull);

            // Búsqueda rápida por pieza y superficie
            b.HasIndex(t => new { t.OdontogramRecordId, t.ToothNumber, t.Surface })
             .HasDatabaseName("IX_ToothSurfaceState_Record_Tooth_Surface");
        });
    }
}
