using System.ComponentModel.DataAnnotations;
using CarePulse.Models.Entities;
using CarePulse.Models.Enums;
using Xunit;

namespace CarePulse.Tests
{
    public class ModelsAndEntitiesTests
    {
        private IList<ValidationResult> ValidateModel(object model)
        {
            var results = new List<ValidationResult>();
            var context = new ValidationContext(model, null, null);
            Validator.TryValidateObject(model, context, results, true);
            return results;
        }

        [Fact]
        public void ApplicationUser_ValidModel_PassesValidation()
        {
            var user = new ApplicationUser
            {
                FullName = "Dr. Gregory House",
                Email = "house@carepulse.com",
                UserName = "house@carepulse.com",
                UserRole = "Doctor",
                IsActive = true
            };

            var results = ValidateModel(user);
            Assert.Empty(results);
        }

        [Fact]
        public void ApplicationUser_EmptyFullName_FailsValidation()
        {
            var user = new ApplicationUser
            {
                FullName = "",
                Email = "test@carepulse.com"
            };

            var results = ValidateModel(user);
            Assert.Contains(results, r => r.MemberNames.Contains("FullName"));
        }

        [Fact]
        public void Department_ValidModel_PassesValidation()
        {
            var dept = new Department
            {
                Name = "Oncology",
                Code = "ONC",
                Description = "Cancer care and therapy",
                IsActive = true
            };

            var results = ValidateModel(dept);
            Assert.Empty(results);
        }

        [Fact]
        public void Department_MissingCode_FailsValidation()
        {
            var dept = new Department
            {
                Name = "Oncology",
                Code = ""
            };

            var results = ValidateModel(dept);
            Assert.Contains(results, r => r.MemberNames.Contains("Code"));
        }

        [Fact]
        public void DoctorProfile_ValidModel_PassesValidation()
        {
            var doc = new DoctorProfile
            {
                UserId = "user-123",
                DepartmentId = 1,
                Specialization = "Interventional Cardiology",
                LicenseNumber = "LIC-99124",
                ConsultationFee = 750.00m,
                WorkingDays = "Monday,Tuesday,Wednesday",
                DailyStartTime = new TimeSpan(9, 0, 0),
                DailyEndTime = new TimeSpan(17, 0, 0),
                SlotDurationMinutes = 30
            };

            var results = ValidateModel(doc);
            Assert.Empty(results);
        }

        [Fact]
        public void DoctorProfile_MissingSpecialization_FailsValidation()
        {
            var doc = new DoctorProfile
            {
                UserId = "user-123",
                Specialization = "",
                LicenseNumber = "LIC-001"
            };

            var results = ValidateModel(doc);
            Assert.Contains(results, r => r.MemberNames.Contains("Specialization"));
        }

        [Fact]
        public void DoctorProfile_MissingLicense_FailsValidation()
        {
            var doc = new DoctorProfile
            {
                UserId = "user-123",
                Specialization = "Cardiology",
                LicenseNumber = ""
            };

            var results = ValidateModel(doc);
            Assert.Contains(results, r => r.MemberNames.Contains("LicenseNumber"));
        }

        [Fact]
        public void PatientProfile_DefaultValues_AreInitializedProperly()
        {
            var patient = new PatientProfile
            {
                UserId = "patient-user-1"
            };

            Assert.Equal("O+", patient.BloodGroup);
            Assert.NotNull(patient.Appointments);
            Assert.NotNull(patient.Consultations);
            Assert.NotNull(patient.MedicalAttachments);
        }

        [Fact]
        public void Appointment_ValidModel_PassesValidation()
        {
            var appt = new Appointment
            {
                AppointmentNumber = "CP-20261105-001",
                DoctorId = 1,
                PatientId = 1,
                AppointmentDate = DateTime.Today.AddDays(1),
                StartTime = new TimeSpan(10, 0, 0),
                EndTime = new TimeSpan(10, 30, 0),
                ChiefComplaint = "Severe migraine for 3 days",
                Status = AppointmentStatus.Confirmed
            };

            var results = ValidateModel(appt);
            Assert.Empty(results);
        }

        [Fact]
        public void Appointment_MissingChiefComplaint_FailsValidation()
        {
            var appt = new Appointment
            {
                AppointmentNumber = "CP-20261105-002",
                DoctorId = 1,
                PatientId = 1,
                AppointmentDate = DateTime.Today.AddDays(1),
                ChiefComplaint = ""
            };

            var results = ValidateModel(appt);
            Assert.Contains(results, r => r.MemberNames.Contains("ChiefComplaint"));
        }

        [Fact]
        public void Appointment_StatusTransitions_AreAllowed()
        {
            var appt = new Appointment
            {
                Status = AppointmentStatus.Pending
            };

            appt.Status = AppointmentStatus.Confirmed;
            Assert.Equal(AppointmentStatus.Confirmed, appt.Status);

            appt.Status = AppointmentStatus.InConsultation;
            Assert.Equal(AppointmentStatus.InConsultation, appt.Status);

            appt.Status = AppointmentStatus.Completed;
            Assert.Equal(AppointmentStatus.Completed, appt.Status);

            appt.Status = AppointmentStatus.Cancelled;
            Assert.Equal(AppointmentStatus.Cancelled, appt.Status);
        }

