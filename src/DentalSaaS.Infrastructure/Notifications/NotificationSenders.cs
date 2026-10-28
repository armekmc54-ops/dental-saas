using Microsoft.Extensions.Logging;
using DentalSaaS.Application.Interfaces;
using DentalSaaS.Domain.Enums;

namespace DentalSaaS.Infrastructure.Notifications;

public class WhatsAppNotificationSender : INotificationSender
{
    private readonly ILogger<WhatsAppNotificationSender> _logger;

    public WhatsAppNotificationSender(ILogger<WhatsAppNotificationSender> logger)
    {
        _logger = logger;
    }

    public bool SupportsChannel(NotificationChannel channel) => channel == NotificationChannel.WhatsApp;

    public async Task<NotificationSendResult> SendAsync(NotificationMessage message, CancellationToken ct = default)
    {
        // Simulación robusta y realista de integración con Meta Cloud API / Twilio WhatsApp
        _logger.LogInformation(
            "[WhatsApp API] Enviando mensaje a {Phone} ({RecipientName}) | Tipo: {Type} | Acción: {Url}",
            message.RecipientContact, message.RecipientName, message.Type, message.ActionUrl);

        // Simulamos latencia de red de llamada HTTP a la API de WhatsApp
        await Task.Delay(50, ct);

        // Generamos un ID de mensaje único como el devuelto por Meta/Twilio
        var messageId = $"wamid.HBgL{Guid.NewGuid():N}";

        _logger.LogInformation("[WhatsApp API] Mensaje entregado con éxito. MessageId: {MessageId}", messageId);

        return new NotificationSendResult
        {
            Success = true,
            ExternalMessageId = messageId,
            SentAt = DateTime.UtcNow
        };
    }
}

public class EmailNotificationSender : INotificationSender
{
    private readonly ILogger<EmailNotificationSender> _logger;

    public EmailNotificationSender(ILogger<EmailNotificationSender> logger)
    {
        _logger = logger;
    }

    public bool SupportsChannel(NotificationChannel channel) => channel == NotificationChannel.Email;

    public async Task<NotificationSendResult> SendAsync(NotificationMessage message, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[Email SMTP] Enviando correo HTML a {Email} | Asunto: {Subject} | Enlace: {Url}",
            message.RecipientContact, message.Subject, message.ActionUrl);

        // Simulamos envío SMTP / SendGrid / Mailgun
        await Task.Delay(50, ct);

        var emailId = $"email-{Guid.NewGuid():N}";

        return new NotificationSendResult
        {
            Success = true,
            ExternalMessageId = emailId,
            SentAt = DateTime.UtcNow
        };
    }
}

public class CompositeNotificationSender : INotificationSender
{
    private readonly IEnumerable<INotificationSender> _senders;
    private readonly ILogger<CompositeNotificationSender> _logger;

    public CompositeNotificationSender(
        IEnumerable<INotificationSender> senders,
        ILogger<CompositeNotificationSender> logger)
    {
        _senders = senders;
        _logger = logger;
    }

    public bool SupportsChannel(NotificationChannel channel) => true;

    public async Task<NotificationSendResult> SendAsync(NotificationMessage message, CancellationToken ct = default)
    {
        var sender = _senders.FirstOrDefault(s => s.SupportsChannel(message.Channel) && s != this);
        if (sender != null)
        {
            return await sender.SendAsync(message, ct);
        }

        _logger.LogWarning("No se encontró proveedor de notificaciones para el canal {Channel}", message.Channel);
        return new NotificationSendResult
        {
            Success = false,
            ErrorMessage = $"Proveedor no configurado para canal: {message.Channel}"
        };
    }
}
