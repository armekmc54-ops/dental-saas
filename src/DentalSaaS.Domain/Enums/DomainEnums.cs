namespace DentalSaaS.Domain.Enums;

public static class UserRoles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string Dentist = "Dentist";
    public const string Receptionist = "Receptionist";
}

public enum AppointmentStatus
{
    Scheduled = 1,       // Agendada / Pendiente de confirmación
    Confirmed = 2,       // Confirmada por el paciente (vía WhatsApp/Magic Link o Recepción)
    WaitingRoom = 3,     // En Sala de Espera (llegó a la clínica)
    InChair = 4,         // En Sillón (en consulta con el dentista)
    Completed = 5,       // Finalizada
    Cancelled = 6,       // Cancelada (por paciente o clínica)
    NoShow = 7           // No asistió
}

public enum ToothSurface
{
    GeneralWholeTooth = 0, // Diente completo o corona total
    Vestibular = 1,        // Cara frontal / externa
    LingualOrPalatal = 2,  // Cara interna (Palatina superior / Lingual inferior)
    Mesial = 3,            // Cara lateral hacia la línea media
    Distal = 4,            // Cara lateral alejada de la línea media
    OcclusalOrIncisal = 5, // Cara masticatoria / borde cortante
    Root = 6               // Raíz dental (para endodoncias / periapical)
}

public enum ToothConditionType
{
    Healthy = 0,               // Sano
    Caries = 1,                // Caries activa (Rojo)
    RestorationResin = 2,      // Restauración en Resina (Azul)
    RestorationAmalgam = 3,    // Restauración en Amalgama (Azul / Gris)
    MissingExtracted = 4,      // Diente ausente / Extraído (Negro / Gris oscuro)
    Crown = 5,                 // Corona dental (Dorado / Amarillo)
    Endodontics = 6,           // Tratamiento de conducto / Endodoncia (Dorado / Azul)
    Implant = 7,               // Implante dental
    ExtractionIndicated = 8,   // Extracción indicada (Cruz Roja)
    Prosthesis = 9,            // Prótesis fija o removible
    Fracture = 10              // Fractura dental
}

public enum TreatmentClinicalStatus
{
    ExistingPreDiagnosis = 1,  // Estado preexistente con el que llegó el paciente
    PlanTreatmentPending = 2,  // Tratamiento proyectado / pendiente en presupuesto
    TreatmentCompleted = 3     // Tratamiento realizado en la clínica
}

public enum NotificationChannel
{
    WhatsApp = 1,
    Email = 2,
    Sms = 3
}

public enum NotificationType
{
    Reminder24h = 1,
    Reminder2h = 2,
    WaitlistAlert = 3,
    MagicLinkAccess = 4,
    CancellationNotice = 5
}

public enum NotificationStatus
{
    Queued = 1,
    Sent = 2,
    Delivered = 3,
    ConfirmedByPatient = 4,
    Failed = 5
}

public enum PaymentMethod
{
    Cash = 1,
    DebitCard = 2,
    CreditCard = 3,
    BankTransfer = 4,
    Insurance = 5,
    Other = 6
}

public enum PaymentStatus
{
    Pending = 1,
    Paid = 2,
    Partial = 3,
    Refunded = 4
}

public enum MedicalAlertType
{
    Allergy = 1,             // Penicilina, Látex, AINEs, etc.
    SystemicCondition = 2,   // Diabetes, Hipertensión, Cardiopatías
    Medication = 3,          // Anticoagulantes, Bifosfonatos
    Pregnancy = 4,           // Embarazo
    HighRisk = 5,            // Hepatitis, VIH, etc.
    Other = 6
}

public enum MedicalAlertSeverity
{
    Low = 1,
    Medium = 2,
    Critical = 3 // Muestra semáforo rojo llamativo en UI
}

public enum WaitlistStatus
{
    Active = 1,
    Notified = 2,
    Claimed = 3,
    Expired = 4
}

public enum PreferredTimeRange
{
    Anytime = 0,
    Morning = 1,   // 08:00 - 13:00
    Afternoon = 2  // 13:00 - 20:00
}
