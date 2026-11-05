using CarePulse.Data;
using CarePulse.Models.Entities;
using CarePulse.Models.Enums;
using CarePulse.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CarePulse.Tests
{
    public class NotificationServiceTests
    {
        private ApplicationDbContext CreateInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            return new ApplicationDbContext(options);
        }

        [Fact]
        public async Task SendNotification_CreatesUnreadRecordInDatabase()
        {
            using var context = CreateInMemoryDbContext();
            var service = new NotificationService(context, NullLogger<NotificationService>.Instance);

            await service.SendNotificationAsync("user-1", "Test Title", "Test Message", NotificationType.SystemAlert, "/link");

            var notif = await context.Notifications.FirstOrDefaultAsync(n => n.UserId == "user-1");
            Assert.NotNull(notif);
            Assert.Equal("Test Title", notif.Title);
            Assert.Equal("Test Message", notif.Message);
            Assert.False(notif.IsRead);
            Assert.Equal("/link", notif.LinkUrl);
        }

        [Fact]
        public async Task NotifyAppointmentBooked_DispatchesToBothDoctorAndPatient()
        {
            using var context = CreateInMemoryDbContext();
            var service = new NotificationService(context, NullLogger<NotificationService>.Instance);

            var docUser = new ApplicationUser { Id = "doc-uid-1", FullName = "Dr. Carter" };
            var patUser = new ApplicationUser { Id = "pat-uid-1", FullName = "Bruce Wayne" };
            context.Users.AddRange(docUser, patUser);

            var doctor = new DoctorProfile { Id = 1, UserId = docUser.Id };
            var patient = new PatientProfile { Id = 1, UserId = patUser.Id };
            context.DoctorProfiles.Add(doctor);
            context.PatientProfiles.Add(patient);

            var appt = new Appointment
            {
                Id = 1,
                AppointmentNumber = "CP-20261105-001",
                DoctorId = doctor.Id,
                PatientId = patient.Id,
                AppointmentDate = DateTime.Today,
                StartTime = new TimeSpan(10, 0, 0),
                EndTime = new TimeSpan(10, 30, 0),
                ChiefComplaint = "Chest pain"
            };
            context.Appointments.Add(appt);
            await context.SaveChangesAsync();

            await service.NotifyAppointmentBookedAsync(appt);

            var patientNotifs = await context.Notifications.Where(n => n.UserId == patUser.Id).ToListAsync();
            var doctorNotifs = await context.Notifications.Where(n => n.UserId == docUser.Id).ToListAsync();

            Assert.Single(patientNotifs);
            Assert.Single(doctorNotifs);
            Assert.Equal(NotificationType.AppointmentBooked, patientNotifs.First().Type);
            Assert.Equal(NotificationType.AppointmentBooked, doctorNotifs.First().Type);
        }

        [Fact]
        public async Task NotifyAppointmentStatusChanged_CompletedStatus_SetsPrescriptionReadyType()
        {
            using var context = CreateInMemoryDbContext();
            var service = new NotificationService(context, NullLogger<NotificationService>.Instance);

            var patUser = new ApplicationUser { Id = "pat-uid-1", FullName = "Bruce" };
            context.Users.Add(patUser);
            var patient = new PatientProfile { Id = 1, UserId = patUser.Id };
            context.PatientProfiles.Add(patient);

            var appt = new Appointment
            {
                Id = 1,
                AppointmentNumber = "CP-COMPLETED",
                PatientId = patient.Id,
                Status = AppointmentStatus.Completed
            };
            context.Appointments.Add(appt);
            await context.SaveChangesAsync();

            await service.NotifyAppointmentStatusChangedAsync(appt, AppointmentStatus.Confirmed, AppointmentStatus.Completed);

            var notif = await context.Notifications.FirstOrDefaultAsync(n => n.UserId == patUser.Id);
            Assert.NotNull(notif);
            Assert.Equal(NotificationType.PrescriptionReady, notif.Type);
            Assert.Contains("Prescription Ready", notif.Title);
        }

        [Fact]
        public async Task NotifyAppointmentCancelled_SendsCancellationAlertWithReason()
        {
            using var context = CreateInMemoryDbContext();
            var service = new NotificationService(context, NullLogger<NotificationService>.Instance);

            var patUser = new ApplicationUser { Id = "pat-uid-1", FullName = "Bruce" };
            context.Users.Add(patUser);
            var patient = new PatientProfile { Id = 1, UserId = patUser.Id };
            context.PatientProfiles.Add(patient);

            var appt = new Appointment
            {
                Id = 1,
                AppointmentNumber = "CP-CANCEL",
                PatientId = patient.Id
            };
            context.Appointments.Add(appt);
            await context.SaveChangesAsync();

            await service.NotifyAppointmentCancelledAsync(appt, "Physician emergency leave");

            var notif = await context.Notifications.FirstOrDefaultAsync(n => n.UserId == patUser.Id);
            Assert.NotNull(notif);
            Assert.Equal(NotificationType.AppointmentCancelled, notif.Type);
            Assert.Contains("Physician emergency leave", notif.Message);
        }

        [Fact]
        public async Task GetUnreadCount_And_MarkAsRead_WorkCorrectly()
        {
            using var context = CreateInMemoryDbContext();
            var service = new NotificationService(context, NullLogger<NotificationService>.Instance);

            context.Notifications.AddRange(
                new Notification { Id = 1, UserId = "user-A", Title = "1", Message = "M1", IsRead = false },
                new Notification { Id = 2, UserId = "user-A", Title = "2", Message = "M2", IsRead = false },
                new Notification { Id = 3, UserId = "user-A", Title = "3", Message = "M3", IsRead = true }
            );
            await context.SaveChangesAsync();

            var countBefore = await service.GetUnreadCountAsync("user-A");
            Assert.Equal(2, countBefore);

            await service.MarkAsReadAsync(1, "user-A");

            var countAfter = await service.GetUnreadCountAsync("user-A");
            Assert.Equal(1, countAfter);

            var updatedNotif = await context.Notifications.FindAsync(1);
            Assert.True(updatedNotif!.IsRead);
        }

        [Fact]
        public async Task MarkAllAsRead_MarksAllForUser()
        {
            using var context = CreateInMemoryDbContext();
            var service = new NotificationService(context, NullLogger<NotificationService>.Instance);

            context.Notifications.AddRange(
                new Notification { Id = 10, UserId = "user-B", Title = "T1", Message = "M", IsRead = false },
                new Notification { Id = 11, UserId = "user-B", Title = "T2", Message = "M", IsRead = false }
            );
            await context.SaveChangesAsync();

            await service.MarkAllAsReadAsync("user-B");

            var unreadCount = await service.GetUnreadCountAsync("user-B");
            Assert.Equal(0, unreadCount);
        }
    }
}
