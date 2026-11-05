using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using CarePulse.Models.Enums;
using Microsoft.AspNetCore.Identity;

namespace CarePulse.Models.Entities
{
    public class ApplicationUser : IdentityUser
    {
        [Required]
        [MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        [MaxLength(50)]
        public string UserRole { get; set; } = "Patient"; // Admin, Doctor, Patient

        public DateTime? DateOfBirth { get; set; }

        public Gender? Gender { get; set; }

        [MaxLength(250)]
        public string? Address { get; set; }

        [MaxLength(500)]
        public string? ProfilePictureUrl { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsActive { get; set; } = true;

        // Navigation
        public virtual DoctorProfile? DoctorProfile { get; set; }
        public virtual PatientProfile? PatientProfile { get; set; }
        public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    }

    public class Department
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string Code { get; set; } = string.Empty;

        [MaxLength(500)]
        public string Description { get; set; } = string.Empty;

        [MaxLength(50)]
        public string IconClass { get; set; } = "bi-heart-pulse";

        public bool IsActive { get; set; } = true;

        public virtual ICollection<DoctorProfile> Doctors { get; set; } = new List<DoctorProfile>();
    }

    public class DoctorProfile
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [ForeignKey("UserId")]
        public virtual ApplicationUser? User { get; set; }

        public int DepartmentId { get; set; }

        [ForeignKey("DepartmentId")]
        public virtual Department? Department { get; set; }

        [Required]
        [MaxLength(100)]
        public string Specialization { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string LicenseNumber { get; set; } = string.Empty;

        [MaxLength(100)]
        public string Qualification { get; set; } = "MBBS, MD";

        public int ExperienceYears { get; set; } = 5;

        [Column(TypeName = "decimal(18,2)")]
        public decimal ConsultationFee { get; set; } = 500.00m;

        [MaxLength(1000)]
        public string? Bio { get; set; }

        [MaxLength(50)]
        public string RoomNumber { get; set; } = "Room 101";

        public bool IsAvailable { get; set; } = true;

        // Comma-separated days e.g. "Monday,Tuesday,Wednesday,Thursday,Friday"
        [MaxLength(200)]
        public string WorkingDays { get; set; } = "Monday,Tuesday,Wednesday,Thursday,Friday";

        public TimeSpan DailyStartTime { get; set; } = new TimeSpan(9, 0, 0); // 09:00 AM

        public TimeSpan DailyEndTime { get; set; } = new TimeSpan(17, 0, 0); // 05:00 PM

        public int SlotDurationMinutes { get; set; } = 30;

        public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
        public virtual ICollection<Consultation> Consultations { get; set; } = new List<Consultation>();
    }

    public class PatientProfile
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [ForeignKey("UserId")]
        public virtual ApplicationUser? User { get; set; }

        [MaxLength(10)]
        public string BloodGroup { get; set; } = "O+";

        [MaxLength(100)]
        public string? EmergencyContactName { get; set; }

        [MaxLength(20)]
        public string? EmergencyContactPhone { get; set; }

        [MaxLength(500)]
        public string? Allergies { get; set; }

        [MaxLength(1000)]
        public string? MedicalHistoryNotes { get; set; }

        [MaxLength(100)]
        public string? InsuranceProvider { get; set; }

        [MaxLength(50)]
        public string? InsurancePolicyNumber { get; set; }

        public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
        public virtual ICollection<Consultation> Consultations { get; set; } = new List<Consultation>();
        public virtual ICollection<MedicalRecordAttachment> MedicalAttachments { get; set; } = new List<MedicalRecordAttachment>();
    }

    public class Appointment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string AppointmentNumber { get; set; } = string.Empty;

        public int PatientId { get; set; }

        [ForeignKey("PatientId")]
        public virtual PatientProfile? Patient { get; set; }

        public int DoctorId { get; set; }

        [ForeignKey("DoctorId")]
        public virtual DoctorProfile? Doctor { get; set; }

        public DateTime AppointmentDate { get; set; }

        public TimeSpan StartTime { get; set; }

        public TimeSpan EndTime { get; set; }

        public AppointmentStatus Status { get; set; } = AppointmentStatus.Confirmed;

        [Required]
        [MaxLength(250)]
        public string ChiefComplaint { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? ReasonForVisit { get; set; }

        [MaxLength(1000)]
        public string? PatientNotes { get; set; }

        [MaxLength(1000)]
        public string? DoctorNotes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public DateTime? CancelledAt { get; set; }

        [MaxLength(250)]
        public string? CancellationReason { get; set; }

        public virtual Consultation? Consultation { get; set; }
    }

