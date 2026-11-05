using CarePulse.Models.Entities;
using CarePulse.Models.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CarePulse.Data
{
    public static class DbInitializer
    {
        public static async Task SeedAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DbInitializer");

            try
            {
                // Ensure DB exists
                await context.Database.EnsureCreatedAsync();

                // 1. Seed Roles
                string[] roles = { "Admin", "Doctor", "Patient" };
                foreach (var role in roles)
                {
                    if (!await roleManager.RoleExistsAsync(role))
                    {
                        await roleManager.CreateAsync(new IdentityRole(role));
                    }
                }

                // 2. Seed Admin User
                var adminEmail = "admin@carepulse.com";
                var adminUser = await userManager.FindByEmailAsync(adminEmail);
                if (adminUser == null)
                {
                    adminUser = new ApplicationUser
                    {
                        UserName = adminEmail,
                        Email = adminEmail,
                        FullName = "Dr. Alexander Vance (Chief Medical Officer)",
                        UserRole = "Admin",
                        EmailConfirmed = true,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow.AddMonths(-6)
                    };
                    await userManager.CreateAsync(adminUser, "Admin@123");
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                }

                // 3. Seed Departments
                if (!await context.Departments.AnyAsync())
                {
                    var departments = new List<Department>
                    {
                        new() { Name = "Cardiology", Code = "CARD", Description = "Heart, cardiovascular diagnostics and intervention.", IconClass = "bi-heart-pulse", IsActive = true },
                        new() { Name = "General Internal Medicine", Code = "GEN", Description = "Comprehensive clinical diagnostics, preventive health, and acute care.", IconClass = "bi-shield-plus", IsActive = true },
                        new() { Name = "Neurology", Code = "NEUR", Description = "Brain, spine, nervous system disorders, and stroke care.", IconClass = "bi-activity", IsActive = true },
                        new() { Name = "Pediatrics & Child Health", Code = "PED", Description = "Specialized infant, child, and adolescent healthcare.", IconClass = "bi-emoji-smile", IsActive = true },
                        new() { Name = "Orthopedics & Sports Medicine", Code = "ORTH", Description = "Bones, joints, ligaments, arthroscopy, and trauma management.", IconClass = "bi-bandaid", IsActive = true },
                        new() { Name = "Dermatology", Code = "DERM", Description = "Skin pathology, allergy treatment, and clinical cosmetics.", IconClass = "bi-sun", IsActive = true }
                    };

                    await context.Departments.AddRangeAsync(departments);
                    await context.SaveChangesAsync();
                }

                var cardDept = await context.Departments.FirstAsync(d => d.Code == "CARD");
                var genDept = await context.Departments.FirstAsync(d => d.Code == "GEN");
                var neurDept = await context.Departments.FirstAsync(d => d.Code == "NEUR");
                var pedDept = await context.Departments.FirstAsync(d => d.Code == "PED");

                // 4. Seed Doctors
                var doctorSeedData = new[]
                {
                    new
                    {
                        Email = "doctor.carter@carepulse.com",
                        FullName = "John Carter",
                        DepartmentId = cardDept.Id,
                        Specialization = "Interventional Cardiology",
                        License = "LIC-CARD-8819",
                        Qualification = "MD, FACC, FSCAI",
                        Exp = 14,
                        Fee = 800.00m,
                        Room = "Suite 302",
                        Bio = "Specialist in preventative cardiology, coronary interventions, echocardiography, and hypertension management.",
                        WorkingDays = "Monday,Tuesday,Wednesday,Thursday,Friday",
                        Start = new TimeSpan(9, 0, 0),
                        End = new TimeSpan(17, 0, 0)
                    },
                    new
                    {
                        Email = "doctor.meredith@carepulse.com",
                        FullName = "Meredith Grey",
                        DepartmentId = genDept.Id,
                        Specialization = "General Internal Medicine",
                        License = "LIC-GEN-4432",
                        Qualification = "MD, FACP",
                        Exp = 11,
                        Fee = 500.00m,
                        Room = "Suite 104",
                        Bio = "Expertise in complex diagnostics, multi-system chronic disease management, and adult preventive healthcare.",
                        WorkingDays = "Monday,Tuesday,Wednesday,Thursday,Friday",
                        Start = new TimeSpan(8, 30, 0),
                        End = new TimeSpan(16, 30, 0)
                    },
                    new
                    {
                        Email = "doctor.watson@carepulse.com",
                        FullName = "John Watson",
                        DepartmentId = neurDept.Id,
                        Specialization = "Clinical Neurology",
                        License = "LIC-NEU-1123",
                        Qualification = "MD, PhD (Neuroscience)",
                        Exp = 9,
                        Fee = 850.00m,
                        Room = "Suite 205",
                        Bio = "Focusing on migraine disorders, neuro-muscular conditions, stroke prevention, and cognitive assessments.",
                        WorkingDays = "Monday,Wednesday,Thursday,Friday",
                        Start = new TimeSpan(10, 0, 0),
                        End = new TimeSpan(18, 0, 0)
                    },
                    new
                    {
                        Email = "doctor.clara@carepulse.com",
                        FullName = "Clara Oswald",
                        DepartmentId = pedDept.Id,
                        Specialization = "Pediatric Care & Immunization",
                        License = "LIC-PED-5521",
                        Qualification = "MBBS, MD (Pediatrics)",
                        Exp = 7,
                        Fee = 600.00m,
                        Room = "Suite 112",
                        Bio = "Dedicated to pediatric developmental milestones, vaccination scheduling, and adolescent wellness.",
                        WorkingDays = "Tuesday,Wednesday,Thursday,Friday,Saturday",
                        Start = new TimeSpan(9, 0, 0),
                        End = new TimeSpan(15, 0, 0)
                    }
                };

                foreach (var d in doctorSeedData)
                {
                    var docUser = await userManager.FindByEmailAsync(d.Email);
                    if (docUser == null)
                    {
                        docUser = new ApplicationUser
                        {
                            UserName = d.Email,
                            Email = d.Email,
                            FullName = d.FullName,
                            UserRole = "Doctor",
                            EmailConfirmed = true,
                            IsActive = true,
                            CreatedAt = DateTime.UtcNow.AddMonths(-3)
                        };
                        await userManager.CreateAsync(docUser, "Doctor@123");
                        await userManager.AddToRoleAsync(docUser, "Doctor");

                        var doctorProfile = new DoctorProfile
                        {
                            UserId = docUser.Id,
                            DepartmentId = d.DepartmentId,
                            Specialization = d.Specialization,
                            LicenseNumber = d.License,
                            Qualification = d.Qualification,
                            ExperienceYears = d.Exp,
                            ConsultationFee = d.Fee,
                            RoomNumber = d.Room,
                            Bio = d.Bio,
                            IsAvailable = true,
                            WorkingDays = d.WorkingDays,
                            DailyStartTime = d.Start,
                            DailyEndTime = d.End,
                            SlotDurationMinutes = 30
                        };
                        await context.DoctorProfiles.AddAsync(doctorProfile);
                        await context.SaveChangesAsync();
                    }
                }

                // 5. Seed Patients
                var patientSeedData = new[]
                {
                    new
                    {
                        Email = "patient.bruce@carepulse.com",
                        FullName = "Bruce Wayne",
                        BloodGroup = "O+",
                        Allergies = "Penicillin, Latex",
                        History = "Mild hypertension, past clavicle fracture repair.",
                        EmergencyName = "Alfred Pennyworth",
                        EmergencyPhone = "+1 555-0199",
                        Dob = new DateTime(1985, 4, 17),
                        Gender = Gender.Male,
                        Address = "1007 Mountain Drive, Gotham"
                    },
                    new
                    {
                        Email = "patient.diana@carepulse.com",
                        FullName = "Diana Prince",
                        BloodGroup = "A+",
                        Allergies = "Sulfa Antibiotics",
                        History = "No chronic illness; seasonal allergic rhinitis.",
                        EmergencyName = "Hippolyta Prince",
                        EmergencyPhone = "+1 555-0144",
                        Dob = new DateTime(1990, 8, 22),
                        Gender = Gender.Female,
                        Address = "742 Evergreen Terrace, Gateway City"
                    },
                    new
                    {
                        Email = "patient.clark@carepulse.com",
                        FullName = "Clark Kent",
                        BloodGroup = "B+",
                        Allergies = "None reported",
                        History = "Routine executive health checks, active lifestyle.",
                        EmergencyName = "Martha Kent",
                        EmergencyPhone = "+1 555-0188",
                        Dob = new DateTime(1988, 6, 18),
                        Gender = Gender.Male,
                        Address = "344 Clinton St, Apt 3B, Metropolis"
                    }
                };

                foreach (var p in patientSeedData)
                {
                    var patUser = await userManager.FindByEmailAsync(p.Email);
                    if (patUser == null)
                    {
                        patUser = new ApplicationUser
                        {
                            UserName = p.Email,
                            Email = p.Email,
                            FullName = p.FullName,
                            UserRole = "Patient",
                            EmailConfirmed = true,
                            DateOfBirth = p.Dob,
                            Gender = p.Gender,
                            Address = p.Address,
                            IsActive = true,
                            CreatedAt = DateTime.UtcNow.AddMonths(-2)
                        };
                        await userManager.CreateAsync(patUser, "Patient@123");
                        await userManager.AddToRoleAsync(patUser, "Patient");

                        var patientProfile = new PatientProfile
                        {
                            UserId = patUser.Id,
                            BloodGroup = p.BloodGroup,
                            Allergies = p.Allergies,
                            MedicalHistoryNotes = p.History,
                            EmergencyContactName = p.EmergencyName,
                            EmergencyContactPhone = p.EmergencyPhone,
                            InsuranceProvider = "CareHealth Premier",
                            InsurancePolicyNumber = $"POL-{Guid.NewGuid().ToString("N")[..8].ToUpper()}"
                        };
                        await context.PatientProfiles.AddAsync(patientProfile);
                        await context.SaveChangesAsync();
                    }
                }

                // 6. Seed Appointments, Consultations & Prescriptions
                if (!await context.Appointments.AnyAsync())
                {
                    var doctorMeredith = await context.DoctorProfiles.Include(d => d.User).FirstAsync(d => d.Specialization.Contains("General"));
                    var doctorCarter = await context.DoctorProfiles.Include(d => d.User).FirstAsync(d => d.Specialization.Contains("Cardiology"));
                    var patientBruce = await context.PatientProfiles.Include(p => p.User).FirstAsync(p => p.BloodGroup == "O+");
                    var patientDiana = await context.PatientProfiles.Include(p => p.User).FirstAsync(p => p.BloodGroup == "A+");

                    // 1. Completed Consultation with Bruce Wayne
                    var appt1 = new Appointment
                    {
                        AppointmentNumber = "CP-20261101-001",
                        PatientId = patientBruce.Id,
                        DoctorId = doctorMeredith.Id,
                        AppointmentDate = DateTime.Today.AddDays(-2),
                        StartTime = new TimeSpan(10, 0, 0),
                        EndTime = new TimeSpan(10, 30, 0),
                        Status = AppointmentStatus.Completed,
                        ChiefComplaint = "Severe throbbing frontal headache, facial pressure, nasal obstruction for 5 days.",
                        ReasonForVisit = "Acute sinus discomfort not responding to OTC decongestants.",
                        CreatedAt = DateTime.UtcNow.AddDays(-3)
                    };
                    await context.Appointments.AddAsync(appt1);
                    await context.SaveChangesAsync();

                    var consultation1 = new Consultation
                    {
                        AppointmentId = appt1.Id,
                        DoctorId = doctorMeredith.Id,
                        PatientId = patientBruce.Id,
                        ConsultationDate = DateTime.Today.AddDays(-2).AddHours(10).AddMinutes(15),
                        ChiefComplaint = "Severe headache and facial pressure with purulent rhinorrhea.",
                        HistoryOfPresentIllness = "Patient reports onset 5 days ago with progressive nasal congestion and periorbital heaviness. No fever at presentation.",
                        Diagnosis = "Acute Maxillary Bacterial Sinusitis (J01.00)",
                        PhysicalExamination = "Bilateral tenderness on maxillary sinus percussion. Mucosal erythema with turbinate hypertrophy.",
                        BloodPressure = "124/82 mmHg",
                        HeartRateBpm = 74,
                        TemperatureF = 98.8m,
                        RespiratoryRate = 16,
                        SpO2Percentage = 99,
                        WeightKg = 86.5m,
                        HeightCm = 188.0m,
                        DoctorAdvice = "Perform warm saline steam inhalations twice daily. Maintain 2.5L daily hydration. Avoid swimming or dusty environments.",
                        FollowUpDate = DateTime.Today.AddDays(7)
                    };
                    await context.Consultations.AddAsync(consultation1);
                    await context.SaveChangesAsync();

                    var prescription1 = new Prescription
                    {
                        ConsultationId = consultation1.Id,
                        PrescriptionNumber = "RX-2026-00101",
                        IssuedDate = DateTime.Today.AddDays(-2),
                        ValidUntil = DateTime.Today.AddDays(14),
                        GeneralInstructions = "Complete full antibiotic course. Take medications with meals."
                    };
                    await context.Prescriptions.AddAsync(prescription1);
                    await context.SaveChangesAsync();

                    var prescriptionItems = new List<PrescriptionItem>
                    {
                        new() { PrescriptionId = prescription1.Id, MedicineName = "Amoxicillin-Potassium Clavulanate 625mg", Dosage = "625 mg", Frequency = "1-0-1 (Twice Daily)", Duration = "7 Days", Route = "Oral", SpecialInstructions = "Take after breakfast and dinner." },
                        new() { PrescriptionId = prescription1.Id, MedicineName = "Levocetirizine Dihydrochloride 5mg", Dosage = "5 mg", Frequency = "0-0-1 (Once at Night)", Duration = "5 Days", Route = "Oral", SpecialInstructions = "Take at bedtime; may cause mild drowsiness." },
                        new() { PrescriptionId = prescription1.Id, MedicineName = "Hypertonic Saline Nasal Spray", Dosage = "2 Sprays", Frequency = "2 sprays tid (Thrice Daily)", Duration = "10 Days", Route = "Nasal", SpecialInstructions = "Use before inhaling steam." }
                    };
                    await context.PrescriptionItems.AddRangeAsync(prescriptionItems);

                    // 2. Scheduled Appointments for today & upcoming
                    var appt2 = new Appointment
                    {
                        AppointmentNumber = "CP-20261105-002",
                        PatientId = patientDiana.Id,
                        DoctorId = doctorCarter.Id,
                        AppointmentDate = DateTime.Today,
                        StartTime = new TimeSpan(11, 0, 0),
                        EndTime = new TimeSpan(11, 30, 0),
                        Status = AppointmentStatus.Confirmed,
                        ChiefComplaint = "Palpitations during heavy cardiovascular endurance training and occasional dizziness.",
                        ReasonForVisit = "Cardiovascular sports screening and ECG review.",
                        CreatedAt = DateTime.UtcNow.AddDays(-1)
                    };

                    var appt3 = new Appointment
                    {
                        AppointmentNumber = "CP-20261106-003",
                        PatientId = patientBruce.Id,
                        DoctorId = doctorMeredith.Id,
                        AppointmentDate = DateTime.Today.AddDays(1),
                        StartTime = new TimeSpan(14, 0, 0),
                        EndTime = new TimeSpan(14, 30, 0),
                        Status = AppointmentStatus.Confirmed,
                        ChiefComplaint = "Follow-up consultation for sinus symptoms & blood pressure check.",
                        ReasonForVisit = "Sinusitis follow-up review.",
                        CreatedAt = DateTime.UtcNow
                    };

                    await context.Appointments.AddRangeAsync(appt2, appt3);

                    // 3. Attachments
                    var attachment1 = new MedicalRecordAttachment
                    {
                        PatientId = patientBruce.Id,
                        Title = "Maxillary Sinus CT Scan Summary",
                        RecordType = "Radiology Scan",
                        FileUrl = "/img/sample-scan.svg",
                        ContentType = "image/svg+xml",
                        FileSizeBytes = 1048576,
                        UploadedAt = DateTime.UtcNow.AddDays(-2),
                        DoctorNotes = "Demonstrates bilateral mucosal thickening consistent with acute sinusitis. No bone erosion."
                    };
                    await context.MedicalRecordAttachments.AddAsync(attachment1);

                    // 4. Notifications
                    var notifications = new List<Notification>
                    {
                        new() { UserId = patientBruce.UserId, Title = "Prescription Ready for Download", Message = "Dr. Meredith Grey has issued your digital prescription for Maxillary Sinusitis.", Type = NotificationType.PrescriptionReady, IsRead = true, CreatedAt = DateTime.UtcNow.AddDays(-2), LinkUrl = $"/Consultation/Details/{consultation1.Id}" },
                        new() { UserId = patientDiana.UserId, Title = "Appointment Confirmed", Message = $"Your appointment with Dr. John Carter is confirmed for today at 11:00 AM.", Type = NotificationType.AppointmentConfirmed, IsRead = false, CreatedAt = DateTime.UtcNow.AddHours(-3), LinkUrl = "/Appointments" },
                        new() { UserId = doctorCarter.UserId, Title = "New Patient Consultation Booked", Message = "Diana Prince has scheduled an appointment for 11:00 AM today.", Type = NotificationType.AppointmentBooked, IsRead = false, CreatedAt = DateTime.UtcNow.AddHours(-3), LinkUrl = "/Appointments" }
                    };
                    await context.Notifications.AddRangeAsync(notifications);

                    await context.SaveChangesAsync();
                }

                logger.LogInformation("CarePulse clinical database seeded successfully with Admin, Doctors, Patients, Appointments, EHR Records, and Digital Prescriptions.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while seeding the CarePulse database.");
                throw;
            }
        }
    }
}
