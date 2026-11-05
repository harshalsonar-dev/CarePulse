using CarePulse.Data;
using CarePulse.Models.Entities;
using CarePulse.Models.Enums;
using CarePulse.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CarePulse.Tests
{
    public class SchedulingServiceTests
    {
        private ApplicationDbContext CreateInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            return new ApplicationDbContext(options);
        }

        private async Task<(DoctorProfile Doctor, PatientProfile Patient)> SeedBasicDoctorAndPatientAsync(ApplicationDbContext context)
        {
            var docUser = new ApplicationUser { Id = "doc-user-1", FullName = "Dr. Alice Smith", Email = "alice@carepulse.com" };
            var patUser = new ApplicationUser { Id = "pat-user-1", FullName = "Bob Johnson", Email = "bob@example.com" };

            context.Users.AddRange(docUser, patUser);

            var doctor = new DoctorProfile
            {
                Id = 1,
                UserId = docUser.Id,
                Specialization = "Cardiology",
                LicenseNumber = "LIC-001",
                WorkingDays = "Monday,Tuesday,Wednesday,Thursday,Friday,Saturday,Sunday",
                DailyStartTime = new TimeSpan(9, 0, 0),
                DailyEndTime = new TimeSpan(17, 0, 0),
                SlotDurationMinutes = 30,
                IsAvailable = true
            };

            var patient = new PatientProfile
            {
                Id = 1,
                UserId = patUser.Id,
                BloodGroup = "O+"
            };

            context.DoctorProfiles.Add(doctor);
            context.PatientProfiles.Add(patient);
            await context.SaveChangesAsync();

            return (doctor, patient);
        }

        [Fact]
        public async Task GetAvailableSlots_CalculatesCorrectNumberOf30MinSlots()
        {
            using var context = CreateInMemoryDbContext();
            await SeedBasicDoctorAndPatientAsync(context);
            var service = new SchedulingService(context, NullLogger<SchedulingService>.Instance);

            var futureDate = DateTime.Today.AddDays(7);
            var slots = await service.GetAvailableSlotsAsync(1, futureDate);

            // 09:00 to 17:00 = 8 hours = 16 slots of 30 mins
            Assert.Equal(16, slots.Count);
            Assert.All(slots, s => Assert.True(s.IsAvailable));
            Assert.Equal(new TimeSpan(9, 0, 0), slots.First().StartTime);
            Assert.Equal(new TimeSpan(17, 0, 0), slots.Last().EndTime);
        }

        [Fact]
        public async Task GetAvailableSlots_CustomSlotDuration60Mins_Calculates8Slots()
        {
            using var context = CreateInMemoryDbContext();
            var (doctor, _) = await SeedBasicDoctorAndPatientAsync(context);
            doctor.SlotDurationMinutes = 60;
            await context.SaveChangesAsync();

            var service = new SchedulingService(context, NullLogger<SchedulingService>.Instance);
            var futureDate = DateTime.Today.AddDays(7);

            var slots = await service.GetAvailableSlotsAsync(doctor.Id, futureDate);

            // 8 hours / 60 mins = 8 slots
            Assert.Equal(8, slots.Count);
        }

        [Fact]
        public async Task GetAvailableSlots_CustomSlotDuration15Mins_Calculates32Slots()
        {
            using var context = CreateInMemoryDbContext();
            var (doctor, _) = await SeedBasicDoctorAndPatientAsync(context);
            doctor.SlotDurationMinutes = 15;
            await context.SaveChangesAsync();

            var service = new SchedulingService(context, NullLogger<SchedulingService>.Instance);
            var futureDate = DateTime.Today.AddDays(7);

            var slots = await service.GetAvailableSlotsAsync(doctor.Id, futureDate);

            // 8 hours * 4 slots/hr = 32 slots
            Assert.Equal(32, slots.Count);
        }

        [Fact]
        public async Task GetAvailableSlots_DoctorNotWorkingOnDay_ReturnsEmpty()
        {
            using var context = CreateInMemoryDbContext();
            var (doctor, _) = await SeedBasicDoctorAndPatientAsync(context);
            doctor.WorkingDays = "Monday,Tuesday";
            await context.SaveChangesAsync();

            var service = new SchedulingService(context, NullLogger<SchedulingService>.Instance);

            // Find next Sunday
            var testDate = DateTime.Today;
            while (testDate.DayOfWeek != DayOfWeek.Sunday) testDate = testDate.AddDays(1);

            var slots = await service.GetAvailableSlotsAsync(doctor.Id, testDate);
            Assert.Empty(slots);
        }

        [Fact]
        public async Task GetAvailableSlots_DoctorInactive_ReturnsEmpty()
        {
            using var context = CreateInMemoryDbContext();
            var (doctor, _) = await SeedBasicDoctorAndPatientAsync(context);
            doctor.IsAvailable = false;
            await context.SaveChangesAsync();

            var service = new SchedulingService(context, NullLogger<SchedulingService>.Instance);
            var slots = await service.GetAvailableSlotsAsync(doctor.Id, DateTime.Today.AddDays(5));

            Assert.Empty(slots);
        }

        [Fact]
        public async Task GetAvailableSlots_NonExistentDoctor_ReturnsEmpty()
        {
            using var context = CreateInMemoryDbContext();
            var service = new SchedulingService(context, NullLogger<SchedulingService>.Instance);

            var slots = await service.GetAvailableSlotsAsync(999, DateTime.Today.AddDays(1));
            Assert.Empty(slots);
        }

        [Fact]
        public async Task GetAvailableSlots_ExistingAppointment_MarksSlotAsUnavailable()
        {
            using var context = CreateInMemoryDbContext();
            var (doctor, patient) = await SeedBasicDoctorAndPatientAsync(context);

            var targetDate = DateTime.Today.AddDays(3);
            var appt = new Appointment
            {
                AppointmentNumber = "CP-TEST-001",
                DoctorId = doctor.Id,
                PatientId = patient.Id,
                AppointmentDate = targetDate,
                StartTime = new TimeSpan(10, 0, 0),
                EndTime = new TimeSpan(10, 30, 0),
                Status = AppointmentStatus.Confirmed,
                ChiefComplaint = "Checkup"
            };
            context.Appointments.Add(appt);
            await context.SaveChangesAsync();

            var service = new SchedulingService(context, NullLogger<SchedulingService>.Instance);
            var slots = await service.GetAvailableSlotsAsync(doctor.Id, targetDate);

            var bookedSlot = slots.FirstOrDefault(s => s.StartTime == new TimeSpan(10, 0, 0));
            Assert.NotNull(bookedSlot);
            Assert.False(bookedSlot.IsAvailable);
            Assert.Equal("Reserved / Booked", bookedSlot.ReasonUnavailable);

            var freeSlot = slots.FirstOrDefault(s => s.StartTime == new TimeSpan(10, 30, 0));
            Assert.NotNull(freeSlot);
            Assert.True(freeSlot.IsAvailable);
        }

        [Fact]
        public async Task GetAvailableSlots_CancelledAppointment_DoesNotBlockSlot()
        {
            using var context = CreateInMemoryDbContext();
            var (doctor, patient) = await SeedBasicDoctorAndPatientAsync(context);

            var targetDate = DateTime.Today.AddDays(3);
            var appt = new Appointment
            {
                AppointmentNumber = "CP-TEST-002",
                DoctorId = doctor.Id,
                PatientId = patient.Id,
                AppointmentDate = targetDate,
                StartTime = new TimeSpan(11, 0, 0),
                EndTime = new TimeSpan(11, 30, 0),
                Status = AppointmentStatus.Cancelled, // Cancelled!
                ChiefComplaint = "Checkup"
            };
            context.Appointments.Add(appt);
            await context.SaveChangesAsync();

            var service = new SchedulingService(context, NullLogger<SchedulingService>.Instance);
            var slots = await service.GetAvailableSlotsAsync(doctor.Id, targetDate);

            var slot = slots.FirstOrDefault(s => s.StartTime == new TimeSpan(11, 0, 0));
            Assert.NotNull(slot);
            Assert.True(slot.IsAvailable);
        }

        [Fact]
        public async Task ValidateBookingConflict_PastDate_ReturnsConflict()
        {
            using var context = CreateInMemoryDbContext();
            await SeedBasicDoctorAndPatientAsync(context);
            var service = new SchedulingService(context, NullLogger<SchedulingService>.Instance);

            var pastDate = DateTime.Today.AddDays(-1);
            var result = await service.ValidateBookingConflictAsync(1, 1, pastDate, new TimeSpan(10, 0, 0), new TimeSpan(10, 30, 0));

            Assert.True(result.HasConflict);
            Assert.Contains("past", result.ConflictReason, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ValidateBookingConflict_DoctorUnavailable_ReturnsConflict()
        {
            using var context = CreateInMemoryDbContext();
            var (doctor, _) = await SeedBasicDoctorAndPatientAsync(context);
            doctor.IsAvailable = false;
            await context.SaveChangesAsync();

            var service = new SchedulingService(context, NullLogger<SchedulingService>.Instance);
            var result = await service.ValidateBookingConflictAsync(doctor.Id, 1, DateTime.Today.AddDays(2), new TimeSpan(10, 0, 0), new TimeSpan(10, 30, 0));

            Assert.True(result.HasConflict);
            Assert.Contains("unavailable", result.ConflictReason, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ValidateBookingConflict_DoctorDoesNotWorkOnDay_ReturnsConflict()
        {
            using var context = CreateInMemoryDbContext();
            var (doctor, _) = await SeedBasicDoctorAndPatientAsync(context);
            doctor.WorkingDays = "Wednesday";
            await context.SaveChangesAsync();

            var service = new SchedulingService(context, NullLogger<SchedulingService>.Instance);

            // Find next Tuesday
            var tuesday = DateTime.Today;
            while (tuesday.DayOfWeek != DayOfWeek.Tuesday) tuesday = tuesday.AddDays(1);

            var result = await service.ValidateBookingConflictAsync(doctor.Id, 1, tuesday, new TimeSpan(10, 0, 0), new TimeSpan(10, 30, 0));

            Assert.True(result.HasConflict);
            Assert.Contains("Tuesday", result.ConflictReason);
        }

        [Fact]
        public async Task ValidateBookingConflict_TimeOutsideDoctorHours_ReturnsConflict()
        {
            using var context = CreateInMemoryDbContext();
            var (doctor, _) = await SeedBasicDoctorAndPatientAsync(context);
            var service = new SchedulingService(context, NullLogger<SchedulingService>.Instance);

            // Doctor works 09:00 - 17:00; test booking at 08:00
            var result = await service.ValidateBookingConflictAsync(doctor.Id, 1, DateTime.Today.AddDays(2), new TimeSpan(8, 0, 0), new TimeSpan(8, 30, 0));
            Assert.True(result.HasConflict);
            Assert.Contains("outside", result.ConflictReason, StringComparison.OrdinalIgnoreCase);

            // Test booking at 17:30
            var resultLate = await service.ValidateBookingConflictAsync(doctor.Id, 1, DateTime.Today.AddDays(2), new TimeSpan(17, 30, 0), new TimeSpan(18, 0, 0));
            Assert.True(resultLate.HasConflict);
        }

        [Fact]
        public async Task ValidateBookingConflict_ExactDoctorOverlap_ReturnsConflict()
        {
            using var context = CreateInMemoryDbContext();
            var (doctor, patient) = await SeedBasicDoctorAndPatientAsync(context);

            var targetDate = DateTime.Today.AddDays(2);
            context.Appointments.Add(new Appointment
            {
                AppointmentNumber = "CP-1",
                DoctorId = doctor.Id,
                PatientId = patient.Id,
                AppointmentDate = targetDate,
                StartTime = new TimeSpan(14, 0, 0),
                EndTime = new TimeSpan(14, 30, 0),
                Status = AppointmentStatus.Confirmed,
                ChiefComplaint = "Complaint"
            });
            await context.SaveChangesAsync();

            var service = new SchedulingService(context, NullLogger<SchedulingService>.Instance);

            // Another patient attempts to book same doctor at exact same time
            var result = await service.ValidateBookingConflictAsync(doctor.Id, 2, targetDate, new TimeSpan(14, 0, 0), new TimeSpan(14, 30, 0));

            Assert.True(result.HasConflict);
            Assert.Contains("already booked", result.ConflictReason);
        }

        [Fact]
        public async Task ValidateBookingConflict_PartialDoctorOverlapStart_ReturnsConflict()
        {
            using var context = CreateInMemoryDbContext();
            var (doctor, patient) = await SeedBasicDoctorAndPatientAsync(context);

            var targetDate = DateTime.Today.AddDays(2);
            context.Appointments.Add(new Appointment
            {
                AppointmentNumber = "CP-1",
                DoctorId = doctor.Id,
                PatientId = patient.Id,
                AppointmentDate = targetDate,
                StartTime = new TimeSpan(14, 0, 0),
                EndTime = new TimeSpan(15, 0, 0),
                Status = AppointmentStatus.Confirmed,
                ChiefComplaint = "Complaint"
            });
            await context.SaveChangesAsync();

            var service = new SchedulingService(context, NullLogger<SchedulingService>.Instance);

            // Attempt to book 14:30 - 15:30 (overlaps second half of 14:00-15:00)
            var result = await service.ValidateBookingConflictAsync(doctor.Id, 2, targetDate, new TimeSpan(14, 30, 0), new TimeSpan(15, 30, 0));
            Assert.True(result.HasConflict);
        }

        [Fact]
        public async Task ValidateBookingConflict_PartialDoctorOverlapEnd_ReturnsConflict()
        {
            using var context = CreateInMemoryDbContext();
            var (doctor, patient) = await SeedBasicDoctorAndPatientAsync(context);

            var targetDate = DateTime.Today.AddDays(2);
            context.Appointments.Add(new Appointment
            {
                AppointmentNumber = "CP-1",
                DoctorId = doctor.Id,
                PatientId = patient.Id,
                AppointmentDate = targetDate,
                StartTime = new TimeSpan(14, 0, 0),
                EndTime = new TimeSpan(15, 0, 0),
                Status = AppointmentStatus.Confirmed,
                ChiefComplaint = "Complaint"
            });
            await context.SaveChangesAsync();

            var service = new SchedulingService(context, NullLogger<SchedulingService>.Instance);

            // Attempt to book 13:30 - 14:30 (overlaps first half of 14:00-15:00)
            var result = await service.ValidateBookingConflictAsync(doctor.Id, 2, targetDate, new TimeSpan(13, 30, 0), new TimeSpan(14, 30, 0));
            Assert.True(result.HasConflict);
        }

        [Fact]
        public async Task ValidateBookingConflict_CrossDoctorPatientConflict_ReturnsConflict()
        {
            using var context = CreateInMemoryDbContext();
            var (doctor1, patient) = await SeedBasicDoctorAndPatientAsync(context);

            // Add Doctor 2
            var doc2User = new ApplicationUser { Id = "doc-user-2", FullName = "Dr. Bob" };
            context.Users.Add(doc2User);
            var doctor2 = new DoctorProfile
            {
                Id = 2,
                UserId = doc2User.Id,
                Specialization = "Neurology",
                LicenseNumber = "LIC-002",
                DailyStartTime = new TimeSpan(9, 0, 0),
                DailyEndTime = new TimeSpan(17, 0, 0),
                WorkingDays = "Monday,Tuesday,Wednesday,Thursday,Friday,Saturday,Sunday",
                IsAvailable = true
            };
            context.DoctorProfiles.Add(doctor2);

            var targetDate = DateTime.Today.AddDays(2);

            // Patient is already booked with Doctor 1 at 11:00 - 11:30
            context.Appointments.Add(new Appointment
            {
                AppointmentNumber = "CP-DOC1",
                DoctorId = doctor1.Id,
                PatientId = patient.Id,
                AppointmentDate = targetDate,
                StartTime = new TimeSpan(11, 0, 0),
                EndTime = new TimeSpan(11, 30, 0),
                Status = AppointmentStatus.Confirmed,
                ChiefComplaint = "Heart checkup"
            });
            await context.SaveChangesAsync();

            var service = new SchedulingService(context, NullLogger<SchedulingService>.Instance);

            // Same patient attempts to book Doctor 2 at 11:00 - 11:30
            var result = await service.ValidateBookingConflictAsync(doctor2.Id, patient.Id, targetDate, new TimeSpan(11, 0, 0), new TimeSpan(11, 30, 0));

            Assert.True(result.HasConflict);
            Assert.Contains("Patient already has an active clinical appointment", result.ConflictReason);
        }

        [Fact]
        public async Task ValidateBookingConflict_ReschedulingExcludesSelf_NoConflict()
        {
            using var context = CreateInMemoryDbContext();
            var (doctor, patient) = await SeedBasicDoctorAndPatientAsync(context);

            var targetDate = DateTime.Today.AddDays(2);
            var appt = new Appointment
            {
                Id = 10,
                AppointmentNumber = "CP-SELF",
                DoctorId = doctor.Id,
                PatientId = patient.Id,
                AppointmentDate = targetDate,
                StartTime = new TimeSpan(10, 0, 0),
                EndTime = new TimeSpan(10, 30, 0),
                Status = AppointmentStatus.Confirmed,
                ChiefComplaint = "Complaint"
            };
            context.Appointments.Add(appt);
            await context.SaveChangesAsync();

            var service = new SchedulingService(context, NullLogger<SchedulingService>.Instance);

            // Re-validating the same slot while passing excludeAppointmentId = 10
            var result = await service.ValidateBookingConflictAsync(
                doctor.Id,
                patient.Id,
                targetDate,
                new TimeSpan(10, 0, 0),
                new TimeSpan(10, 30, 0),
                excludeAppointmentId: 10
            );

            Assert.False(result.HasConflict);
        }

        [Fact]
        public async Task ValidateBookingConflict_FreeSlot_NoConflict()
        {
            using var context = CreateInMemoryDbContext();
            var (doctor, patient) = await SeedBasicDoctorAndPatientAsync(context);
            var service = new SchedulingService(context, NullLogger<SchedulingService>.Instance);

            var result = await service.ValidateBookingConflictAsync(
                doctor.Id,
                patient.Id,
                DateTime.Today.AddDays(2),
                new TimeSpan(15, 0, 0),
                new TimeSpan(15, 30, 0)
            );

            Assert.False(result.HasConflict);
            Assert.Null(result.ConflictReason);
        }

        [Fact]
        public async Task GenerateAppointmentNumber_FormatsCorrectlyWithCount()
        {
            using var context = CreateInMemoryDbContext();
            var service = new SchedulingService(context, NullLogger<SchedulingService>.Instance);

            var num1 = await service.GenerateAppointmentNumberAsync();
            var todayStr = DateTime.UtcNow.ToString("yyyyMMdd");
            Assert.Equal($"CP-{todayStr}-001", num1);

            // Add an appointment for today
            context.Appointments.Add(new Appointment
            {
                AppointmentNumber = num1,
                DoctorId = 1,
                PatientId = 1,
                AppointmentDate = DateTime.UtcNow.Date,
                ChiefComplaint = "C"
            });
            await context.SaveChangesAsync();

            var num2 = await service.GenerateAppointmentNumberAsync();
            Assert.Equal($"CP-{todayStr}-002", num2);
        }
    }
}
