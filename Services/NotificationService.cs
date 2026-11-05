using CarePulse.Data;
using CarePulse.Models.Entities;
using CarePulse.Models.Enums;
using CarePulse.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CarePulse.Services
{
    public class NotificationService : INotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(ApplicationDbContext context, ILogger<NotificationService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task SendNotificationAsync(string userId, string title, string message, NotificationType type, string? linkUrl = null)
        {
            try
            {
                var notification = new Notification
                {
                    UserId = userId,
                    Title = title,
                    Message = message,
                    Type = type,
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow,
                    LinkUrl = linkUrl
                };

                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Notification sent to {UserId}: [{Title}] {Message}", userId, title, message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send notification to user {UserId}", userId);
            }
        }

        public async Task NotifyAppointmentBookedAsync(Appointment appointment)
        {
            var appt = await _context.Appointments
                .Include(a => a.Doctor).ThenInclude(d => d!.User)
                .Include(a => a.Patient).ThenInclude(p => p!.User)
                .FirstOrDefaultAsync(a => a.Id == appointment.Id) ?? appointment;

            var timeFormatted = $"{DateTime.Today.Add(appt.StartTime):hh:mm tt} - {DateTime.Today.Add(appt.EndTime):hh:mm tt}";

            // 1. Notify Patient
            if (appt.Patient?.UserId != null)
            {
                await SendNotificationAsync(
                    appt.Patient.UserId,
                    "Appointment Scheduled Successfully",
                    $"Your clinical appointment ({appt.AppointmentNumber}) with Dr. {appt.Doctor?.User?.FullName} is confirmed for {appt.AppointmentDate:dd MMM yyyy} at {timeFormatted}.",
                    NotificationType.AppointmentBooked,
                    $"/Appointments/Details/{appt.Id}"
                );
            }

            // 2. Notify Doctor
            if (appt.Doctor?.UserId != null)
            {
                await SendNotificationAsync(
                    appt.Doctor.UserId,
                    "New Patient Appointment Booked",
                    $"Patient {appt.Patient?.User?.FullName} has booked an appointment ({appt.AppointmentNumber}) for {appt.AppointmentDate:dd MMM yyyy} at {timeFormatted}. Complaint: {appt.ChiefComplaint}",
                    NotificationType.AppointmentBooked,
                    $"/Appointments/Details/{appt.Id}"
                );
            }
        }

        public async Task NotifyAppointmentStatusChangedAsync(Appointment appointment, AppointmentStatus oldStatus, AppointmentStatus newStatus)
        {
            var appt = await _context.Appointments
                .Include(a => a.Doctor).ThenInclude(d => d!.User)
                .Include(a => a.Patient).ThenInclude(p => p!.User)
                .FirstOrDefaultAsync(a => a.Id == appointment.Id) ?? appointment;

            if (appt.Patient?.UserId != null)
            {
                string title = $"Appointment Status: {newStatus}";
                string msg = $"Your appointment ({appt.AppointmentNumber}) status has changed from {oldStatus} to {newStatus}.";

                if (newStatus == AppointmentStatus.Completed)
                {
                    title = "Consultation Completed & Prescription Ready";
                    msg = $"Your consultation with Dr. {appt.Doctor?.User?.FullName} is complete. Your digital prescription is now available to download.";
                }

                await SendNotificationAsync(
                    appt.Patient.UserId,
                    title,
                    msg,
                    newStatus == AppointmentStatus.Completed ? NotificationType.PrescriptionReady : NotificationType.AppointmentConfirmed,
                    $"/Appointments/Details/{appt.Id}"
                );
            }
        }

        public async Task NotifyAppointmentCancelledAsync(Appointment appointment, string reason)
        {
            var appt = await _context.Appointments
                .Include(a => a.Doctor).ThenInclude(d => d!.User)
                .Include(a => a.Patient).ThenInclude(p => p!.User)
                .FirstOrDefaultAsync(a => a.Id == appointment.Id) ?? appointment;

            var timeFormatted = $"{DateTime.Today.Add(appt.StartTime):hh:mm tt}";

            if (appt.Patient?.UserId != null)
            {
                await SendNotificationAsync(
                    appt.Patient.UserId,
                    "Appointment Cancelled",
                    $"Your appointment ({appt.AppointmentNumber}) scheduled for {appt.AppointmentDate:dd MMM yyyy} at {timeFormatted} has been cancelled. Reason: {reason}",
                    NotificationType.AppointmentCancelled,
                    $"/Appointments/Details/{appt.Id}"
                );
            }

            if (appt.Doctor?.UserId != null)
            {
                await SendNotificationAsync(
                    appt.Doctor.UserId,
                    "Appointment Cancelled",
                    $"Appointment ({appt.AppointmentNumber}) with {appt.Patient?.User?.FullName} on {appt.AppointmentDate:dd MMM yyyy} was cancelled. Reason: {reason}",
                    NotificationType.AppointmentCancelled,
                    $"/Appointments/Details/{appt.Id}"
                );
            }
        }

        public async Task<List<Notification>> GetUserNotificationsAsync(string userId, int count = 20)
        {
            return await _context.Notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .Take(count)
                .ToListAsync();
        }

        public async Task<int> GetUnreadCountAsync(string userId)
        {
            return await _context.Notifications
                .CountAsync(n => n.UserId == userId && !n.IsRead);
        }

        public async Task MarkAsReadAsync(int notificationId, string userId)
        {
            var notif = await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

            if (notif != null && !notif.IsRead)
            {
                notif.IsRead = true;
                await _context.SaveChangesAsync();
            }
        }

        public async Task MarkAllAsReadAsync(string userId)
        {
            var unread = await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ToListAsync();

            if (unread.Any())
            {
                foreach (var item in unread)
                {
                    item.IsRead = true;
                }
                await _context.SaveChangesAsync();
            }
        }
    }
}
