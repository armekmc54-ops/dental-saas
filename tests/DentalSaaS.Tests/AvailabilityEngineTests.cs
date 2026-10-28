using Microsoft.EntityFrameworkCore;
using DentalSaaS.Application.DTOs;
using DentalSaaS.Application.Services;
using DentalSaaS.Domain.Entities;
using DentalSaaS.Domain.Enums;
using DentalSaaS.Infrastructure.Persistence;
using Xunit;

namespace DentalSaaS.Tests;

public class AvailabilityEngineTests
{
    private ApplicationDbContext GetInMemoryDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        var context = new ApplicationDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    [Fact]
    public async Task GetDailyAvailability_ShouldReturnSlots_ExcludingLunchBreakAndRespectingBuffer()
    {
        // Arrange
        var context = GetInMemoryDbContext("TestDb_Availability_Lunch");

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            FirstName = "Roberto",
            LastName = "Gómez",
            Email = "dr.gomez@dental.com"
        };
        context.Users.Add(user);

        var dentist = new DentistProfile
        {
            Id = 1,
            UserId = user.Id,
            Specialty = "General",
            LicenseNumber = "D-1234",
            IsActive = true
        };

        // Trabaja los lunes de 9:00 a 14:00 con almuerzo de 12:00 a 13:00
        dentist.WorkSchedules.Add(new WorkSchedule
        {
            DentistId = 1,
            DayOfWeek = DayOfWeek.Monday,
            StartTime = new TimeSpan(9, 0, 0),
            EndTime = new TimeSpan(14, 0, 0),
            HasLunchBreak = true,
            LunchBreakStart = new TimeSpan(12, 0, 0),
            LunchBreakEnd = new TimeSpan(13, 0, 0),
            IsWorkingDay = true
        });
        context.DentistProfiles.Add(dentist);

        var chair = new DentalChair
        {
            Id = 1,
            Name = "Sillón 1",
            RoomNumber = "Box 1",
            IsActive = true
        };
        context.DentalChairs.Add(chair);

        // Servicio: 50 min de duración + 10 min de buffer = 60 min total por slot
        var service = new ServiceCatalog
        {
            Id = 1,
            Name = "Limpieza Profunda",
            DurationMinutes = 50,
            BufferTimeMinutes = 10,
            Price = 60.00m,
            IsActive = true
        };
        context.ServiceCatalogs.Add(service);
        await context.SaveChangesAsync();

        var serviceEngine = new AvailabilityService(context);

        // Próximo Lunes futuro garantizado
        var targetMonday = DateTime.UtcNow.Date.AddDays(7);
        while (targetMonday.DayOfWeek != DayOfWeek.Monday)
        {
            targetMonday = targetMonday.AddDays(1);
        }

        var request = new SlotSearchRequest
        {
            Date = targetMonday,
            ServiceId = 1,
            PreferredDentistId = 1,
            SlotIntervalMinutes = 60 // Horas en punto
        };

        // Act
        var availability = await serviceEngine.GetDailyAvailabilityAsync(request);

