using System.Security.Claims;
using CarePulse.Controllers;
using CarePulse.Data;
using CarePulse.Models.Entities;
using CarePulse.Models.Enums;
using CarePulse.Models.ViewModels;
using CarePulse.Services;
using CarePulse.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CarePulse.Tests
{
    public class RbacAndMvcControllersTests
    {
        private ApplicationDbContext CreateInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            return new ApplicationDbContext(options);
        }

        private Mock<UserManager<ApplicationUser>> CreateMockUserManager(ApplicationUser currentUser, string role)
        {
            var store = new Mock<IUserStore<ApplicationUser>>();
            var mockUserManager = new Mock<UserManager<ApplicationUser>>(store.Object, null!, null!, null!, null!, null!, null!, null!, null!);

            mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync(currentUser);
            mockUserManager.Setup(m => m.GetRolesAsync(currentUser))
                .ReturnsAsync(new List<string> { role });

            return mockUserManager;
        }

        private ControllerContext CreateControllerContext(string userId, string userName, string role)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Name, userName),
                new Claim(ClaimTypes.Role, role)
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var principal = new ClaimsPrincipal(identity);

            var httpContext = new DefaultHttpContext { User = principal };
            return new ControllerContext { HttpContext = httpContext };
        }

        private async Task<Appointment> SeedCompleteAppointmentAsync(ApplicationDbContext context, string patientUserId, string docUserId)
        {
            var dept = await context.Departments.FirstOrDefaultAsync();
            if (dept == null)
            {
                dept = new Department { Name = "General", Code = "GEN" };
                context.Departments.Add(dept);
                await context.SaveChangesAsync();
            }

            var doctor = await context.DoctorProfiles.FirstOrDefaultAsync(d => d.UserId == docUserId);
            if (doctor == null)
            {
                doctor = new DoctorProfile
                {
                    UserId = docUserId,
                    DepartmentId = dept.Id,
                    Specialization = "Cardiology",
                    LicenseNumber = "LIC-" + Guid.NewGuid().ToString("N")[..4],
                    WorkingDays = "Monday,Tuesday,Wednesday,Thursday,Friday,Saturday,Sunday",
                    IsAvailable = true
                };
                context.DoctorProfiles.Add(doctor);
                await context.SaveChangesAsync();
            }

            var patient = await context.PatientProfiles.FirstOrDefaultAsync(p => p.UserId == patientUserId);
            if (patient == null)
            {
                patient = new PatientProfile
                {
                    UserId = patientUserId,
                    BloodGroup = "O+"
                };
                context.PatientProfiles.Add(patient);
                await context.SaveChangesAsync();
            }

            var appt = new Appointment
            {
                AppointmentNumber = "CP-" + Guid.NewGuid().ToString("N")[..6].ToUpper(),
                DoctorId = doctor.Id,
                PatientId = patient.Id,
                AppointmentDate = DateTime.Today,
                StartTime = new TimeSpan(10, 0, 0),
                EndTime = new TimeSpan(10, 30, 0),
                ChiefComplaint = "Test complaint",
                Status = AppointmentStatus.Confirmed
            };
            context.Appointments.Add(appt);
            await context.SaveChangesAsync();

            return appt;
        }

        private async Task<Consultation> SeedCompleteConsultationAsync(ApplicationDbContext context, string patientUserId, string docUserId)
        {
            var appt = await SeedCompleteAppointmentAsync(context, patientUserId, docUserId);

            var consultation = new Consultation
            {
                AppointmentId = appt.Id,
                DoctorId = appt.DoctorId,
                PatientId = appt.PatientId,
                ChiefComplaint = appt.ChiefComplaint,
                Diagnosis = "Acute Maxillary Sinusitis",
                ConsultationDate = DateTime.UtcNow
            };
            context.Consultations.Add(consultation);

            var rx = new Prescription
            {
                ConsultationId = consultation.Id,
                PrescriptionNumber = "RX-" + Guid.NewGuid().ToString("N")[..6].ToUpper(),
                IssuedDate = DateTime.UtcNow
            };
            context.Prescriptions.Add(rx);
            await context.SaveChangesAsync();

            return consultation;
        }

        [Fact]
        public async Task AppointmentsController_Index_PatientUser_SeesOnlyOwnAppointments()
        {
            using var context = CreateInMemoryDbContext();

            var patUser1 = new ApplicationUser { Id = "pat-1", FullName = "Patient 1", UserName = "p1" };
            var patUser2 = new ApplicationUser { Id = "pat-2", FullName = "Patient 2", UserName = "p2" };
            var docUser = new ApplicationUser { Id = "doc-1", FullName = "Dr. House", UserName = "dh" };
            context.Users.AddRange(patUser1, patUser2, docUser);
            await context.SaveChangesAsync();

            var appt1 = await SeedCompleteAppointmentAsync(context, patUser1.Id, docUser.Id);
            var appt2 = await SeedCompleteAppointmentAsync(context, patUser2.Id, docUser.Id);

            var mockUserManager = CreateMockUserManager(patUser1, "Patient");
            var schedService = new SchedulingService(context, NullLogger<SchedulingService>.Instance);
            var mockNotif = new Mock<INotificationService>();

            var controller = new AppointmentsController(context, mockUserManager.Object, schedService, mockNotif.Object, NullLogger<AppointmentsController>.Instance)
            {
                ControllerContext = CreateControllerContext(patUser1.Id, patUser1.UserName, "Patient")
            };

            var result = await controller.Index(null, null);
            var viewResult = Assert.IsType<ViewResult>(result);
            var appointments = Assert.IsAssignableFrom<List<Appointment>>(viewResult.Model);

            Assert.Single(appointments);
            Assert.Equal(appt1.Id, appointments.First().Id);
        }

        [Fact]
        public async Task AppointmentsController_Index_DoctorUser_SeesOnlyAssignedAppointments()
        {
            using var context = CreateInMemoryDbContext();

            var docUser1 = new ApplicationUser { Id = "doc-1", FullName = "Dr. Carter", UserName = "d1" };
            var docUser2 = new ApplicationUser { Id = "doc-2", FullName = "Dr. Grey", UserName = "d2" };
            var patUser = new ApplicationUser { Id = "pat-1", FullName = "Patient A", UserName = "p1" };
            context.Users.AddRange(docUser1, docUser2, patUser);
            await context.SaveChangesAsync();

            var appt1 = await SeedCompleteAppointmentAsync(context, patUser.Id, docUser1.Id);
            var appt2 = await SeedCompleteAppointmentAsync(context, patUser.Id, docUser2.Id);

            var mockUserManager = CreateMockUserManager(docUser1, "Doctor");
            var schedService = new SchedulingService(context, NullLogger<SchedulingService>.Instance);
            var mockNotif = new Mock<INotificationService>();

            var controller = new AppointmentsController(context, mockUserManager.Object, schedService, mockNotif.Object, NullLogger<AppointmentsController>.Instance)
            {
                ControllerContext = CreateControllerContext(docUser1.Id, docUser1.UserName, "Doctor")
            };

            var result = await controller.Index(null, null);
            var viewResult = Assert.IsType<ViewResult>(result);
            var appointments = Assert.IsAssignableFrom<List<Appointment>>(viewResult.Model);

            Assert.Single(appointments);
            Assert.Equal(appt1.DoctorId, appointments.First().DoctorId);
        }

        [Fact]
        public async Task AppointmentsController_Details_ForeignPatientAccess_ReturnsForbid()
        {
            using var context = CreateInMemoryDbContext();

            var patUser1 = new ApplicationUser { Id = "pat-1", FullName = "Bruce Wayne", UserName = "bruce" };
            var patUser2 = new ApplicationUser { Id = "pat-2", FullName = "Clark Kent", UserName = "clark" };
            var docUser = new ApplicationUser { Id = "doc-1", FullName = "Dr. Strange", UserName = "strange" };
            context.Users.AddRange(patUser1, patUser2, docUser);
            await context.SaveChangesAsync();

            // Appointment belongs to Patient 2
            var appt = await SeedCompleteAppointmentAsync(context, patUser2.Id, docUser.Id);

            // Logged in as Patient 1
            var mockUserManager = CreateMockUserManager(patUser1, "Patient");
            var schedService = new SchedulingService(context, NullLogger<SchedulingService>.Instance);
            var mockNotif = new Mock<INotificationService>();

            var controller = new AppointmentsController(context, mockUserManager.Object, schedService, mockNotif.Object, NullLogger<AppointmentsController>.Instance)
            {
                ControllerContext = CreateControllerContext(patUser1.Id, patUser1.UserName, "Patient")
            };

            var result = await controller.Details(appt.Id);
            Assert.IsType<ForbidResult>(result);
        }

        [Fact]
        public async Task AppointmentsController_Details_AdminAccess_ReturnsViewResult()
        {
            using var context = CreateInMemoryDbContext();

            var adminUser = new ApplicationUser { Id = "admin-1", FullName = "Admin", UserName = "admin" };
            var patUser = new ApplicationUser { Id = "pat-1", FullName = "Patient", UserName = "pat" };
            var docUser = new ApplicationUser { Id = "doc-1", FullName = "Doctor", UserName = "doc" };
            context.Users.AddRange(adminUser, patUser, docUser);
            await context.SaveChangesAsync();

            var appt = await SeedCompleteAppointmentAsync(context, patUser.Id, docUser.Id);

            var mockUserManager = CreateMockUserManager(adminUser, "Admin");
            var schedService = new SchedulingService(context, NullLogger<SchedulingService>.Instance);
            var mockNotif = new Mock<INotificationService>();

            var controller = new AppointmentsController(context, mockUserManager.Object, schedService, mockNotif.Object, NullLogger<AppointmentsController>.Instance)
            {
                ControllerContext = CreateControllerContext(adminUser.Id, adminUser.UserName, "Admin")
            };

            var result = await controller.Details(appt.Id);
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<AppointmentDetailsViewModel>(viewResult.Model);
            Assert.Equal(appt.AppointmentNumber, model.Appointment.AppointmentNumber);
        }

        [Fact]
        public async Task ConsultationController_Details_ForeignPatientAccess_ReturnsForbid()
        {
            using var context = CreateInMemoryDbContext();

            var patUser1 = new ApplicationUser { Id = "pat-1", FullName = "Patient 1", UserName = "p1" };
            var patUser2 = new ApplicationUser { Id = "pat-2", FullName = "Patient 2", UserName = "p2" };
            var docUser = new ApplicationUser { Id = "doc-1", FullName = "Doctor", UserName = "doc" };
            context.Users.AddRange(patUser1, patUser2, docUser);
            await context.SaveChangesAsync();

            var consultation = await SeedCompleteConsultationAsync(context, patUser2.Id, docUser.Id);

            // Access as Patient 1
            var mockUserManager = CreateMockUserManager(patUser1, "Patient");
            var mockPdf = new Mock<IPrescriptionPdfService>();
            var mockNotif = new Mock<INotificationService>();

            var controller = new ConsultationController(context, mockUserManager.Object, mockPdf.Object, mockNotif.Object, NullLogger<ConsultationController>.Instance)
            {
                ControllerContext = CreateControllerContext(patUser1.Id, patUser1.UserName, "Patient")
            };

            var result = await controller.Details(consultation.Id);
            Assert.IsType<ForbidResult>(result);
        }

        [Fact]
        public async Task ConsultationController_DownloadPdf_AuthorizedUser_ReturnsFile()
        {
            using var context = CreateInMemoryDbContext();

            var patUser = new ApplicationUser { Id = "pat-1", FullName = "Patient", UserName = "pat" };
            var docUser = new ApplicationUser { Id = "doc-1", FullName = "Doctor", UserName = "doc" };
            context.Users.AddRange(patUser, docUser);
            await context.SaveChangesAsync();

            var consultation = await SeedCompleteConsultationAsync(context, patUser.Id, docUser.Id);

            var mockUserManager = CreateMockUserManager(patUser, "Patient");
            var mockPdf = new Mock<IPrescriptionPdfService>();
            mockPdf.Setup(p => p.GeneratePrescriptionPdfAsync(consultation.Id))
                .ReturnsAsync(new byte[] { 0x25, 0x50, 0x44, 0x46 }); // %PDF

            var mockNotif = new Mock<INotificationService>();

            var controller = new ConsultationController(context, mockUserManager.Object, mockPdf.Object, mockNotif.Object, NullLogger<ConsultationController>.Instance)
            {
                ControllerContext = CreateControllerContext(patUser.Id, patUser.UserName, "Patient")
            };

            var result = await controller.DownloadPrescriptionPdf(consultation.Id);
            var fileResult = Assert.IsType<FileContentResult>(result);
            Assert.Equal("application/pdf", fileResult.ContentType);
        }

        [Fact]
        public async Task AdminController_ToggleDoctorStatus_TogglesAvailability()
        {
            using var context = CreateInMemoryDbContext();

            var doctor = new DoctorProfile
            {
                Id = 1,
                UserId = "doc-1",
                Specialization = "Cardio",
                LicenseNumber = "L1",
                IsAvailable = true
            };
            context.DoctorProfiles.Add(doctor);
            await context.SaveChangesAsync();

            var controller = new AdminController(context)
            {
                TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())
            };

            // First toggle -> becomes false
            var result1 = await controller.ToggleDoctorStatus(1);
            var redirect1 = Assert.IsType<RedirectToActionResult>(result1);
            Assert.Equal("Dashboard", redirect1.ActionName);

            var docAfter1 = await context.DoctorProfiles.FindAsync(1);
            Assert.False(docAfter1!.IsAvailable);

            // Second toggle -> becomes true
            await controller.ToggleDoctorStatus(1);
            var docAfter2 = await context.DoctorProfiles.FindAsync(1);
            Assert.True(docAfter2!.IsAvailable);
        }

        [Fact]
        public async Task EhrController_MyRecord_PatientUser_ReturnsRecordView()
        {
            using var context = CreateInMemoryDbContext();

            var patUser = new ApplicationUser { Id = "pat-1", FullName = "Bruce Wayne", UserName = "bruce" };
            context.Users.Add(patUser);

            var patient = new PatientProfile
            {
                Id = 1,
                UserId = patUser.Id,
                BloodGroup = "O+",
                Allergies = "Latex"
            };
            context.PatientProfiles.Add(patient);
            await context.SaveChangesAsync();

            var mockUserManager = CreateMockUserManager(patUser, "Patient");
            var mockBlob = new Mock<IAzureBlobStorageService>();

            var controller = new EhrController(context, mockUserManager.Object, mockBlob.Object, NullLogger<EhrController>.Instance)
            {
                ControllerContext = CreateControllerContext(patUser.Id, patUser.UserName, "Patient")
            };

            var result = await controller.MyRecord();
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal("RecordView", viewResult.ViewName);
            var model = Assert.IsType<PatientEhrViewModel>(viewResult.Model);
            Assert.Equal("O+", model.Patient.BloodGroup);
        }

        [Fact]
        public async Task EhrController_Patient_InvalidId_ReturnsNotFound()
        {
            using var context = CreateInMemoryDbContext();

            var docUser = new ApplicationUser { Id = "doc-1", FullName = "Dr. House", UserName = "dh" };
            var mockUserManager = CreateMockUserManager(docUser, "Doctor");
            var mockBlob = new Mock<IAzureBlobStorageService>();

            var controller = new EhrController(context, mockUserManager.Object, mockBlob.Object, NullLogger<EhrController>.Instance)
            {
                ControllerContext = CreateControllerContext(docUser.Id, docUser.UserName, "Doctor")
            };

            var result = await controller.Patient(999);
            Assert.IsType<NotFoundResult>(result);
        }
    }
}
