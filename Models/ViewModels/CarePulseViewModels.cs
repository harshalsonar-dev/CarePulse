using System.ComponentModel.DataAnnotations;
using CarePulse.Models.Entities;
using CarePulse.Models.Enums;
using Microsoft.AspNetCore.Http;

namespace CarePulse.Models.ViewModels
{
    // Auth View Models
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Remember Me")]
        public bool RememberMe { get; set; }

        public string? ReturnUrl { get; set; }
    }

    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Full Name is required")]
        [Display(Name = "Full Name")]
        [MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone Number is required")]
        [Phone]
        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters")]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Confirm Password")]
        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Display(Name = "Date of Birth")]
        [DataType(DataType.Date)]
        public DateTime? DateOfBirth { get; set; }

        public Gender? Gender { get; set; }

        [Display(Name = "Blood Group")]
        public string BloodGroup { get; set; } = "O+";

        [Display(Name = "Emergency Contact Name")]
        public string? EmergencyContactName { get; set; }

        [Display(Name = "Emergency Contact Phone")]
        [Phone]
        public string? EmergencyContactPhone { get; set; }

        [Display(Name = "Known Allergies")]
        public string? Allergies { get; set; }

        public string? Address { get; set; }
    }

    // Appointment View Models
    public class BookAppointmentViewModel
    {
        [Required(ErrorMessage = "Please select a Doctor")]
        public int DoctorId { get; set; }

        public int? PatientId { get; set; }

        [Required(ErrorMessage = "Please select a Date")]
        [DataType(DataType.Date)]
        [Display(Name = "Appointment Date")]
        public DateTime AppointmentDate { get; set; } = DateTime.Today.AddDays(1);

        [Required(ErrorMessage = "Please select a Time Slot")]
        [Display(Name = "Time Slot")]
        public string SelectedTimeSlot { get; set; } = string.Empty; // Format: "09:00-09:30"

        [Required(ErrorMessage = "Chief Complaint is required")]
        [Display(Name = "Chief Complaint / Symptoms")]
        [MaxLength(250)]
        public string ChiefComplaint { get; set; } = string.Empty;

        [Display(Name = "Additional Notes (Optional)")]
        [MaxLength(1000)]
        public string? PatientNotes { get; set; }

        // Display helper properties
        public DoctorProfile? SelectedDoctor { get; set; }
        public List<Department>? Departments { get; set; }
        public List<DoctorProfile>? Doctors { get; set; }
    }

    public class AppointmentDetailsViewModel
    {
        public Appointment Appointment { get; set; } = null!;
        public bool CanCancel { get; set; }
        public bool CanConsult { get; set; }
        public bool HasPrescription { get; set; }
    }

    // Consultation & Prescription View Models
    public class CreateConsultationViewModel
    {
        public int AppointmentId { get; set; }
        public Appointment? Appointment { get; set; }

        [Required(ErrorMessage = "Chief complaint is required")]
        [Display(Name = "Chief Complaint")]
        public string ChiefComplaint { get; set; } = string.Empty;

        [Display(Name = "Clinical Notes / History")]
        public string? HistoryOfPresentIllness { get; set; }

        [Required(ErrorMessage = "Diagnosis is required")]
        [Display(Name = "Clinical Diagnosis")]
        public string Diagnosis { get; set; } = string.Empty;

        [Display(Name = "Physical Examination Findings")]
        public string? PhysicalExamination { get; set; }

        // Vitals
        [Display(Name = "Blood Pressure (e.g. 120/80 mmHg)")]
        public string? BloodPressure { get; set; } = "120/80";

        [Display(Name = "Heart Rate (bpm)")]
        public int? HeartRateBpm { get; set; } = 75;

        [Display(Name = "Temperature (°F)")]
        public decimal? TemperatureF { get; set; } = 98.6m;

        [Display(Name = "Respiratory Rate")]
        public int? RespiratoryRate { get; set; } = 16;

        [Display(Name = "SpO2 (%)")]
        public int? SpO2Percentage { get; set; } = 98;

        [Display(Name = "Weight (kg)")]
        public decimal? WeightKg { get; set; }

        [Display(Name = "Height (cm)")]
        public decimal? HeightCm { get; set; }

        [Display(Name = "Special Advice & Diet Instructions")]
        public string? DoctorAdvice { get; set; }

        [Display(Name = "Follow-up Date")]
        [DataType(DataType.Date)]
        public DateTime? FollowUpDate { get; set; }

        // Dynamic Prescription Items
        public List<PrescriptionItemInputModel> Medicines { get; set; } = new List<PrescriptionItemInputModel>();
    }

    public class PrescriptionItemInputModel
    {
        public string MedicineName { get; set; } = string.Empty;
        public string Dosage { get; set; } = "500 mg";
        public string Frequency { get; set; } = "1-0-1 (After Food)";
        public string Duration { get; set; } = "5 Days";
        public string Route { get; set; } = "Oral";
        public string? SpecialInstructions { get; set; }
    }

    // EHR View Models
    public class PatientEhrViewModel
    {
        public PatientProfile Patient { get; set; } = null!;
        public List<Appointment> Appointments { get; set; } = new List<Appointment>();
        public List<Consultation> Consultations { get; set; } = new List<Consultation>();
        public List<MedicalRecordAttachment> Attachments { get; set; } = new List<MedicalRecordAttachment>();
        public bool IsDoctorOrAdmin { get; set; }
    }

    public class UploadAttachmentViewModel
    {
        public int PatientId { get; set; }

        [Required(ErrorMessage = "Title is required")]
        [MaxLength(150)]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Record type is required")]
        public string RecordType { get; set; } = "Lab Report";

        [Display(Name = "Clinical Notes")]
        public string? DoctorNotes { get; set; }

        [Required(ErrorMessage = "Please select a file to upload")]
        public IFormFile File { get; set; } = null!;
    }

    // Dashboard View Models
    public class AdminDashboardViewModel
    {
        public int TotalDoctors { get; set; }
        public int TotalPatients { get; set; }
        public int TotalAppointments { get; set; }
        public int TodayAppointmentsCount { get; set; }
        public int CompletedConsultationsCount { get; set; }
        public List<Appointment> RecentAppointments { get; set; } = new List<Appointment>();
        public List<DoctorProfile> Doctors { get; set; } = new List<DoctorProfile>();
        public List<Department> Departments { get; set; } = new List<Department>();
    }

    public class DoctorDashboardViewModel
    {
        public DoctorProfile Doctor { get; set; } = null!;
        public int TodayAppointmentsCount { get; set; }
        public int PendingAppointmentsCount { get; set; }
        public int TotalCompletedCount { get; set; }
        public List<Appointment> TodayAppointments { get; set; } = new List<Appointment>();
        public List<Appointment> UpcomingAppointments { get; set; } = new List<Appointment>();
    }

    public class PatientDashboardViewModel
    {
        public PatientProfile Patient { get; set; } = null!;
        public List<Appointment> UpcomingAppointments { get; set; } = new List<Appointment>();
        public List<Appointment> PastAppointments { get; set; } = new List<Appointment>();
        public List<Prescription> Prescriptions { get; set; } = new List<Prescription>();
        public List<MedicalRecordAttachment> RecentLabReports { get; set; } = new List<MedicalRecordAttachment>();
    }
}
