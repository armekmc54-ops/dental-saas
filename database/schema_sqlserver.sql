-- =============================================================================
-- DENTALCARE SAAS - ESQUEMA T-SQL (MICROSOFT SQL SERVER / AZURE SQL)
-- =============================================================================

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AspNetRoles')
BEGIN
    CREATE TABLE AspNetRoles (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        Name NVARCHAR(256) NULL,
        NormalizedName NVARCHAR(256) NULL,
        ConcurrencyStamp NVARCHAR(MAX) NULL,
        Description NVARCHAR(MAX) NULL
    );
END;

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AspNetUsers')
BEGIN
    CREATE TABLE AspNetUsers (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        UserName NVARCHAR(256) NULL,
        NormalizedUserName NVARCHAR(256) NULL,
        Email NVARCHAR(256) NULL,
        NormalizedEmail NVARCHAR(256) NULL,
        EmailConfirmed BIT NOT NULL DEFAULT 0,
        PasswordHash NVARCHAR(MAX) NULL,
        SecurityStamp NVARCHAR(MAX) NULL,
        ConcurrencyStamp NVARCHAR(MAX) NULL,
        PhoneNumber NVARCHAR(MAX) NULL,
        PhoneNumberConfirmed BIT NOT NULL DEFAULT 0,
        TwoFactorEnabled BIT NOT NULL DEFAULT 0,
        LockoutEnd DATETIMEOFFSET NULL,
        LockoutEnabled BIT NOT NULL DEFAULT 0,
        AccessFailedCount INT NOT NULL DEFAULT 0,
        FirstName NVARCHAR(100) NOT NULL,
        LastName NVARCHAR(100) NOT NULL,
        AvatarUrl NVARCHAR(500) NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        IsActive BIT NOT NULL DEFAULT 1
    );
END;

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'DentistProfiles')
BEGIN
    CREATE TABLE DentistProfiles (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        UserId UNIQUEIDENTIFIER NOT NULL UNIQUE FOREIGN KEY REFERENCES AspNetUsers(Id) ON DELETE CASCADE,
        Specialty NVARCHAR(150) NOT NULL,
        LicenseNumber NVARCHAR(80) NOT NULL,
        Biography NVARCHAR(MAX) NULL,
        CalendarColorHex NVARCHAR(20) NOT NULL DEFAULT '#2563EB',
        MaxDailyAppointments INT NOT NULL DEFAULT 12,
        IsActive BIT NOT NULL DEFAULT 1
    );
END;

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'DentalChairs')
BEGIN
    CREATE TABLE DentalChairs (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Name NVARCHAR(100) NOT NULL,
        RoomNumber NVARCHAR(50) NOT NULL,
        Description NVARCHAR(MAX) NULL,
        IsActive BIT NOT NULL DEFAULT 1
    );
END;

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ServiceCatalogs')
BEGIN
    CREATE TABLE ServiceCatalogs (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Name NVARCHAR(150) NOT NULL,
        Category NVARCHAR(80) NOT NULL DEFAULT 'General',
        Description NVARCHAR(MAX) NULL,
        DurationMinutes INT NOT NULL DEFAULT 30,
        BufferTimeMinutes INT NOT NULL DEFAULT 10,
        Price DECIMAL(10,2) NOT NULL DEFAULT 0.00,
        IsActive BIT NOT NULL DEFAULT 1,
        IconName NVARCHAR(50) DEFAULT 'tooth',
        BadgeColor NVARCHAR(20) DEFAULT '#10B981'
    );
END;

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ScheduleExceptions')
BEGIN
    CREATE TABLE ScheduleExceptions (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        DentistId INT NOT NULL FOREIGN KEY REFERENCES DentistProfiles(Id) ON DELETE CASCADE,
        StartDateTime DATETIME2 NOT NULL,
        EndDateTime DATETIME2 NOT NULL,
        Reason NVARCHAR(250) NOT NULL,
        IsFullDay BIT NOT NULL DEFAULT 0,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
    );
END;

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Patients')
BEGIN
    CREATE TABLE Patients (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        FolioNumber NVARCHAR(30) NOT NULL UNIQUE,
        FirstName NVARCHAR(100) NOT NULL,
        LastName NVARCHAR(100) NOT NULL,
        Curp NVARCHAR(18) NULL,
        IdentificationNumber NVARCHAR(50) NULL,
        PhoneNumber NVARCHAR(30) NOT NULL,
        Email NVARCHAR(150) NULL,
        DateOfBirth DATE NULL,
        Gender NVARCHAR(10) NULL,
        RequiresInvoice BIT NOT NULL DEFAULT 0,
        TaxId NVARCHAR(20) NULL,
        LegalName NVARCHAR(250) NULL,
        TaxRegime NVARCHAR(150) NULL,
        TaxZipCode NVARCHAR(10) NULL,
        CfdiUsage NVARCHAR(10) NULL DEFAULT 'D01',
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        UpdatedAt DATETIME2 NULL
    );
END;
