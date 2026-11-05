using System.Text;
using CarePulse.Data;
using CarePulse.Models.Entities;
using CarePulse.Models.Enums;
using CarePulse.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CarePulse.Tests
{
    public class PrescriptionPdfServiceTests
    {
        private ApplicationDbContext CreateInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            return new ApplicationDbContext(options);
        }

        private async Task<int> SeedConsultationAsync(ApplicationDbContext context, bool withMedicines = true, bool withVitals = true)
        {
            var docUser = new ApplicationUser { Id = "doc-1", FullName = "Dr. Meredith Grey", Email = "meredith@carepulse.com" };
            var patUser = new ApplicationUser { Id = "pat-1", FullName = "Bruce Wayne", Email = "bruce@wayne.com", DateOfBirth = new DateTime(1985, 4, 17), Gender = Gender.Male };
            context.Users.AddRange(docUser, patUser);

            var dept = new Department { Id = 1, Name = "General Internal Medicine", Code = "GEN" };
            context.Departments.Add(dept);

            var doctor = new DoctorProfile
            {
                Id = 1,
                UserId = docUser.Id,
                DepartmentId = dept.Id,
                Specialization = "Internal Medicine",
                LicenseNumber = "LIC-GEN-100",
                Qualification = "MD, FACP"
            };
            var patient = new PatientProfile
            {
                Id = 1,
                UserId = patUser.Id,
                BloodGroup = "O+",
                Allergies = "Penicillin"
            };
            context.DoctorProfiles.Add(doctor);
            context.PatientProfiles.Add(patient);

            var appt = new Appointment
            {
                Id = 1,
                AppointmentNumber = "CP-20261105-001",
                DoctorId = doctor.Id,
                PatientId = patient.Id,
                AppointmentDate = DateTime.Today,
                ChiefComplaint = "Frontal headache and sinus congestion"
            };
            context.Appointments.Add(appt);

            var consultation = new Consultation
            {
                Id = 1,
                AppointmentId = appt.Id,
                DoctorId = doctor.Id,
                PatientId = patient.Id,
                ConsultationDate = DateTime.UtcNow,
                ChiefComplaint = appt.ChiefComplaint,
                HistoryOfPresentIllness = "Patient reports acute congestion for 5 days.",
                Diagnosis = "Acute Maxillary Sinusitis (J01.00)",
                DoctorAdvice = "Hydration, saline nasal spray, steam inhalations.",
                FollowUpDate = DateTime.Today.AddDays(7)
            };

            if (withVitals)
            {
                consultation.BloodPressure = "120/80";
                consultation.HeartRateBpm = 72;
                consultation.TemperatureF = 98.6m;
                consultation.SpO2Percentage = 99;
                consultation.WeightKg = 85.0m;
                consultation.HeightCm = 188.0m;
            }

            context.Consultations.Add(consultation);

            var rx = new Prescription
            {
                Id = 1,
                ConsultationId = consultation.Id,
                PrescriptionNumber = "RX-2026-00042",
                IssuedDate = DateTime.UtcNow,
                ValidUntil = DateTime.UtcNow.AddDays(14),
                GeneralInstructions = "Take medications with food."
            };
            context.Prescriptions.Add(rx);

            if (withMedicines)
            {
                context.PrescriptionItems.AddRange(
                    new PrescriptionItem
                    {
                        PrescriptionId = rx.Id,
                        MedicineName = "Amoxicillin-Clavulanate 625mg",
                        Dosage = "625 mg",
                        Frequency = "1-0-1 (After Food)",
                        Duration = "7 Days",
                        Route = "Oral",
                        SpecialInstructions = "Twice daily with meals"
                    },
                    new PrescriptionItem
                    {
                        PrescriptionId = rx.Id,
                        MedicineName = "Levocetirizine 5mg",
                        Dosage = "5 mg",
                        Frequency = "0-0-1 (At Bedtime)",
                        Duration = "5 Days",
                        Route = "Oral",
                        SpecialInstructions = "Night only, may cause drowsiness"
                    }
                );
            }

            await context.SaveChangesAsync();
            return consultation.Id;
        }

        [Fact]
        public async Task GeneratePrescriptionPdf_ValidConsultation_ProducesValidPdfBytes()
        {
            using var context = CreateInMemoryDbContext();
            var consultationId = await SeedConsultationAsync(context, withMedicines: true, withVitals: true);

            var service = new PrescriptionPdfService(context, NullLogger<PrescriptionPdfService>.Instance);
            var pdfBytes = await service.GeneratePrescriptionPdfAsync(consultationId);

            Assert.NotNull(pdfBytes);
            Assert.NotEmpty(pdfBytes);

            // Valid PDF magic header bytes: %PDF- (0x25, 0x50, 0x44, 0x46, 0x2D)
            var header = Encoding.ASCII.GetString(pdfBytes.Take(5).ToArray());
            Assert.Equal("%PDF-", header);
            Assert.True(pdfBytes.Length > 10000, "PDF file is substantive in size");
        }

        [Fact]
        public async Task GeneratePrescriptionPdf_WithoutMedicines_GeneratesValidPdfWithoutCrashing()
        {
            using var context = CreateInMemoryDbContext();
            var consultationId = await SeedConsultationAsync(context, withMedicines: false, withVitals: true);

            var service = new PrescriptionPdfService(context, NullLogger<PrescriptionPdfService>.Instance);
            var pdfBytes = await service.GeneratePrescriptionPdfAsync(consultationId);

            Assert.NotNull(pdfBytes);
            var header = Encoding.ASCII.GetString(pdfBytes.Take(5).ToArray());
            Assert.Equal("%PDF-", header);
        }

        [Fact]
        public async Task GeneratePrescriptionPdf_WithoutVitals_HandlesNullsGracefully()
        {
            using var context = CreateInMemoryDbContext();
            var consultationId = await SeedConsultationAsync(context, withMedicines: true, withVitals: false);

            var service = new PrescriptionPdfService(context, NullLogger<PrescriptionPdfService>.Instance);
            var pdfBytes = await service.GeneratePrescriptionPdfAsync(consultationId);

            Assert.NotNull(pdfBytes);
            var header = Encoding.ASCII.GetString(pdfBytes.Take(5).ToArray());
            Assert.Equal("%PDF-", header);
        }

        [Fact]
        public async Task GeneratePrescriptionPdf_NonExistentConsultation_ThrowsInvalidOperationException()
        {
            using var context = CreateInMemoryDbContext();
            var service = new PrescriptionPdfService(context, NullLogger<PrescriptionPdfService>.Instance);

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.GeneratePrescriptionPdfAsync(9999));
        }

        [Fact]
        public async Task GeneratePrescriptionPdf_LongAdviceAndPrescription_DoesNotThrow()
        {
            using var context = CreateInMemoryDbContext();
            var consultationId = await SeedConsultationAsync(context);

            var consultation = await context.Consultations.FindAsync(consultationId);
            consultation!.DoctorAdvice = new string('A', 1000);
            consultation.HistoryOfPresentIllness = new string('H', 800);
            await context.SaveChangesAsync();

            var service = new PrescriptionPdfService(context, NullLogger<PrescriptionPdfService>.Instance);
            var pdfBytes = await service.GeneratePrescriptionPdfAsync(consultationId);

            Assert.NotNull(pdfBytes);
            Assert.NotEmpty(pdfBytes);
        }
    }
}

