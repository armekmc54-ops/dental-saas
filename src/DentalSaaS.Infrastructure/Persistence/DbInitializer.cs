using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using DentalSaaS.Domain.Entities;
using DentalSaaS.Domain.Enums;

namespace DentalSaaS.Infrastructure.Persistence;

public static class DbInitializer
{
    private static async Task ApplyPostgresPatchesAsync(ApplicationDbContext context)
    {
        if (context.Database.IsNpgsql())
        {
            var sql = """
                -- 1. Tablas de Identity que puedan faltar
                CREATE TABLE IF NOT EXISTS "AspNetUserClaims" (
                    "Id" SERIAL PRIMARY KEY,
                    "UserId" UUID NOT NULL REFERENCES "AspNetUsers"("Id") ON DELETE CASCADE,
                    "ClaimType" TEXT NULL,
                    "ClaimValue" TEXT NULL
                );
                CREATE TABLE IF NOT EXISTS "AspNetUserLogins" (
                    "LoginProvider" VARCHAR(128) NOT NULL,
                    "ProviderKey" VARCHAR(128) NOT NULL,
                    "ProviderDisplayName" TEXT NULL,
                    "UserId" UUID NOT NULL REFERENCES "AspNetUsers"("Id") ON DELETE CASCADE,
                    PRIMARY KEY ("LoginProvider", "ProviderKey")
                );
                CREATE TABLE IF NOT EXISTS "AspNetUserTokens" (
                    "UserId" UUID NOT NULL REFERENCES "AspNetUsers"("Id") ON DELETE CASCADE,
                    "LoginProvider" VARCHAR(128) NOT NULL,
                    "Name" VARCHAR(128) NOT NULL,
                    "Value" TEXT NULL,
                    PRIMARY KEY ("UserId", "LoginProvider", "Name")
                );
                CREATE TABLE IF NOT EXISTS "AspNetRoleClaims" (
                    "Id" SERIAL PRIMARY KEY,
                    "RoleId" UUID NOT NULL REFERENCES "AspNetRoles"("Id") ON DELETE CASCADE,
                    "ClaimType" TEXT NULL,
                    "ClaimValue" TEXT NULL
                );

                -- 2. Tabla de Notificaciones
                CREATE TABLE IF NOT EXISTS "NotificationLogs" (
                    "Id" SERIAL PRIMARY KEY,
                    "AppointmentId" INT NULL REFERENCES "Appointments"("Id") ON DELETE SET NULL,
                    "WaitlistEntryId" INT NULL,
                    "RecipientName" VARCHAR(150) NOT NULL,
                    "RecipientContact" VARCHAR(100) NOT NULL,
                    "Channel" INT NOT NULL DEFAULT 1,
                    "NotificationType" INT NOT NULL DEFAULT 1,
                    "Status" INT NOT NULL DEFAULT 1,
                    "MessageBody" TEXT NULL,
                    "ExternalMessageId" VARCHAR(150) NULL,
                    "ErrorDetails" TEXT NULL,
                    "RetryCount" INT NOT NULL DEFAULT 0,
                    "ScheduledFor" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                    "SentAt" TIMESTAMPTZ NULL,
                    "DeliveredAt" TIMESTAMPTZ NULL,
                    "ConfirmedAt" TIMESTAMPTZ NULL
                );

                -- 3. Parches de Columnas para Pacientes
                ALTER TABLE "Patients" ADD COLUMN IF NOT EXISTS "Address" TEXT NULL;
                ALTER TABLE "Patients" ADD COLUMN IF NOT EXISTS "EmergencyContactName" VARCHAR(150) NULL;
                ALTER TABLE "Patients" ADD COLUMN IF NOT EXISTS "EmergencyContactPhone" VARCHAR(30) NULL;
                ALTER TABLE "Patients" ADD COLUMN IF NOT EXISTS "GeneralNotes" TEXT NULL;

                -- 4. Parches de Alertas Médicas
                ALTER TABLE "MedicalAlerts" ADD COLUMN IF NOT EXISTS "ReportedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW();

                -- 5. Parches de Lista de Espera
                ALTER TABLE "WaitlistEntries" ADD COLUMN IF NOT EXISTS "PatientId" INT NULL REFERENCES "Patients"("Id") ON DELETE SET NULL;
                ALTER TABLE "WaitlistEntries" ADD COLUMN IF NOT EXISTS "NotifiedAt" TIMESTAMPTZ NULL;
                ALTER TABLE "WaitlistEntries" ADD COLUMN IF NOT EXISTS "ClaimExpiresAt" TIMESTAMPTZ NULL;

                -- 6. Parches de Odontograma
                ALTER TABLE "OdontogramRecords" ADD COLUMN IF NOT EXISTS "SnapshotTitle" VARCHAR(200) NOT NULL DEFAULT 'Estado Inicial al Ingreso';
                ALTER TABLE "OdontogramRecords" ADD COLUMN IF NOT EXISTS "IsCurrentActive" BOOLEAN NOT NULL DEFAULT TRUE;

                ALTER TABLE "ToothSurfaceStates" ADD COLUMN IF NOT EXISTS "ServiceCatalogId" INT NULL REFERENCES "ServiceCatalogs"("Id") ON DELETE SET NULL;
                ALTER TABLE "ToothSurfaceStates" ADD COLUMN IF NOT EXISTS "ClinicalNotes" TEXT NULL;
                ALTER TABLE "ToothSurfaceStates" ADD COLUMN IF NOT EXISTS "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW();
                ALTER TABLE "ToothSurfaceStates" ADD COLUMN IF NOT EXISTS "UpdatedAt" TIMESTAMPTZ NULL;

                -- 7. Parches de Usuarios, Dentistas y Citas
                ALTER TABLE "AspNetUsers" ADD COLUMN IF NOT EXISTS "LastLoginAt" TIMESTAMPTZ NULL;
                ALTER TABLE "DentistProfiles" ADD COLUMN IF NOT EXISTS "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW();
                ALTER TABLE "PaymentRecords" ADD COLUMN IF NOT EXISTS "InvoiceNumber" VARCHAR(50) NULL;
                ALTER TABLE "Appointments" ADD COLUMN IF NOT EXISTS "RowVersion" BYTEA NULL;
            """;
            await context.Database.ExecuteSqlRawAsync(sql);
        }
    }

