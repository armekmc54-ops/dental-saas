-- =============================================================================
-- DENTALCARE SAAS - ESQUEMA DE BASE DE DATOS COMPLETO (POSTGRESQL / SUPABASE)
-- Sistema Integral de Gestión de Citas y Clínica Odontológica con Odontograma FDI
-- =============================================================================

CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- 1. ROLES Y SEGURIDAD (ASP.NET Core Identity)
CREATE TABLE IF NOT EXISTS "AspNetRoles" (
    "Id" UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    "Name" VARCHAR(256) NULL,
    "NormalizedName" VARCHAR(256) NULL,
    "ConcurrencyStamp" TEXT NULL,
    "Description" TEXT NULL
);

CREATE TABLE IF NOT EXISTS "AspNetUsers" (
    "Id" UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    "UserName" VARCHAR(256) NULL,
    "NormalizedUserName" VARCHAR(256) NULL,
    "Email" VARCHAR(256) NULL,
    "NormalizedEmail" VARCHAR(256) NULL,
    "EmailConfirmed" BOOLEAN NOT NULL DEFAULT FALSE,
    "PasswordHash" TEXT NULL,
    "SecurityStamp" TEXT NULL,
    "ConcurrencyStamp" TEXT NULL,
    "PhoneNumber" TEXT NULL,
    "PhoneNumberConfirmed" BOOLEAN NOT NULL DEFAULT FALSE,
    "TwoFactorEnabled" BOOLEAN NOT NULL DEFAULT FALSE,
    "LockoutEnd" TIMESTAMPTZ NULL,
    "LockoutEnabled" BOOLEAN NOT NULL DEFAULT FALSE,
    "AccessFailedCount" INT NOT NULL DEFAULT 0,
    "FirstName" VARCHAR(100) NOT NULL,
    "LastName" VARCHAR(100) NOT NULL,
    "AvatarUrl" VARCHAR(500) NULL,
    "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "LastLoginAt" TIMESTAMPTZ NULL,
    "IsActive" BOOLEAN NOT NULL DEFAULT TRUE
);

CREATE TABLE IF NOT EXISTS "AspNetUserRoles" (
    "UserId" UUID NOT NULL REFERENCES "AspNetUsers"("Id") ON DELETE CASCADE,
    "RoleId" UUID NOT NULL REFERENCES "AspNetRoles"("Id") ON DELETE CASCADE,
    PRIMARY KEY ("UserId", "RoleId")
);

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

-- 2. INFRAESTRUCTURA CLÍNICA (Dentistas, Sillones, Servicios y Horarios)
CREATE TABLE IF NOT EXISTS "DentistProfiles" (
    "Id" SERIAL PRIMARY KEY,
    "UserId" UUID NOT NULL UNIQUE REFERENCES "AspNetUsers"("Id") ON DELETE CASCADE,
    "Specialty" VARCHAR(150) NOT NULL,
    "LicenseNumber" VARCHAR(80) NOT NULL,
    "Biography" TEXT NULL,
    "CalendarColorHex" VARCHAR(20) NOT NULL DEFAULT '#2563EB',
    "MaxDailyAppointments" INT NOT NULL DEFAULT 12,
    "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "IsActive" BOOLEAN NOT NULL DEFAULT TRUE
);

CREATE TABLE IF NOT EXISTS "DentalChairs" (
    "Id" SERIAL PRIMARY KEY,
    "Name" VARCHAR(100) NOT NULL,
    "RoomNumber" VARCHAR(50) NOT NULL,
    "Description" TEXT NULL,
    "IsActive" BOOLEAN NOT NULL DEFAULT TRUE
);

CREATE TABLE IF NOT EXISTS "ServiceCatalogs" (
    "Id" SERIAL PRIMARY KEY,
    "Name" VARCHAR(150) NOT NULL,
    "Category" VARCHAR(80) NOT NULL DEFAULT 'General',
    "Description" TEXT NULL,
    "DurationMinutes" INT NOT NULL DEFAULT 30,
    "BufferTimeMinutes" INT NOT NULL DEFAULT 10,
    "Price" NUMERIC(10,2) NOT NULL DEFAULT 0.00,
    "IsActive" BOOLEAN NOT NULL DEFAULT TRUE,
    "IconName" VARCHAR(50) DEFAULT 'tooth',
    "BadgeColor" VARCHAR(20) DEFAULT '#10B981'
);

