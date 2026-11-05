using CarePulse.Data;
using CarePulse.Models.Enums;
using CarePulse.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CarePulse.Services.Background
{
    public class AppointmentNotificationBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AppointmentNotificationBackgroundService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(60);

        public AppointmentNotificationBackgroundService(
            IServiceScopeFactory scopeFactory,
            ILogger<AppointmentNotificationBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("CarePulse Appointment Notification Background Service is starting.");

            // Initial brief delay to allow database creation & seeding to complete
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessUpcomingAppointmentRemindersAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while executing Appointment Notification Background Service.");
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }

            _logger.LogInformation("CarePulse Appointment Notification Background Service is stopping.");
        }

        private async Task ProcessUpcomingAppointmentRemindersAsync(CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

            var now = DateTime.UtcNow;
            var windowStart = DateTime.Today;
            var windowEnd = DateTime.Today.AddDays(2); // Next 48 hours window

            // Find confirmed appointments coming up soon
            var upcomingAppointments = await context.Appointments
                .Include(a => a.Patient).ThenInclude(p => p!.User)
                .Include(a => a.Doctor).ThenInclude(d => d!.User)
                .Where(a => a.Status == AppointmentStatus.Confirmed 
                            && a.AppointmentDate >= windowStart 
                            && a.AppointmentDate <= windowEnd)
                .ToListAsync(cancellationToken);

            foreach (var appt in upcomingAppointments)
            {
                if (appt.Patient?.UserId == null) continue;

                // Check if reminder was already sent
                var reminderTitle = $"Reminder: Appointment {appt.AppointmentNumber} on {appt.AppointmentDate:dd MMM}";
                bool alreadySent = await context.Notifications
                    .AnyAsync(n => n.UserId == appt.Patient.UserId 
                                && n.Title == reminderTitle 
                                && n.Type == NotificationType.AppointmentReminder, cancellationToken);

                if (!alreadySent)
                {
                    var timeStr = $"{DateTime.Today.Add(appt.StartTime):hh:mm tt}";
                    await notificationService.SendNotificationAsync(
                        appt.Patient.UserId,
                        reminderTitle,
                        $"Friendly reminder from CarePulse: You have an upcoming consultation with Dr. {appt.Doctor?.User?.FullName} on {appt.AppointmentDate:dd MMM yyyy} at {timeStr}.",
                        NotificationType.AppointmentReminder,
                        $"/Appointments/Details/{appt.Id}"
                    );

                    _logger.LogInformation("Background service dispatched reminder for Appointment {AppointmentNumber} to patient {UserId}", appt.AppointmentNumber, appt.Patient.UserId);
                }
            }
        }
    }
}