    public static async Task SeedAsync(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager)
    {
        // 0. Aplicar parches de columnas e índices para compatibilidad total con PostgreSQL / Neon
        await ApplyPostgresPatchesAsync(context);

        // 1. Crear Roles si no existen
        string[] roles = [UserRoles.SuperAdmin, UserRoles.Dentist, UserRoles.Receptionist];
        foreach (var roleName in roles)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new ApplicationRole(roleName)
                {
                    Description = $"Rol del sistema: {roleName}"
                });
            }
        }

        // 2. Crear SuperAdmin si no existe
        const string adminEmail = "admin@dentalcare.com";
        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser == null)
        {
            adminUser = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                FirstName = "Carlos",
                LastName = "Montenegro",
                IsActive = true
            };

            var result = await userManager.CreateAsync(adminUser, "DentalAdmin2026!");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, UserRoles.SuperAdmin);
            }
        }

        // 3. Crear Dentista Ejemplo y su Horario
        const string dentistEmail = "dra.sofia@dentalcare.com";
        var dentistUser = await userManager.FindByEmailAsync(dentistEmail);
        if (dentistUser == null)
        {
            dentistUser = new ApplicationUser
            {
                UserName = dentistEmail,
                Email = dentistEmail,
                EmailConfirmed = true,
                FirstName = "Sofía",
                LastName = "García Navarro",
                IsActive = true
            };

            var result = await userManager.CreateAsync(dentistUser, "DentistPass2026!");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(dentistUser, UserRoles.Dentist);
            }
        }

        var profile = await context.DentistProfiles.FirstOrDefaultAsync(d => d.UserId == dentistUser.Id);
        if (profile == null)
        {
            profile = new DentistProfile
            {
                UserId = dentistUser.Id,
                Specialty = "Odontología Restauradora y Estética",
                LicenseNumber = "ODONT-84920",
                Biography = "Especialista en odontología conservadora y diseño de sonrisa con más de 10 años de experiencia clínica.",
                CalendarColorHex = "#2563EB",
                MaxDailyAppointments = 12,
                IsActive = true
            };

            // Horario de Lunes a Viernes 9:00 a 18:00 (almuerzo 13:00 - 14:00) y Sábado 9:00 a 13:00
            for (int day = 1; day <= 5; day++)
            {
                profile.WorkSchedules.Add(new WorkSchedule
                {
                    DayOfWeek = (DayOfWeek)day,
                    StartTime = new TimeSpan(9, 0, 0),
                    EndTime = new TimeSpan(18, 0, 0),
                    HasLunchBreak = true,
                    LunchBreakStart = new TimeSpan(13, 0, 0),
                    LunchBreakEnd = new TimeSpan(14, 0, 0),
                    IsWorkingDay = true
                });
            }
            profile.WorkSchedules.Add(new WorkSchedule
            {
                DayOfWeek = DayOfWeek.Saturday,
                StartTime = new TimeSpan(9, 0, 0),
                EndTime = new TimeSpan(13, 0, 0),
                HasLunchBreak = false,
                IsWorkingDay = true
            });

            context.DentistProfiles.Add(profile);
            await context.SaveChangesAsync();
        }

        // 4. Crear Recepcionista de Ejemplo
        const string recEmail = "recepcion@dentalcare.com";
        var recUser = await userManager.FindByEmailAsync(recEmail);
        if (recUser == null)
        {
            recUser = new ApplicationUser
            {
                UserName = recEmail,
                Email = recEmail,
                EmailConfirmed = true,
                FirstName = "Mariana",
                LastName = "Ríos",
                IsActive = true
            };

            var result = await userManager.CreateAsync(recUser, "RecepPass2026!");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(recUser, UserRoles.Receptionist);
            }
        }

        // 5. Crear Sillones Dentales si la tabla está vacía
        if (!await context.DentalChairs.AnyAsync())
        {
            context.DentalChairs.AddRange(
                new DentalChair
                {
                    Name = "Sillón 1 - Quirúrgico & RX",
                    RoomNumber = "Box A-101",
                    Description = "Equipado con sensor digital de Rayos X y micromotor endodóntico",
                    IsActive = true
                },
                new DentalChair
                {
                    Name = "Sillón 2 - Estética & General",
                    RoomNumber = "Box B-102",
                    Description = "Equipado con lámpara de polimerización LED de alta potencia y cámara intraoral",
                    IsActive = true
                },
                new DentalChair
                {
                    Name = "Sillón 3 - Prevención e Higiene",
                    RoomNumber = "Box C-103",
                    Description = "Equipado con cavitron ultrasónico de última generación y pulidor de bicarbonato",
                    IsActive = true
                }
            );
            await context.SaveChangesAsync();
        }

        // 6. Catálogo de Tratamientos y Servicios
        if (!await context.ServiceCatalogs.AnyAsync())
        {
            context.ServiceCatalogs.AddRange(
                new ServiceCatalog
                {
                    Name = "Valoración Inicial & Diagnóstico 3D",
                    Category = "Diagnóstico",
                    Description = "Evaluación clínica completa, toma de radiografías digitales periapicales y plan de tratamiento.",
                    DurationMinutes = 40,
                    BufferTimeMinutes = 10,
                    Price = 350.00m,
                    IconName = "stethoscope",
                    BadgeColor = "#2563EB",
                    IsActive = true
                },
                new ServiceCatalog
                {
                    Name = "Limpieza Ultrasónica & Profilaxis",
                    Category = "Higiene y Prevención",
                    Description = "Eliminación de sarro supragingival con cavitron, pulido coronal y aplicación de flúor.",
                    DurationMinutes = 45,
                    BufferTimeMinutes = 10,
                    Price = 650.00m,
                    IconName = "sparkles",
                    BadgeColor = "#059669",
                    IsActive = true
                },
                new ServiceCatalog
                {
                    Name = "Urgencia Dental / Alivio del Dolor",
                    Category = "Urgencias",
                    Description = "Atención prioritaria para dolor agudo, absceso o trauma dental con medicación y sedación pulpar.",
                    DurationMinutes = 30,
                    BufferTimeMinutes = 10,
                    Price = 500.00m,
                    IconName = "flame",
                    BadgeColor = "#DC2626",
                    IsActive = true
                },
                new ServiceCatalog
                {
                    Name = "Resina Estética Fotocurable",
                    Category = "Restauradora",
                    Description = "Obturación estética nano-híbrida de alta resistencia y mimetismo dental con aislamiento.",
                    DurationMinutes = 45,
                    BufferTimeMinutes = 15,
                    Price = 850.00m,
                    IconName = "shield-check",
                    BadgeColor = "#D97706",
                    IsActive = true
                },
                new ServiceCatalog
                {
                    Name = "Extracción Simple / Quirúrgica",
                    Category = "Cirugía",
                    Description = "Exodoncia bajo anestesia local atraumática, sutura reabsorbible y cuidados postoperatorios.",
                    DurationMinutes = 60,
                    BufferTimeMinutes = 15,
                    Price = 1200.00m,
                    IconName = "scissors",
                    BadgeColor = "#7C3AED",
                    IsActive = true
                },
                new ServiceCatalog
                {
                    Name = "Blanqueamiento Dental LED",
                    Category = "Estética",
                    Description = "Aclaramiento dental en consultorio con peróxido al 35% activado por luz LED en 3 sesiones.",
                    DurationMinutes = 60,
                    BufferTimeMinutes = 15,
                    Price = 2500.00m,
                    IconName = "sun",
                    BadgeColor = "#0D9488",
                    IsActive = true
                }
            );
            await context.SaveChangesAsync();
        }

        // 7. Pacientes de demostración con Folios oficiales y Citas activas
        if (!await context.Patients.AnyAsync())
        {
            var p1 = new Patient
            {
                FolioNumber = "FOLIO-00001",
                FirstName = "Sofía",
                LastName = "Valenzuela",
                Curp = "VAS920415MDFLN09",
                PhoneNumber = "+525511223344",
                Email = "sofia.valenzuela@ejemplo.com",
                DateOfBirth = new DateTime(1992, 4, 15),
                Gender = "F",
                RequiresInvoice = true,
                TaxId = "VASS920415AA1",
                LegalName = "SOFIA VALENZUELA SOTO",
                TaxRegime = "605 - Sueldos y Salarios",
                TaxZipCode = "06700",
                CfdiUsage = "D01"
            };
            p1.MedicalAlerts.Add(new MedicalAlert
            {
                AlertType = MedicalAlertType.Allergy,
                Description = "Alergia Severa a Penicilina y derivados beta-lactámicos",
                Severity = MedicalAlertSeverity.Critical,
                ActionProtocol = "PROHIBIDO prescribir Amoxicilina/Ampicilina. Usar Clindamicina 300mg o Azitromicina 500mg.",
                IsPermanent = true
            });

            var p2 = new Patient
            {
                FolioNumber = "FOLIO-00002",
                FirstName = "Roberto",
                LastName = "Mendoza",
                Curp = "MEMR850820HDFN01",
                PhoneNumber = "+525522334455",
                Email = "roberto.mendoza@ejemplo.com",
                DateOfBirth = new DateTime(1985, 8, 20),
                Gender = "M",
                RequiresInvoice = false
            };
            p2.MedicalAlerts.Add(new MedicalAlert
            {
                AlertType = MedicalAlertType.SystemicCondition,
                Description = "Hipertensión Arterial Grado 2 (Controlada con Losartán)",
                Severity = MedicalAlertSeverity.Medium,
                ActionProtocol = "Monitorear presión arterial previa. Anestesia local recomendada: Mepivacaína al 3% sin vasoconstrictor.",
                IsPermanent = true
            });

            var p3 = new Patient
            {
                FolioNumber = "FOLIO-00003",
                FirstName = "Mariana",
                LastName = "Castillo",
                Curp = "CAM981112MDFRR03",
                PhoneNumber = "+525533445566",
                Email = "mariana.c@ejemplo.com",
                DateOfBirth = new DateTime(1998, 11, 12),
                Gender = "F",
                RequiresInvoice = true,
                TaxId = "CAMM981112XX2",
                LegalName = "MARIANA CASTILLO RIVERA",
                TaxRegime = "612 - Personas Físicas con Actividades Empresariales",
                TaxZipCode = "03100"
            };

            context.Patients.AddRange(p1, p2, p3);
            await context.SaveChangesAsync();
        }

        // 8. Citas activas para demostración del tablero en vivo
        if (!await context.Appointments.AnyAsync())
        {
            var p1 = await context.Patients.FirstOrDefaultAsync(p => p.FolioNumber == "FOLIO-00001") ?? await context.Patients.FirstAsync();
            var p2 = await context.Patients.FirstOrDefaultAsync(p => p.FolioNumber == "FOLIO-00002") ?? await context.Patients.Skip(1).FirstAsync();
            var p3 = await context.Patients.FirstOrDefaultAsync(p => p.FolioNumber == "FOLIO-00003") ?? await context.Patients.Skip(2).FirstAsync();

            var dentist = await context.DentistProfiles.FirstAsync();
            var chairs = await context.DentalChairs.ToListAsync();
            var service1 = await context.ServiceCatalogs.FirstAsync();
            var service2 = await context.ServiceCatalogs.Skip(1).FirstAsync();
            var service3 = await context.ServiceCatalogs.Skip(2).FirstAsync();

            var now = DateTime.UtcNow;

            // Cita 1: En Sillón (atendiéndose ahora)
            context.Appointments.Add(new Appointment
            {
                AppointmentCode = "DEN-2026-0001",
                PatientId = p1.Id,
                DentistId = dentist.Id,
                DentalChairId = chairs[0].Id,
                ServiceCatalogId = service1.Id,
                StartDateTime = now.AddMinutes(-20),
                EndDateTime = now.AddMinutes(20),
                Status = AppointmentStatus.InChair,
                InChairCheckInAt = now.AddMinutes(-15),
                QuickAllergyAlertNote = "Alergia Severa a Penicilina",
                PainLevel = 3,
                ClinicalEvolutionNotes = "Paciente en tratamiento. Aislamiento absoluto colocado en cuadrante 1."
            });

            // Cita 2: En Sala de Espera (llegó y esperando)
            context.Appointments.Add(new Appointment
            {
                AppointmentCode = "DEN-2026-0002",
                PatientId = p2.Id,
                DentistId = dentist.Id,
                DentalChairId = chairs[1].Id,
                ServiceCatalogId = service2.Id,
                StartDateTime = now.AddMinutes(10),
                EndDateTime = now.AddMinutes(55),
                Status = AppointmentStatus.WaitingRoom,
                WaitingRoomCheckInAt = now.AddMinutes(-5),
                PainLevel = 0
            });

            // Cita 3: Siguiente en Turno (Confirmada)
            context.Appointments.Add(new Appointment
            {
                AppointmentCode = "DEN-2026-0003",
                PatientId = p3.Id,
                DentistId = dentist.Id,
                DentalChairId = chairs[0].Id,
                ServiceCatalogId = service3.Id,
                StartDateTime = now.AddMinutes(60),
                EndDateTime = now.AddMinutes(90),
                Status = AppointmentStatus.Confirmed,
                PainLevel = 8,
                IsEmergency = true,
                PatientBookingNotes = "Dolor agudo e inflamación en muela del juicio"
            });

            if (!await context.Prescriptions.AnyAsync())
            {
                // Receta activa de ejemplo para Mariana con recordatorio de medicamentos
                var presc = new Prescription
                {
                    PatientId = p3.Id,
                    DentistId = dentist.Id,
                    PrescriptionFolio = "REC-00001",
                    Diagnosis = "Pulpitis aguda reversible / Dolor postquirúrgico",
                    GeneralInstructions = "Tomar abundantes líquidos y evitar alimentos duros o calientes por 48 horas.",
                    CreatedAt = now.AddDays(-1)
                };
                presc.Items.Add(new PrescriptionItem
                {
                    MedicineName = "Ibuprofeno 600mg",
                    Dosage = "1 tableta cada 8 horas",
                    FrequencyHours = 8,
                    DurationDays = 4,
                    Instructions = "Tomar después de los alimentos. No exceder 3 tomas al día.",
                    StartDate = now.AddDays(-1)
                });
                presc.Items.Add(new PrescriptionItem
                {
                    MedicineName = "Clindamicina 300mg",
                    Dosage = "1 cápsula cada 6 horas",
                    FrequencyHours = 6,
                    DurationDays = 7,
                    Instructions = "Completar los 7 días obligatoriamente.",
                    StartDate = now.AddDays(-1)
                });
                context.Prescriptions.Add(presc);
            }

            await context.SaveChangesAsync();
        }
    }
}
