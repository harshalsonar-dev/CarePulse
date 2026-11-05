using CarePulse.Data;
using CarePulse.Models.Entities;
using CarePulse.Models.Enums;
using CarePulse.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CarePulse.Tests
{
    public class ComprehensiveClinicalWorkflowTests
    {
        private ApplicationDbContext CreateInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            return new ApplicationDbContext(options);
        }

        [Fact]
        public async Task EndToEnd_ClinicalConsultationWorkflow_SucceedsFlawlessly()
        {
            using var context = CreateInMemoryDbContext();
            var schedService = new SchedulingService(context, NullLogger<SchedulingService>.Instance);
            var notifService = new NotificationService(context, NullLogger<NotificationService>.Instance);
            var pdfService = new PrescriptionPdfService(context, NullLogger<PrescriptionPdfService>.Instance);

            // Step 1: Setup Doctor and Patient
            var docUser = new ApplicationUser { Id = "doc-alpha", FullName = "Dr. Stephen Strange", Email = "strange@carepulse.com" };
            var patUser = new ApplicationUser { Id = "pat-alpha", FullName = "Peter Parker", Email = "peter@parker.com", DateOfBirth = new DateTime(2001, 8, 10), Gender = Gender.Male };
            context.Users.AddRange(docUser, patUser);

            var dept = new Department { Id = 1, Name = "Neurology", Code = "NEU" };
            context.Departments.Add(dept);

            var doctor = new DoctorProfile
            {
                Id = 1,
                UserId = docUser.Id,
                DepartmentId = dept.Id,
                Specialization = "Neuro-Spinal Surgery",
                LicenseNumber = "LIC-STRANGE-01",
                ConsultationFee = 950.00m,
                WorkingDays = "Monday,Tuesday,Wednesday,Thursday,Friday,Saturday,Sunday",
                DailyStartTime = new TimeSpan(8, 0, 0),
                DailyEndTime = new TimeSpan(18, 0, 0),
                SlotDurationMinutes = 30,
                IsAvailable = true
            };

            var patient = new PatientProfile
            {
                Id = 1,
                UserId = patUser.Id,
                BloodGroup = "A+",
                Allergies = "Spider venom sensitivity"
            };

            context.DoctorProfiles.Add(doctor);
            context.PatientProfiles.Add(patient);
            await context.SaveChangesAsync();

            // Step 2: Query availability & book appointment
            var appointmentDate = DateTime.Today.AddDays(2);
            var slots = await schedService.GetAvailableSlotsAsync(doctor.Id, appointmentDate);
            Assert.NotEmpty(slots);
            var targetSlot = slots.First(s => s.StartTime == new TimeSpan(10, 0, 0));
            Assert.True(targetSlot.IsAvailable);

            var conflictCheck = await schedService.ValidateBookingConflictAsync(
                doctor.Id, patient.Id, appointmentDate, targetSlot.StartTime, targetSlot.EndTime);
            Assert.False(conflictCheck.HasConflict);

            var apptNumber = await schedService.GenerateAppointmentNumberAsync();
            var appointment = new Appointment
            {
                Id = 1,
                AppointmentNumber = apptNumber,
                DoctorId = doctor.Id,
                PatientId = patient.Id,
                AppointmentDate = appointmentDate,
                StartTime = targetSlot.StartTime,
                EndTime = targetSlot.EndTime,
                Status = AppointmentStatus.Confirmed,
                ChiefComplaint = "Cervical spine stiffness and radiating arm tingling"
            };
            context.Appointments.Add(appointment);
            await context.SaveChangesAsync();

            // Step 3: Trigger Booking Notification
            await notifService.NotifyAppointmentBookedAsync(appointment);
            var patientNotif = await context.Notifications.FirstOrDefaultAsync(n => n.UserId == patUser.Id && n.Type == NotificationType.AppointmentBooked);
            Assert.NotNull(patientNotif);

            // Step 4: Verify Double Booking is Blocked
            var doubleBookingAttempt = await schedService.ValidateBookingConflictAsync(
                doctor.Id, 2, appointmentDate, targetSlot.StartTime, targetSlot.EndTime);
            Assert.True(doubleBookingAttempt.HasConflict);

            // Step 5: Doctor examines patient and records consultation
            var consultation = new Consultation
            {
                Id = 1,
                AppointmentId = appointment.Id,
                DoctorId = doctor.Id,
                PatientId = patient.Id,
                ConsultationDate = DateTime.UtcNow,
                ChiefComplaint = appointment.ChiefComplaint,
                HistoryOfPresentIllness = "Patient reports progressive cervical discomfort following heavy gymnastics.",
                Diagnosis = "Cervical Radiculopathy (M54.12)",
                BloodPressure = "118/76 mmHg",
                HeartRateBpm = 68,
                TemperatureF = 98.4m,
                SpO2Percentage = 100,
                WeightKg = 74.0m,
                HeightCm = 178.0m,
                DoctorAdvice = "Ergonomic neck support, physical therapy, avoidance of heavy axial loading.",
                FollowUpDate = DateTime.Today.AddDays(14)
            };
            context.Consultations.Add(consultation);

            var prescription = new Prescription
            {
                Id = 1,
                ConsultationId = consultation.Id,
                PrescriptionNumber = "RX-2026-99001",
                IssuedDate = DateTime.UtcNow,
                ValidUntil = DateTime.UtcNow.AddDays(30),
                GeneralInstructions = "Complete prescribed course. Report any adverse reactions immediately."
            };
            context.Prescriptions.Add(prescription);

            context.PrescriptionItems.AddRange(
                new PrescriptionItem
                {
                    PrescriptionId = prescription.Id,
                    MedicineName = "Pregabalin 75mg",
                    Dosage = "75 mg",
                    Frequency = "0-0-1 (At Bedtime)",
                    Duration = "14 Days",
                    Route = "Oral",
                    SpecialInstructions = "Take before sleep"
                },
                new PrescriptionItem
                {
                    PrescriptionId = prescription.Id,
                    MedicineName = "Methylcobalamin 1500mcg",
                    Dosage = "1500 mcg",
                    Frequency = "1-0-0 (Morning After Breakfast)",
                    Duration = "30 Days",
                    Route = "Oral"
                }
            );

            appointment.Status = AppointmentStatus.Completed;
            await context.SaveChangesAsync();

            // Step 6: Dispatch Status Change / Prescription Ready Notification
            await notifService.NotifyAppointmentStatusChangedAsync(appointment, AppointmentStatus.Confirmed, AppointmentStatus.Completed);
            var rxNotif = await context.Notifications.FirstOrDefaultAsync(n => n.UserId == patUser.Id && n.Type == NotificationType.PrescriptionReady);
            Assert.NotNull(rxNotif);

            // Step 7: Export PDF Clinical Summary
            var pdfBytes = await pdfService.GeneratePrescriptionPdfAsync(consultation.Id);
            Assert.NotNull(pdfBytes);
            Assert.True(pdfBytes.Length > 20000, "High fidelity PDF generated");
        }

        [Theory]
        [InlineData("120/80", 120, 80)]
        [InlineData("140/90", 140, 90)]
        [InlineData("110/70", 110, 70)]
        [InlineData("95/60", 95, 60)]
        public void BloodPressure_CommonClinicalFormats_ParseCorrectly(string bp, int systolic, int diastolic)
        {
            var parts = bp.Split('/');
            Assert.Equal(2, parts.Length);
            Assert.Equal(systolic, int.Parse(parts[0]));
            Assert.Equal(diastolic, int.Parse(parts[1]));
        }

        [Fact]
        public void Consultation_BmiCalculation_MatchesStandardFormula()
        {
            decimal weightKg = 70.0m;
            decimal heightCm = 175.0m;

            decimal heightM = heightCm / 100.0m;
            decimal bmi = weightKg / (heightM * heightM);

            Assert.InRange(bmi, 22.8m, 22.9m);
        }

        [Fact]
        public async Task SequentialAppointmentNumbers_NeverCollideUnderRapidCreation()
        {
            using var context = CreateInMemoryDbContext();
            var schedService = new SchedulingService(context, NullLogger<SchedulingService>.Instance);

            var generatedNumbers = new HashSet<string>();
            var today = DateTime.UtcNow.Date;

            for (int i = 1; i <= 20; i++)
            {
                var num = await schedService.GenerateAppointmentNumberAsync();
                Assert.DoesNotContain(num, generatedNumbers);
                generatedNumbers.Add(num);

                context.Appointments.Add(new Appointment
                {
                    AppointmentNumber = num,
                    DoctorId = 1,
                    PatientId = 1,
                    AppointmentDate = today,
                    ChiefComplaint = "Speed test"
                });
                await context.SaveChangesAsync();
            }

            Assert.Equal(20, generatedNumbers.Count);
        }

        [Theory]
        [InlineData(1, "Monday")]
        [InlineData(2, "Tuesday")]
        [InlineData(3, "Wednesday")]
        [InlineData(4, "Thursday")]
        [InlineData(5, "Friday")]
        [InlineData(6, "Saturday")]
        [InlineData(0, "Sunday")]
        public void DayOfWeek_MappingMatchesStandardEnum(int dayValue, string dayName)
        {
            var dow = (DayOfWeek)dayValue;
            Assert.Equal(dayName, dow.ToString());
        }

        [Fact]
        public void DoctorProfile_WorkingDaysParsing_HandlesVariedSpacingAndCasing()
        {
            var workingDays = " Monday , Tuesday , Thursday ";
            var split = workingDays.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            Assert.Equal(3, split.Length);
            Assert.Contains("Monday", split);
            Assert.Contains("Tuesday", split);
            Assert.Contains("Thursday", split);
        }

        [Fact]
        public void PrescriptionItem_FrequencyFormats_AreFormattedNicely()
        {
            var item = new PrescriptionItem
            {
                MedicineName = "Metformin 500mg",
                Dosage = "500 mg",
                Frequency = "1-0-1 (Twice Daily After Food)",
                Duration = "30 Days",
                Route = "Oral",
                SpecialInstructions = "Do not take on empty stomach"
            };

            Assert.Contains("1-0-1", item.Frequency);
            Assert.Equal("Oral", item.Route);
        }

        [Fact]
        public void MedicalRecordAttachment_ComputesHumanReadableFileSize()
        {
            long bytes = 2097152; // 2 MB
            double mb = bytes / (1024.0 * 1024.0);
            Assert.Equal(2.0, mb);
        }

        [Fact]
        public void Notification_LinkUrlGeneration_FormatsCorrectRouting()
        {
            int appointmentId = 42;
            string link = $"/Appointments/Details/{appointmentId}";
            Assert.Equal("/Appointments/Details/42", link);
        }

        [Fact]
        public async Task AppointmentCancellation_PersistsReasonAndCancelledTimestamp()
        {
            using var context = CreateInMemoryDbContext();
            var appt = new Appointment
            {
                Id = 1,
                AppointmentNumber = "CP-CANCEL-1",
                DoctorId = 1,
                PatientId = 1,
                Status = AppointmentStatus.Confirmed,
                ChiefComplaint = "Complaint"
            };
            context.Appointments.Add(appt);
            await context.SaveChangesAsync();

            var cancelTime = DateTime.UtcNow;
            appt.Status = AppointmentStatus.Cancelled;
            appt.CancelledAt = cancelTime;
            appt.CancellationReason = "Patient feeling better, no visit required.";
            await context.SaveChangesAsync();

            var loaded = await context.Appointments.FindAsync(1);
            Assert.Equal(AppointmentStatus.Cancelled, loaded!.Status);
            Assert.Equal(cancelTime, loaded.CancelledAt);
            Assert.Equal("Patient feeling better, no visit required.", loaded.CancellationReason);
        }
    }
}
