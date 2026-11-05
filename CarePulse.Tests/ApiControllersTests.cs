using CarePulse.Controllers.Api;
using CarePulse.Data;
using CarePulse.Models.Entities;
using CarePulse.Models.Enums;
using CarePulse.Services;
using CarePulse.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CarePulse.Tests
{
    public class ApiControllersTests
    {
        private ApplicationDbContext CreateInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            return new ApplicationDbContext(options);
        }

        private async Task<(DoctorProfile Doctor, PatientProfile Patient)> SeedDataAsync(ApplicationDbContext context)
        {
            var docUser = new ApplicationUser { Id = "doc-1", FullName = "Dr. John Carter", Email = "carter@carepulse.com" };
            var patUser = new ApplicationUser { Id = "pat-1", FullName = "Bruce Wayne", Email = "bruce@wayne.com" };
            context.Users.AddRange(docUser, patUser);

            var dept1 = new Department { Id = 1, Name = "Cardiology", Code = "CARD" };
            var dept2 = new Department { Id = 2, Name = "Neurology", Code = "NEU" };
            context.Departments.AddRange(dept1, dept2);

            var doctor = new DoctorProfile
            {
                Id = 1,
                UserId = docUser.Id,
                DepartmentId = dept1.Id,
                Specialization = "Cardiologist",
                LicenseNumber = "LIC-001",
                ConsultationFee = 800.00m,
                WorkingDays = "Monday,Tuesday,Wednesday,Thursday,Friday,Saturday,Sunday",
                DailyStartTime = new TimeSpan(9, 0, 0),
                DailyEndTime = new TimeSpan(17, 0, 0),
                IsAvailable = true
            };

            var patient = new PatientProfile
            {
                Id = 1,
                UserId = patUser.Id,
                BloodGroup = "O+",
                Allergies = "Latex",
                InsurancePolicyNumber = "POL-100"
            };

            context.DoctorProfiles.Add(doctor);
            context.PatientProfiles.Add(patient);
            await context.SaveChangesAsync();

            return (doctor, patient);
        }

        [Fact]
        public async Task DoctorsApi_GetDoctors_ReturnsOkWithList()
        {
            using var context = CreateInMemoryDbContext();
            await SeedDataAsync(context);
            var schedService = new SchedulingService(context, NullLogger<SchedulingService>.Instance);
            var controller = new DoctorsApiController(context, schedService);

            var actionResult = await controller.GetDoctors(null);
            var okResult = Assert.IsType<OkObjectResult>(actionResult);
            Assert.NotNull(okResult.Value);
        }

        [Fact]
        public async Task DoctorsApi_GetDoctors_FilteredByDepartment_ReturnsMatching()
        {
            using var context = CreateInMemoryDbContext();
            await SeedDataAsync(context);
            var schedService = new SchedulingService(context, NullLogger<SchedulingService>.Instance);
            var controller = new DoctorsApiController(context, schedService);

            // Filter for Department 1 (Cardiology)
            var actionResult = await controller.GetDoctors(1);
            var okResult = Assert.IsType<OkObjectResult>(actionResult);
            Assert.NotNull(okResult.Value);

            // Filter for Department 2 (Neurology) - should be empty
            var emptyResult = await controller.GetDoctors(2);
            var okEmpty = Assert.IsType<OkObjectResult>(emptyResult);
            Assert.NotNull(okEmpty.Value);
        }

        [Fact]
        public async Task DoctorsApi_GetDoctorById_ValidId_ReturnsDoctor()
        {
            using var context = CreateInMemoryDbContext();
            await SeedDataAsync(context);
            var schedService = new SchedulingService(context, NullLogger<SchedulingService>.Instance);
            var controller = new DoctorsApiController(context, schedService);

            var actionResult = await controller.GetDoctorById(1);
            var okResult = Assert.IsType<OkObjectResult>(actionResult);
            Assert.NotNull(okResult.Value);
        }

        [Fact]
        public async Task DoctorsApi_GetDoctorById_InvalidId_ReturnsNotFound()
        {
            using var context = CreateInMemoryDbContext();
            var schedService = new SchedulingService(context, NullLogger<SchedulingService>.Instance);
            var controller = new DoctorsApiController(context, schedService);

            var actionResult = await controller.GetDoctorById(999);
            Assert.IsType<NotFoundObjectResult>(actionResult);
        }

        [Fact]
        public async Task DoctorsApi_GetDoctorAvailability_InvalidDateFormat_ReturnsBadRequest()
        {
            using var context = CreateInMemoryDbContext();
            var schedService = new SchedulingService(context, NullLogger<SchedulingService>.Instance);
            var controller = new DoctorsApiController(context, schedService);

            var actionResult = await controller.GetDoctorAvailability(1, "invalid-date");
            Assert.IsType<BadRequestObjectResult>(actionResult);
        }

        [Fact]
        public async Task DoctorsApi_GetDoctorAvailability_ValidDate_ReturnsSlots()
        {
            using var context = CreateInMemoryDbContext();
            await SeedDataAsync(context);
            var schedService = new SchedulingService(context, NullLogger<SchedulingService>.Instance);
            var controller = new DoctorsApiController(context, schedService);

            var tomorrowStr = DateTime.Today.AddDays(1).ToString("yyyy-MM-dd");
            var actionResult = await controller.GetDoctorAvailability(1, tomorrowStr);

            var okResult = Assert.IsType<OkObjectResult>(actionResult);
            Assert.NotNull(okResult.Value);
        }

        [Fact]
        public async Task AppointmentsApi_GetAppointment_ValidId_ReturnsAppointment()
        {
            using var context = CreateInMemoryDbContext();
            var (doctor, patient) = await SeedDataAsync(context);

            var appt = new Appointment
            {
                Id = 1,
                AppointmentNumber = "CP-001",
                DoctorId = doctor.Id,
                PatientId = patient.Id,
                AppointmentDate = DateTime.Today,
                ChiefComplaint = "Palpitations"
            };
            context.Appointments.Add(appt);
            await context.SaveChangesAsync();

            var schedService = new SchedulingService(context, NullLogger<SchedulingService>.Instance);
            var mockNotif = new Mock<INotificationService>();
            var controller = new AppointmentsApiController(context, schedService, mockNotif.Object);

            var actionResult = await controller.GetAppointment(1);
            var okResult = Assert.IsType<OkObjectResult>(actionResult);
            Assert.NotNull(okResult.Value);
        }

        [Fact]
        public async Task AppointmentsApi_GetAppointment_InvalidId_ReturnsNotFound()
        {
            using var context = CreateInMemoryDbContext();
            var schedService = new SchedulingService(context, NullLogger<SchedulingService>.Instance);
            var mockNotif = new Mock<INotificationService>();
            var controller = new AppointmentsApiController(context, schedService, mockNotif.Object);

            var actionResult = await controller.GetAppointment(999);
            Assert.IsType<NotFoundObjectResult>(actionResult);
        }

        [Fact]
        public async Task AppointmentsApi_BookAppointment_FreeSlot_ReturnsCreated()
        {
            using var context = CreateInMemoryDbContext();
            var (doctor, patient) = await SeedDataAsync(context);

            var schedService = new SchedulingService(context, NullLogger<SchedulingService>.Instance);
            var mockNotif = new Mock<INotificationService>();
            var controller = new AppointmentsApiController(context, schedService, mockNotif.Object);

            var request = new BookAppointmentApiRequest
            {
                DoctorId = doctor.Id,
                PatientId = patient.Id,
                AppointmentDate = DateTime.Today.AddDays(2),
                StartTime = "10:00",
                EndTime = "10:30",
                ChiefComplaint = "Routine Checkup"
            };

            var actionResult = await controller.BookAppointment(request);
            var createdResult = Assert.IsType<CreatedAtActionResult>(actionResult);
            Assert.NotNull(createdResult.Value);

            mockNotif.Verify(n => n.NotifyAppointmentBookedAsync(It.IsAny<Appointment>()), Times.Once);
        }

        [Fact]
        public async Task AppointmentsApi_BookAppointment_InvalidTimeFormat_ReturnsBadRequest()
        {
            using var context = CreateInMemoryDbContext();
            var schedService = new SchedulingService(context, NullLogger<SchedulingService>.Instance);
            var mockNotif = new Mock<INotificationService>();
            var controller = new AppointmentsApiController(context, schedService, mockNotif.Object);

            var request = new BookAppointmentApiRequest
            {
                DoctorId = 1,
                PatientId = 1,
                StartTime = "invalid",
                EndTime = "time"
            };

            var actionResult = await controller.BookAppointment(request);
            Assert.IsType<BadRequestObjectResult>(actionResult);
        }

        [Fact]
        public async Task AppointmentsApi_BookAppointment_ConflictSlot_ReturnsConflict()
        {
            using var context = CreateInMemoryDbContext();
            var (doctor, patient) = await SeedDataAsync(context);

            var targetDate = DateTime.Today.AddDays(2);
            context.Appointments.Add(new Appointment
            {
                AppointmentNumber = "CP-EXISTING",
                DoctorId = doctor.Id,
                PatientId = patient.Id,
                AppointmentDate = targetDate,
                StartTime = new TimeSpan(11, 0, 0),
                EndTime = new TimeSpan(11, 30, 0),
                Status = AppointmentStatus.Confirmed,
                ChiefComplaint = "Existing"
            });
            await context.SaveChangesAsync();

            var schedService = new SchedulingService(context, NullLogger<SchedulingService>.Instance);
            var mockNotif = new Mock<INotificationService>();
            var controller = new AppointmentsApiController(context, schedService, mockNotif.Object);

            var conflictRequest = new BookAppointmentApiRequest
            {
                DoctorId = doctor.Id,
                PatientId = 2, // different patient
                AppointmentDate = targetDate,
                StartTime = "11:00",
                EndTime = "11:30",
                ChiefComplaint = "Overlap"
            };

            var actionResult = await controller.BookAppointment(conflictRequest);
            var conflictResult = Assert.IsType<ConflictObjectResult>(actionResult);
            Assert.NotNull(conflictResult.Value);
        }

        [Fact]
        public async Task AppointmentsApi_CancelAppointment_ActiveAppointment_Succeeds()
        {
            using var context = CreateInMemoryDbContext();
            var (doctor, patient) = await SeedDataAsync(context);

            var appt = new Appointment
            {
                Id = 1,
                AppointmentNumber = "CP-CANCEL",
                DoctorId = doctor.Id,
                PatientId = patient.Id,
                Status = AppointmentStatus.Confirmed,
                ChiefComplaint = "Complaint"
            };
            context.Appointments.Add(appt);
            await context.SaveChangesAsync();

            var schedService = new SchedulingService(context, NullLogger<SchedulingService>.Instance);
            var mockNotif = new Mock<INotificationService>();
            var controller = new AppointmentsApiController(context, schedService, mockNotif.Object);

            var actionResult = await controller.CancelAppointment(1, "Family emergency");
            var okResult = Assert.IsType<OkObjectResult>(actionResult);
            Assert.NotNull(okResult.Value);

            var updatedAppt = await context.Appointments.FindAsync(1);
            Assert.Equal(AppointmentStatus.Cancelled, updatedAppt!.Status);
            Assert.Equal("Family emergency", updatedAppt.CancellationReason);
            mockNotif.Verify(n => n.NotifyAppointmentCancelledAsync(It.IsAny<Appointment>(), "Family emergency"), Times.Once);
        }

        [Fact]
        public async Task AppointmentsApi_CancelAppointment_CompletedAppointment_ReturnsBadRequest()
        {
            using var context = CreateInMemoryDbContext();
            var (doctor, patient) = await SeedDataAsync(context);

            var appt = new Appointment
            {
                Id = 1,
                AppointmentNumber = "CP-DONE",
                DoctorId = doctor.Id,
                PatientId = patient.Id,
                Status = AppointmentStatus.Completed,
                ChiefComplaint = "Done"
            };
            context.Appointments.Add(appt);
            await context.SaveChangesAsync();

            var schedService = new SchedulingService(context, NullLogger<SchedulingService>.Instance);
            var mockNotif = new Mock<INotificationService>();
            var controller = new AppointmentsApiController(context, schedService, mockNotif.Object);

            var actionResult = await controller.CancelAppointment(1, "Cannot cancel completed");
            Assert.IsType<BadRequestObjectResult>(actionResult);
        }

        [Fact]
        public async Task EhrApi_GetPatientHealthRecord_ValidPatient_ReturnsHealthRecord()
        {
            using var context = CreateInMemoryDbContext();
            var (doctor, patient) = await SeedDataAsync(context);

            var appt = new Appointment { Id = 1, AppointmentNumber = "CP-1", DoctorId = doctor.Id, PatientId = patient.Id, ChiefComplaint = "CC" };
            context.Appointments.Add(appt);

            var consultation = new Consultation
            {
                Id = 1,
                AppointmentId = appt.Id,
                DoctorId = doctor.Id,
                PatientId = patient.Id,
                ChiefComplaint = "Headache",
                Diagnosis = "Migraine",
                BloodPressure = "120/80",
                HeartRateBpm = 75,
                SpO2Percentage = 99
            };
            context.Consultations.Add(consultation);

            var rx = new Prescription { Id = 1, ConsultationId = 1, PrescriptionNumber = "RX-01" };
            context.Prescriptions.Add(rx);
            context.PrescriptionItems.Add(new PrescriptionItem { PrescriptionId = 1, MedicineName = "Ibuprofen 400mg", Dosage = "400mg" });

            context.MedicalRecordAttachments.Add(new MedicalRecordAttachment
            {
                PatientId = patient.Id,
                Title = "Blood Test",
                RecordType = "Lab Report",
                FileUrl = "/sample.pdf"
            });
            await context.SaveChangesAsync();

            var controller = new EhrApiController(context);
            var actionResult = await controller.GetPatientHealthRecord(patient.Id);

            var okResult = Assert.IsType<OkObjectResult>(actionResult);
            Assert.NotNull(okResult.Value);
        }

        [Fact]
        public async Task EhrApi_GetPatientHealthRecord_InvalidPatient_ReturnsNotFound()
        {
            using var context = CreateInMemoryDbContext();
            var controller = new EhrApiController(context);

            var actionResult = await controller.GetPatientHealthRecord(999);
            Assert.IsType<NotFoundObjectResult>(actionResult);
        }
    }
}