        // Assert:
        // Horas posibles en punto:
        // 09:00 - 10:00 (Válido)
        // 10:00 - 11:00 (Válido)
        // 11:00 - 12:00 (Válido)
        // 12:00 - 13:00 (Almuerzo -> Excluido)
        // 13:00 - 14:00 (Válido)
        // Total slots esperados = 4
        Assert.NotNull(availability);
        Assert.Equal(4, availability.TotalAvailableSlots);
        Assert.DoesNotContain(availability.MorningSlots, s => s.StartDateTime.Hour == 12);
        Assert.Contains(availability.MorningSlots, s => s.StartDateTime.Hour == 9);
        Assert.Contains(availability.MorningSlots, s => s.StartDateTime.Hour == 10);
        Assert.Contains(availability.MorningSlots, s => s.StartDateTime.Hour == 11);
        Assert.Contains(availability.AfternoonSlots, s => s.StartDateTime.Hour == 13);
    }

    [Fact]
    public async Task BookAppointment_ShouldCreatePatient_GenerateMagicToken_AndPreventDuplicateBooking()
    {
        // Arrange
        var context = GetInMemoryDbContext("TestDb_Booking_MagicToken");

        var user = new ApplicationUser { Id = Guid.NewGuid(), FirstName = "Elena", LastName = "Vásquez" };
        context.Users.Add(user);

        var dentist = new DentistProfile
        {
            Id = 10,
            UserId = user.Id,
            Specialty = "Ortodoncia",
            LicenseNumber = "ORT-99",
            IsActive = true
        };
        dentist.WorkSchedules.Add(new WorkSchedule
        {
            DentistId = 10,
            DayOfWeek = DayOfWeek.Wednesday,
            StartTime = new TimeSpan(9, 0, 0),
            EndTime = new TimeSpan(18, 0, 0),
            IsWorkingDay = true
        });
        context.DentistProfiles.Add(dentist);

        var chair = new DentalChair { Id = 5, Name = "Sillón Ortodoncia", RoomNumber = "Box 5", IsActive = true };
        context.DentalChairs.Add(chair);

        var service = new ServiceCatalog
        {
            Id = 100,
            Name = "Ajuste de Brackets",
            DurationMinutes = 30,
            BufferTimeMinutes = 10,
            Price = 45.00m,
            IsActive = true
        };
        context.ServiceCatalogs.Add(service);
        await context.SaveChangesAsync();

        var serviceEngine = new AvailabilityService(context);

        var targetWednesday = DateTime.UtcNow.Date.AddDays(7);
        while (targetWednesday.DayOfWeek != DayOfWeek.Wednesday)
        {
            targetWednesday = targetWednesday.AddDays(1);
        }
        var bookingSlot = targetWednesday.AddHours(10); // 10:00 AM

        var bookingDto = new AppointmentBookingDto
        {
            ServiceId = 100,
            PreferredDentistId = 10,
            StartDateTime = bookingSlot,
            FirstName = "Juan",
            LastName = "Pérez",
            PhoneNumber = "+525511223344",
            Email = "juan.perez@example.com",
            HasMedicalAlertOrAllergy = true,
            MedicalAlertDescription = "Alergia a la penicilina",
            HasCurrentPain = true,
            PainLevel = 7
        };

        // Act 1: Agendar primera vez
        var result1 = await serviceEngine.BookAppointmentAsync(bookingDto);

        // Assert 1:
        Assert.True(result1.Success);
        Assert.NotNull(result1.AppointmentCode);
        Assert.NotNull(result1.MagicToken);
        Assert.StartsWith("/cita/gestionar/", result1.MagicManageUrl);

        // Verificar paciente creado con alerta médica en base de datos
        var savedPatient = await context.Patients.Include(p => p.MedicalAlerts).FirstOrDefaultAsync(p => p.PhoneNumber == "+525511223344");
        Assert.NotNull(savedPatient);
        Assert.Equal("Juan", savedPatient.FirstName);
        Assert.Single(savedPatient.MedicalAlerts);
        Assert.Equal("Alergia a la penicilina", savedPatient.MedicalAlerts.First().Description);

        // Verificar cita creada con status Scheduled y flags de dolor
        var savedAppointment = await context.Appointments.FindAsync(result1.AppointmentId);
        Assert.NotNull(savedAppointment);
        Assert.Equal(AppointmentStatus.Scheduled, savedAppointment.Status);
        Assert.True(savedAppointment.IsEmergency);
        Assert.Equal(7, savedAppointment.PainLevel);

        // Act 2: Intento de reserva simultánea en el mismo horario (Carrera / Doble Reserva)
        var bookingDtoConflict = new AppointmentBookingDto
        {
            ServiceId = 100,
            PreferredDentistId = 10,
            StartDateTime = bookingSlot, // Mismo horario
            FirstName = "María",
            LastName = "López",
            PhoneNumber = "+525599887766"
        };
        var result2 = await serviceEngine.BookAppointmentAsync(bookingDtoConflict);

        // Assert 2: Debe ser rechazado para evitar doble reserva
        Assert.False(result2.Success);
        Assert.Contains("acaba de ser reservado", result2.ErrorMessage);
    }

    [Fact]
    public async Task JoinWaitlist_ShouldRegisterEntrySuccessfully()
    {
        // Arrange
        var context = GetInMemoryDbContext("TestDb_Waitlist");
        var serviceEngine = new AvailabilityService(context);

        var waitlistDto = new WaitlistJoinDto
        {
            PatientName = "Camila Silva",
            PhoneNumber = "+5491133445566",
            Email = "camila@example.com",
            ServiceId = 1,
            RequestedDate = DateTime.UtcNow.Date.AddDays(2),
            PreferredTimeRange = PreferredTimeRange.Morning
        };

        // Act
        var success = await serviceEngine.JoinWaitlistAsync(waitlistDto);

        // Assert
        Assert.True(success);
        var entry = await context.WaitlistEntries.FirstOrDefaultAsync(w => w.PhoneNumber == "+5491133445566");
        Assert.NotNull(entry);
        Assert.Equal(WaitlistStatus.Active, entry.Status);
        Assert.NotEqual(Guid.Empty, entry.ClaimToken);
    }
}
