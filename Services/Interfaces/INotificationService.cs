using CarePulse.Models.Entities;
using CarePulse.Models.Enums;

namespace CarePulse.Services.Interfaces
{
    public interface INotificationService
    {
        Task SendNotificationAsync(string userId, string title, string message, NotificationType type, string? linkUrl = null);
        Task NotifyAppointmentBookedAsync(Appointment appointment);
        Task NotifyAppointmentStatusChangedAsync(Appointment appointment, AppointmentStatus oldStatus, AppointmentStatus newStatus);
        Task NotifyAppointmentCancelledAsync(Appointment appointment, string reason);
        Task<List<Notification>> GetUserNotificationsAsync(string userId, int count = 20);
        Task<int> GetUnreadCountAsync(string userId);
        Task MarkAsReadAsync(int notificationId, string userId);
        Task MarkAllAsReadAsync(string userId);
    }
}