CREATE TABLE IF NOT EXISTS "WorkSchedules" (
    "Id" SERIAL PRIMARY KEY,
    "DentistId" INT NOT NULL REFERENCES "DentistProfiles"("Id") ON DELETE CASCADE,
    "DayOfWeek" INT NOT NULL, -- 0=Domingo, 1=Lunes ... 6=Sabado
    "StartTime" TIME NOT NULL DEFAULT '09:00:00',
    "EndTime" TIME NOT NULL DEFAULT '18:00:00',
    "HasLunchBreak" BOOLEAN NOT NULL DEFAULT TRUE,
    "LunchBreakStart" TIME NULL DEFAULT '13:00:00',
    "LunchBreakEnd" TIME NULL DEFAULT '14:00:00',
    "IsWorkingDay" BOOLEAN NOT NULL DEFAULT TRUE
);

CREATE TABLE IF NOT EXISTS "ScheduleExceptions" (
    "Id" SERIAL PRIMARY KEY,
    "DentistId" INT NOT NULL REFERENCES "DentistProfiles"("Id") ON DELETE CASCADE,
    "StartDateTime" TIMESTAMPTZ NOT NULL,
    "EndDateTime" TIMESTAMPTZ NOT NULL,
    "Reason" VARCHAR(250) NOT NULL,
    "IsFullDay" BOOLEAN NOT NULL DEFAULT FALSE,
    "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- 3. PACIENTES Y EXPEDIENTES POR FOLIO
CREATE TABLE IF NOT EXISTS "Patients" (
    "Id" SERIAL PRIMARY KEY,
    "FolioNumber" VARCHAR(30) NOT NULL UNIQUE, -- FOLIO-00001
    "FirstName" VARCHAR(100) NOT NULL,
    "LastName" VARCHAR(100) NOT NULL,
    "Curp" VARCHAR(18) NULL,
    "IdentificationNumber" VARCHAR(50) NULL,
    "PhoneNumber" VARCHAR(30) NOT NULL,
    "Email" VARCHAR(150) NULL,
    "DateOfBirth" DATE NULL,
    "Gender" VARCHAR(10) NULL,
    "Address" TEXT NULL,
    "EmergencyContactName" VARCHAR(150) NULL,
    "EmergencyContactPhone" VARCHAR(30) NULL,
    "GeneralNotes" TEXT NULL,
    
    -- Facturación Fiscal (CFDI México)
    "RequiresInvoice" BOOLEAN NOT NULL DEFAULT FALSE,
    "TaxId" VARCHAR(20) NULL,        -- RFC
    "LegalName" VARCHAR(250) NULL,   -- Razón Social
    "TaxRegime" VARCHAR(150) NULL,   -- Régimen Fiscal
    "TaxZipCode" VARCHAR(10) NULL,   -- Código Postal Fiscal
    "CfdiUsage" VARCHAR(10) NULL DEFAULT 'D01',

    "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "UpdatedAt" TIMESTAMPTZ NULL
);

CREATE TABLE IF NOT EXISTS "MedicalAlerts" (
    "Id" SERIAL PRIMARY KEY,
    "PatientId" INT NOT NULL REFERENCES "Patients"("Id") ON DELETE CASCADE,
    "AlertType" INT NOT NULL,     -- 1: Alergia, 2: Sistémica, 3: Medicación, 4: Embarazo
    "Severity" INT NOT NULL,      -- 1: Bajo, 2: Medio, 3: Crítico (Rojo)
    "Description" VARCHAR(300) NOT NULL,
    "ActionProtocol" TEXT NULL,
    "IsPermanent" BOOLEAN NOT NULL DEFAULT TRUE,
    "ReportedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- 4. CITAS, CONTROL DE FLUJO Y LISTA DE ESPERA
CREATE TABLE IF NOT EXISTS "Appointments" (
    "Id" SERIAL PRIMARY KEY,
    "AppointmentCode" VARCHAR(40) NOT NULL UNIQUE,
    "PatientId" INT NOT NULL REFERENCES "Patients"("Id") ON DELETE RESTRICT,
    "DentistId" INT NOT NULL REFERENCES "DentistProfiles"("Id") ON DELETE RESTRICT,
    "DentalChairId" INT NOT NULL REFERENCES "DentalChairs"("Id") ON DELETE RESTRICT,
    "ServiceCatalogId" INT NOT NULL REFERENCES "ServiceCatalogs"("Id") ON DELETE RESTRICT,
    "StartDateTime" TIMESTAMPTZ NOT NULL,
    "EndDateTime" TIMESTAMPTZ NOT NULL,
    "Status" INT NOT NULL DEFAULT 1, -- 1: Scheduled, 2: Confirmed, 3: WaitingRoom, 4: InChair, 5: Completed, 6: Cancelled
    
    "MagicToken" UUID NOT NULL DEFAULT uuid_generate_v4(),
    "MagicTokenExpiresAt" TIMESTAMPTZ NOT NULL DEFAULT (NOW() + INTERVAL '7 days'),
    
    -- Tiempos de flujo en recepción
    "WaitingRoomCheckInAt" TIMESTAMPTZ NULL,
    "InChairCheckInAt" TIMESTAMPTZ NULL,
    "CompletedAt" TIMESTAMPTZ NULL,
    "CancelledAt" TIMESTAMPTZ NULL,
    "CancellationReason" TEXT NULL,
    "NeedsReschedule" BOOLEAN NOT NULL DEFAULT FALSE,
    
    -- Triaje y notas clínicas
    "PainLevel" INT NOT NULL DEFAULT 0,
    "IsEmergency" BOOLEAN NOT NULL DEFAULT FALSE,
    "QuickAllergyAlertNote" VARCHAR(300) NULL,
    "PatientBookingNotes" TEXT NULL,
    "ClinicalEvolutionNotes" TEXT NULL,
    
    "RowVersion" BYTEA NULL,
    "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "UpdatedAt" TIMESTAMPTZ NULL
);

CREATE INDEX IF NOT EXISTS "IX_Appointments_Dentist_Slot" ON "Appointments"("DentistId", "StartDateTime", "EndDateTime");
CREATE INDEX IF NOT EXISTS "IX_Appointments_Chair_Slot" ON "Appointments"("DentalChairId", "StartDateTime", "EndDateTime");

CREATE TABLE IF NOT EXISTS "WaitlistEntries" (
    "Id" SERIAL PRIMARY KEY,
    "PatientId" INT NULL REFERENCES "Patients"("Id") ON DELETE SET NULL,
    "PatientName" VARCHAR(150) NOT NULL,
    "PhoneNumber" VARCHAR(30) NOT NULL,
    "Email" VARCHAR(150) NULL,
    "ServiceCatalogId" INT NOT NULL REFERENCES "ServiceCatalogs"("Id") ON DELETE CASCADE,
    "PreferredDentistId" INT NULL REFERENCES "DentistProfiles"("Id") ON DELETE SET NULL,
    "RequestedDate" DATE NOT NULL,
    "PreferredTimeRange" INT NOT NULL DEFAULT 0,
    "Status" INT NOT NULL DEFAULT 1,
    "ClaimToken" UUID NOT NULL DEFAULT uuid_generate_v4(),
    "NotifiedAt" TIMESTAMPTZ NULL,
    "ClaimExpiresAt" TIMESTAMPTZ NULL,
    "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- 5. ODONTOGRAMA VECTORIAL INTERACTIVO (FDI)
CREATE TABLE IF NOT EXISTS "OdontogramRecords" (
    "Id" SERIAL PRIMARY KEY,
    "PatientId" INT NOT NULL REFERENCES "Patients"("Id") ON DELETE CASCADE,
    "DentistId" INT NOT NULL REFERENCES "DentistProfiles"("Id") ON DELETE RESTRICT,
    "SnapshotDate" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "SnapshotTitle" VARCHAR(200) NOT NULL DEFAULT 'Estado Inicial al Ingreso',
    "IsCurrentActive" BOOLEAN NOT NULL DEFAULT TRUE,
    "TotalEstimatedCost" NUMERIC(10,2) NOT NULL DEFAULT 0.00,
    "ClinicalNotes" TEXT NULL,
    "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS "ToothSurfaceStates" (
    "Id" SERIAL PRIMARY KEY,
    "OdontogramRecordId" INT NOT NULL REFERENCES "OdontogramRecords"("Id") ON DELETE CASCADE,
    "ToothNumber" INT NOT NULL, -- FDI: 11-48 adultos, 51-85 niños
    "Surface" INT NOT NULL DEFAULT 0, -- 0=Diente completo, 1=Oclusal, 2=Vestibular, 3=Lingual, 4=Mesial, 5=Distal
    "ConditionType" INT NOT NULL, -- 1=Caries, 2=Resin, 3=Crown, 4=Endodontics, 5=Extraction, 6=Missing
    "Status" INT NOT NULL DEFAULT 2, -- 1=Preexistente, 2=PlanTratamiento
    "ServiceCatalogId" INT NULL REFERENCES "ServiceCatalogs"("Id") ON DELETE SET NULL,
    "EstimatedPrice" NUMERIC(10,2) NOT NULL DEFAULT 0.00,
    "ClinicalNotes" VARCHAR(250) NULL,
    "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "UpdatedAt" TIMESTAMPTZ NULL
);

-- 6. EXPEDIENTE DIGITALIZADO (MIGRACIÓN PAPEL) Y RECETAS
CREATE TABLE IF NOT EXISTS "PatientDocuments" (
    "Id" SERIAL PRIMARY KEY,
    "PatientId" INT NOT NULL REFERENCES "Patients"("Id") ON DELETE CASCADE,
    "Title" VARCHAR(200) NOT NULL,
    "DocumentType" INT NOT NULL DEFAULT 1, -- 1=HistorialPapelEscaneado, 2=Radiografia, 3=Consentimiento
    "OriginalFileName" VARCHAR(255) NOT NULL,
    "StoredFileName" VARCHAR(255) NOT NULL,
    "RelativePath" VARCHAR(500) NOT NULL,
    "ContentType" VARCHAR(100) NULL,
    "FileSizeBytes" BIGINT NOT NULL DEFAULT 0,
    "Notes" TEXT NULL,
    "UploadedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS "Prescriptions" (
    "Id" SERIAL PRIMARY KEY,
    "PatientId" INT NOT NULL REFERENCES "Patients"("Id") ON DELETE CASCADE,
    "DentistId" INT NOT NULL REFERENCES "DentistProfiles"("Id") ON DELETE RESTRICT,
    "AppointmentId" INT NULL REFERENCES "Appointments"("Id") ON DELETE SET NULL,
    "PrescriptionFolio" VARCHAR(30) NOT NULL UNIQUE,
    "Diagnosis" VARCHAR(300) NOT NULL,
    "GeneralInstructions" TEXT NULL,
    "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS "PrescriptionItems" (
    "Id" SERIAL PRIMARY KEY,
    "PrescriptionId" INT NOT NULL REFERENCES "Prescriptions"("Id") ON DELETE CASCADE,
    "MedicineName" VARCHAR(200) NOT NULL,
    "Dosage" VARCHAR(150) NOT NULL,
    "FrequencyHours" INT NOT NULL DEFAULT 8,
    "DurationDays" INT NOT NULL DEFAULT 5,
    "Instructions" TEXT NULL,
    "StartDate" TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS "PaymentRecords" (
    "Id" SERIAL PRIMARY KEY,
    "AppointmentId" INT NOT NULL REFERENCES "Appointments"("Id") ON DELETE CASCADE,
    "Amount" NUMERIC(10,2) NOT NULL,
    "PaymentMethod" INT NOT NULL DEFAULT 1, -- 1=Efectivo, 2=Tarjeta, 3=Transferencia
    "PaymentStatus" INT NOT NULL DEFAULT 2, -- 2=Paid
    "TransactionReference" VARCHAR(100) NULL,
    "InvoiceNumber" VARCHAR(50) NULL,
    "PaidAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "Notes" TEXT NULL
);

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

-- =============================================================================
-- DATOS INICIALES DE DEMOSTRACIÓN (SEED)
-- =============================================================================
INSERT INTO "AspNetRoles" ("Id", "Name", "NormalizedName", "Description") VALUES
('11111111-1111-1111-1111-111111111111', 'SuperAdmin', 'SUPERADMIN', 'Rol Administrador Dueño'),
('22222222-2222-2222-2222-222222222222', 'Dentist', 'DENTIST', 'Rol Odontólogo Especialista'),
('33333333-3333-3333-3333-333333333333', 'Receptionist', 'RECEPTIONIST', 'Rol Recepción y Sala')
ON CONFLICT DO NOTHING;

INSERT INTO "ServiceCatalogs" ("Id", "Name", "Category", "Description", "DurationMinutes", "BufferTimeMinutes", "Price", "IconName", "BadgeColor") VALUES
(1, 'Valoración Inicial & Diagnóstico 3D', 'Diagnóstico', 'Evaluación clínica completa con Rx digitales', 40, 10, 350.00, 'stethoscope', '#2563EB'),
(2, 'Limpieza Ultrasónica & Profilaxis', 'Higiene', 'Eliminación de sarro con cavitron y pulido', 45, 10, 650.00, 'sparkles', '#059669'),
(3, 'Urgencia Dental / Alivio del Dolor', 'Urgencias', 'Atención prioritaria y sedación pulpar', 30, 10, 500.00, 'flame', '#DC2626'),
(4, 'Resina Estética Fotocurable', 'Restauradora', 'Obturación estética nano-híbrida con aislamiento', 45, 15, 850.00, 'shield-check', '#D97706'),
(5, 'Extracción Simple / Quirúrgica', 'Cirugía', 'Exodoncia atraumática y sutura reabsorbible', 60, 15, 1200.00, 'scissors', '#7C3AED'),
(6, 'Blanqueamiento Dental LED', 'Estética', 'Aclaramiento en consultorio al 35% activado por luz', 60, 15, 2500.00, 'sun', '#0D9488')
ON CONFLICT DO NOTHING;

INSERT INTO "DentalChairs" ("Id", "Name", "RoomNumber", "Description") VALUES
(1, 'Sillón 1 - Quirúrgico & RX', 'Box A-101', 'Equipado con Rayos X digital periapical'),
(2, 'Sillón 2 - Estética & General', 'Box B-102', 'Lámpara LED de alta potencia y cámara intraoral'),
(3, 'Sillón 3 - Higiene & Prevención', 'Box C-103', 'Cavitron ultrasónico y pulidor de bicarbonato')
ON CONFLICT DO NOTHING;
