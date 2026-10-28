using Microsoft.AspNetCore.SignalR;

namespace DentalSaaS.Web.Hubs;

public interface IClinicClient
{
    Task PatientArrivedInWaitingRoom(object alert);
    Task PatientCalledToChair(object alert);
    Task AppointmentStatusChanged(object payload);
    Task OdontogramUpdated(object payload);
    Task NewAppointmentCreated(object payload);
    Task AvailabilityChanged(object payload);
}

public class ClinicHub : Hub<IClinicClient>
{
    public async Task NotifyPatientArrival(int appointmentId, string patientName, int dentistId, string serviceName)
    {
        await Clients.All.PatientArrivedInWaitingRoom(new
        {
            AppointmentId = appointmentId,
            PatientName = patientName,
            DentistId = dentistId,
            ServiceName = serviceName,
            Timestamp = DateTime.UtcNow,
            Message = $"¡El paciente {patientName} ha llegado a sala de espera para {serviceName}!"
        });
    }

    public async Task NotifyPatientCalled(int appointmentId, string patientName, string doctorName, string chairName)
    {
        await Clients.All.PatientCalledToChair(new
        {
            AppointmentId = appointmentId,
            PatientName = patientName,
            DoctorName = doctorName,
            ChairName = chairName,
            Timestamp = DateTime.UtcNow
        });
    }

    public async Task BroadcastStatusChange(int appointmentId, string newStatus, string updatedBy)
    {
        await Clients.All.AppointmentStatusChanged(new
        {
            AppointmentId = appointmentId,
            NewStatus = newStatus,
            UpdatedBy = updatedBy,
            Timestamp = DateTime.UtcNow
        });
    }

    public async Task BroadcastOdontogramUpdate(int patientId, string dentistName, decimal totalBudget)
    {
        await Clients.All.OdontogramUpdated(new
        {
            PatientId = patientId,
            DentistName = dentistName,
            TotalBudget = totalBudget,
            Timestamp = DateTime.UtcNow
        });
    }
}