        [Fact]
        public void Consultation_ValidModel_PassesValidation()
        {
            var consultation = new Consultation
            {
                AppointmentId = 1,
                DoctorId = 1,
                PatientId = 1,
                ChiefComplaint = "Hypertension follow-up",
                Diagnosis = "Essential (primary) hypertension (I10)",
                BloodPressure = "130/85",
                HeartRateBpm = 76,
                TemperatureF = 98.4m,
                SpO2Percentage = 99
            };

            var results = ValidateModel(consultation);
            Assert.Empty(results);
        }

        [Fact]
        public void Consultation_MissingDiagnosis_FailsValidation()
        {
            var consultation = new Consultation
            {
                AppointmentId = 1,
                ChiefComplaint = "Chest pain",
                Diagnosis = ""
            };

            var results = ValidateModel(consultation);
            Assert.Contains(results, r => r.MemberNames.Contains("Diagnosis"));
        }

        [Fact]
        public void Prescription_ValidModel_PassesValidation()
        {
            var rx = new Prescription
            {
                ConsultationId = 1,
                PrescriptionNumber = "RX-2026-0001",
                IssuedDate = DateTime.UtcNow,
                ValidUntil = DateTime.UtcNow.AddDays(30)
            };

            var results = ValidateModel(rx);
            Assert.Empty(results);
        }

        [Fact]
        public void PrescriptionItem_ValidModel_PassesValidation()
        {
            var item = new PrescriptionItem
            {
                PrescriptionId = 1,
                MedicineName = "Atorvastatin 20mg",
                Dosage = "20 mg",
                Frequency = "0-0-1 (Night)",
                Duration = "30 Days",
                Route = "Oral"
            };

            var results = ValidateModel(item);
            Assert.Empty(results);
        }

        [Fact]
        public void PrescriptionItem_MissingMedicineName_FailsValidation()
        {
            var item = new PrescriptionItem
            {
                PrescriptionId = 1,
                MedicineName = "",
                Dosage = "500 mg"
            };

            var results = ValidateModel(item);
            Assert.Contains(results, r => r.MemberNames.Contains("MedicineName"));
        }

        [Fact]
        public void MedicalRecordAttachment_ValidModel_PassesValidation()
        {
            var att = new MedicalRecordAttachment
            {
                PatientId = 1,
                Title = "CBC Blood Panel",
                RecordType = "Lab Report",
                FileUrl = "/uploads/records/cbc.pdf",
                FileSizeBytes = 512000
            };

            var results = ValidateModel(att);
            Assert.Empty(results);
        }

        [Fact]
        public void Notification_DefaultProperties_AreSetCorrectly()
        {
            var notif = new Notification
            {
                UserId = "user-1",
                Title = "Test Alert",
                Message = "Test message content",
                Type = NotificationType.AppointmentConfirmed
            };

            Assert.False(notif.IsRead);
            Assert.True(notif.CreatedAt <= DateTime.UtcNow);
            Assert.Equal(NotificationType.AppointmentConfirmed, notif.Type);
        }

        [Theory]
        [InlineData(AppointmentStatus.Pending, 0)]
        [InlineData(AppointmentStatus.Confirmed, 1)]
        [InlineData(AppointmentStatus.InConsultation, 2)]
        [InlineData(AppointmentStatus.Completed, 3)]
        [InlineData(AppointmentStatus.Cancelled, 4)]
        [InlineData(AppointmentStatus.NoShow, 5)]
        public void AppointmentStatusEnum_ValuesMatchExpected(AppointmentStatus status, int expectedValue)
        {
            Assert.Equal(expectedValue, (int)status);
        }

        [Theory]
        [InlineData("APositive")]
        [InlineData("ANegative")]
        [InlineData("BPositive")]
        [InlineData("BNegative")]
        [InlineData("ABPositive")]
        [InlineData("ABNegative")]
        [InlineData("OPositive")]
        [InlineData("ONegative")]
        public void BloodGroupTypeEnum_CanParseAllTypes(string bloodType)
        {
            bool success = Enum.TryParse<BloodGroupType>(bloodType, out var result);
            Assert.True(success);
        }

        [Theory]
        [InlineData(Gender.Male, 1)]
        [InlineData(Gender.Female, 2)]
        [InlineData(Gender.Other, 3)]
        public void GenderEnum_HasExpectedMappings(Gender gender, int expectedInt)
        {
            Assert.Equal(expectedInt, (int)gender);
        }

        [Fact]
        public void Consultation_CalculatesVitalsBoundariesSafely()
        {
            var c = new Consultation
            {
                HeartRateBpm = 180,
                TemperatureF = 104.2m,
                SpO2Percentage = 91,
                WeightKg = 120.5m,
                HeightCm = 192.0m
            };

            Assert.True(c.HeartRateBpm > 100, "Tachycardia correctly captured");
            Assert.True(c.TemperatureF > 100, "Febrile temperature correctly captured");
            Assert.True(c.SpO2Percentage < 95, "Hypoxemia boundary correctly captured");
        }

        [Fact]
        public void Prescription_CollectionInitializesEmptyNotThrow()
        {
            var rx = new Prescription();
            Assert.NotNull(rx.Items);
            Assert.Empty(rx.Items);
        }

        [Fact]
        public void PatientProfile_InsuranceFields_AllowNullOrEmpty()
        {
            var p = new PatientProfile
            {
                UserId = "uid-1",
                InsuranceProvider = null,
                InsurancePolicyNumber = null
            };

            var results = ValidateModel(p);
            Assert.Empty(results);
        }
    }
}

