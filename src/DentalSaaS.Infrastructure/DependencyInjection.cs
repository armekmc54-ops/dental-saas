using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using DentalSaaS.Application.Interfaces;
using DentalSaaS.Application.Services;
using DentalSaaS.Domain.Entities;
using DentalSaaS.Infrastructure.Notifications;
using DentalSaaS.Infrastructure.Persistence;
using DentalSaaS.Infrastructure.Workers;

namespace DentalSaaS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddDentalSaaSInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 1. Configuración de Base de Datos (Soporte PostgreSQL / SQL Server o InMemory/Sqlite para pruebas inmediatas)
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<ApplicationDbContext>(options =>
        {
            if (!string.IsNullOrEmpty(connectionString) && connectionString.Contains("Host="))
            {
                options.UseNpgsql(connectionString);
            }
            else if (!string.IsNullOrEmpty(connectionString) && connectionString.Contains("Server="))
            {
                options.UseSqlServer(connectionString);
            }
            else
            {
                // Fallback de desarrollo para pruebas o arranque rápido
                options.UseInMemoryDatabase("DentalSaaS_DevDb");
            }
        });

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

        // 2. ASP.NET Core Identity & Roles
        services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequiredLength = 8;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;
            options.User.RequireUniqueEmail = true;
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

        // 3. Notificaciones desacopladas (WhatsApp, Email, Composite)
        services.AddSingleton<WhatsAppNotificationSender>();
        services.AddSingleton<EmailNotificationSender>();
        services.AddTransient<INotificationSender>(sp =>
        {
            var wa = sp.GetRequiredService<WhatsAppNotificationSender>();
            var em = sp.GetRequiredService<EmailNotificationSender>();
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<CompositeNotificationSender>>();
            return new CompositeNotificationSender(new INotificationSender[] { wa, em }, logger);
        });

        // 4. Servicios de Dominio / Aplicación
        services.AddScoped<IAvailabilityService, AvailabilityService>();
        services.AddScoped<IOdontogramService, OdontogramService>();
        services.AddScoped<IClinicalAiService, ClinicalAiService>();

        // 5. Background Service de Recordatorios y Lista de Espera
        services.AddHostedService<AppointmentReminderWorker>();

        return services;
    }
}