    public class Consultation
    {
        [Key]
        public int Id { get; set; }

        public int AppointmentId { get; set; }

        [ForeignKey("AppointmentId")]
        public virtual Appointment? Appointment { get; set; }

        public int DoctorId { get; set; }

        [ForeignKey("DoctorId")]
        public virtual DoctorProfile? Doctor { get; set; }

        public int PatientId { get; set; }

        [ForeignKey("PatientId")]
        public virtual PatientProfile? Patient { get; set; }

        public DateTime ConsultationDate { get; set; } = DateTime.UtcNow;

        [Required]
        [MaxLength(250)]
        public string ChiefComplaint { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? HistoryOfPresentIllness { get; set; }

        [Required]
        [MaxLength(500)]
        public string Diagnosis { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? PhysicalExamination { get; set; }

        // Vitals
        [MaxLength(20)]
        public string? BloodPressure { get; set; } // e.g. "120/80 mmHg"

        public int? HeartRateBpm { get; set; } // e.g. 72

        [Column(TypeName = "decimal(5,2)")]
        public decimal? TemperatureF { get; set; } // e.g. 98.6

        public int? RespiratoryRate { get; set; } // e.g. 16

        public int? SpO2Percentage { get; set; } // e.g. 99

        [Column(TypeName = "decimal(5,2)")]
        public decimal? WeightKg { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal? HeightCm { get; set; }

        [MaxLength(1500)]
        public string? DoctorAdvice { get; set; }

        public DateTime? FollowUpDate { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual Prescription? Prescription { get; set; }
    }

    public class Prescription
    {
        [Key]
        public int Id { get; set; }

        public int ConsultationId { get; set; }

        [ForeignKey("ConsultationId")]
        public virtual Consultation? Consultation { get; set; }

        [Required]
        [MaxLength(50)]
        public string PrescriptionNumber { get; set; } = string.Empty;

        public DateTime IssuedDate { get; set; } = DateTime.UtcNow;

        public DateTime? ValidUntil { get; set; }

        [MaxLength(1000)]
        public string? GeneralInstructions { get; set; }

        [MaxLength(500)]
        public string? PdfDocumentPath { get; set; }

        public virtual ICollection<PrescriptionItem> Items { get; set; } = new List<PrescriptionItem>();
    }

    public class PrescriptionItem
    {
        [Key]
        public int Id { get; set; }

        public int PrescriptionId { get; set; }

        [ForeignKey("PrescriptionId")]
        public virtual Prescription? Prescription { get; set; }

        [Required]
        [MaxLength(150)]
        public string MedicineName { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Dosage { get; set; } = "500 mg"; // e.g. 500mg, 10ml

        [Required]
        [MaxLength(100)]
        public string Frequency { get; set; } = "1-0-1 (Twice Daily)"; // e.g. 1-0-1 After Food

        [Required]
        [MaxLength(50)]
        public string Duration { get; set; } = "5 Days";

        [MaxLength(50)]
        public string Route { get; set; } = "Oral"; // Oral, Topical, IV, Inhalation

        [MaxLength(250)]
        public string? SpecialInstructions { get; set; }
    }

    public class MedicalRecordAttachment
    {
        [Key]
        public int Id { get; set; }

        public int PatientId { get; set; }

        [ForeignKey("PatientId")]
        public virtual PatientProfile? Patient { get; set; }

        [Required]
        [MaxLength(150)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string RecordType { get; set; } = "Lab Report"; // Lab Report, Radiology, ECG, Discharge Summary

        [Required]
        [MaxLength(500)]
        public string FileUrl { get; set; } = string.Empty;

        [MaxLength(250)]
        public string? BlobName { get; set; }

        [MaxLength(100)]
        public string? ContentType { get; set; }

        public long FileSizeBytes { get; set; }

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(500)]
        public string? DoctorNotes { get; set; }
    }

    public class Notification
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [ForeignKey("UserId")]
        public virtual ApplicationUser? User { get; set; }

        [Required]
        [MaxLength(150)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [MaxLength(1000)]
        public string Message { get; set; } = string.Empty;

        public NotificationType Type { get; set; } = NotificationType.SystemAlert;

        public bool IsRead { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(250)]
        public string? LinkUrl { get; set; }
    }
}
