using CarePulse.Data;
using CarePulse.Models.Entities;
using CarePulse.Models.Enums;
using CarePulse.Models.ViewModels;
using CarePulse.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarePulse.Controllers
{
    [Authorize]
    public class ConsultationController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IPrescriptionPdfService _prescriptionPdfService;
        private readonly INotificationService _notificationService;
        private readonly ILogger<ConsultationController> _logger;

        public ConsultationController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IPrescriptionPdfService prescriptionPdfService,
            INotificationService notificationService,
            ILogger<ConsultationController> logger)
        {
            _context = context;
            _userManager = userManager;
            _prescriptionPdfService = prescriptionPdfService;
            _notificationService = notificationService;
            _logger = logger;
        }

        [HttpGet]
        [Authorize(Roles = "Doctor,Admin")]
        public async Task<IActionResult> Create(int appointmentId)
        {
            var appointment = await _context.Appointments
                .Include(a => a.Doctor).ThenInclude(d => d!.User)
                .Include(a => a.Patient).ThenInclude(p => p!.User)
                .Include(a => a.Consultation)
                .FirstOrDefaultAsync(a => a.Id == appointmentId);

            if (appointment == null) return NotFound();

            // If consultation already exists, redirect to it
            if (appointment.Consultation != null)
            {
                return RedirectToAction(nameof(Details), new { id = appointment.Consultation.Id });
            }

            var model = new CreateConsultationViewModel
            {
                AppointmentId = appointment.Id,
                Appointment = appointment,
                ChiefComplaint = appointment.ChiefComplaint,
                BloodPressure = "120/80",
                HeartRateBpm = 72,
                TemperatureF = 98.6m,
                SpO2Percentage = 99,
                Medicines = new List<PrescriptionItemInputModel>
                {
                    new() { MedicineName = "", Dosage = "500 mg", Frequency = "1-0-1 (After Food)", Duration = "5 Days", Route = "Oral" }
                }
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Doctor,Admin")]
        public async Task<IActionResult> Create(CreateConsultationViewModel model)
        {
            var appointment = await _context.Appointments
                .Include(a => a.Doctor)
                .Include(a => a.Patient).ThenInclude(p => p!.User)
                .FirstOrDefaultAsync(a => a.Id == model.AppointmentId);

            if (appointment == null) return NotFound();

            if (!ModelState.IsValid)
            {
                model.Appointment = appointment;
                return View(model);
            }

            // 1. Create Consultation
            var consultation = new Consultation
            {
                AppointmentId = appointment.Id,
                DoctorId = appointment.DoctorId,
                PatientId = appointment.PatientId,
                ConsultationDate = DateTime.UtcNow,
                ChiefComplaint = model.ChiefComplaint,
                HistoryOfPresentIllness = model.HistoryOfPresentIllness,
                Diagnosis = model.Diagnosis,
                PhysicalExamination = model.PhysicalExamination,
                BloodPressure = model.BloodPressure,
                HeartRateBpm = model.HeartRateBpm,
                TemperatureF = model.TemperatureF,
                RespiratoryRate = model.RespiratoryRate,
                SpO2Percentage = model.SpO2Percentage,
                WeightKg = model.WeightKg,
                HeightCm = model.HeightCm,
                DoctorAdvice = model.DoctorAdvice,
                FollowUpDate = model.FollowUpDate,
                CreatedAt = DateTime.UtcNow
            };

            await _context.Consultations.AddAsync(consultation);
            await _context.SaveChangesAsync();

            // 2. Create Digital Prescription
            var rxNumber = $"RX-{DateTime.UtcNow:yyyy}-{(await _context.Prescriptions.CountAsync() + 1):D4}";
            var prescription = new Prescription
            {
                ConsultationId = consultation.Id,
                PrescriptionNumber = rxNumber,
                IssuedDate = DateTime.UtcNow,
                ValidUntil = model.FollowUpDate ?? DateTime.UtcNow.AddDays(30),
                GeneralInstructions = model.DoctorAdvice
            };

            await _context.Prescriptions.AddAsync(prescription);
            await _context.SaveChangesAsync();

            // 3. Add Prescription Items
            if (model.Medicines != null && model.Medicines.Any())
            {
                foreach (var item in model.Medicines.Where(m => !string.IsNullOrWhiteSpace(m.MedicineName)))
                {
                    var prescriptionItem = new PrescriptionItem
                    {
                        PrescriptionId = prescription.Id,
                        MedicineName = item.MedicineName.Trim(),
                        Dosage = item.Dosage?.Trim() ?? "500 mg",
                        Frequency = item.Frequency?.Trim() ?? "1-0-1",
                        Duration = item.Duration?.Trim() ?? "5 Days",
                        Route = item.Route?.Trim() ?? "Oral",
                        SpecialInstructions = item.SpecialInstructions?.Trim()
                    };
                    await _context.PrescriptionItems.AddAsync(prescriptionItem);
                }
                await _context.SaveChangesAsync();
            }

            // 4. Update Appointment Status
            var oldStatus = appointment.Status;
            appointment.Status = AppointmentStatus.Completed;
            appointment.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // 5. Notify Patient
            await _notificationService.NotifyAppointmentStatusChangedAsync(appointment, oldStatus, AppointmentStatus.Completed);

            _logger.LogInformation("Consultation {ConsultationId} and Prescription {RxNumber} recorded for Appointment {AppointmentNumber}",
                consultation.Id, rxNumber, appointment.AppointmentNumber);

            TempData["SuccessMessage"] = $"Consultation completed and Digital Prescription {rxNumber} generated successfully!";
            return RedirectToAction(nameof(Details), new { id = consultation.Id });
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var consultation = await _context.Consultations
                .Include(c => c.Doctor).ThenInclude(d => d!.User)
                .Include(c => c.Doctor).ThenInclude(d => d!.Department)
                .Include(c => c.Patient).ThenInclude(p => p!.User)
                .Include(c => c.Appointment)
                .Include(c => c.Prescription).ThenInclude(p => p!.Items)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (consultation == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            // Strict RBAC: Only attending Doctor, Patient, or Admin can access
            if (User.IsInRole("Patient") && consultation.Patient?.UserId != user.Id)
            {
                return Forbid();
            }
            if (User.IsInRole("Doctor") && consultation.Doctor?.UserId != user.Id && !User.IsInRole("Admin"))
            {
                return Forbid();
            }

            return View(consultation);
        }

        [HttpGet]
        public async Task<IActionResult> DownloadPrescriptionPdf(int id)
        {
            var consultation = await _context.Consultations
                .Include(c => c.Doctor)
                .Include(c => c.Patient)
                .Include(c => c.Prescription)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (consultation == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            // RBAC Check
            if (User.IsInRole("Patient") && consultation.Patient?.UserId != user.Id)
            {
                return Forbid();
            }
            if (User.IsInRole("Doctor") && consultation.Doctor?.UserId != user.Id && !User.IsInRole("Admin"))
            {
                return Forbid();
            }

            try
            {
                var pdfBytes = await _prescriptionPdfService.GeneratePrescriptionPdfAsync(id);
                var fileName = $"Prescription_{consultation.Prescription?.PrescriptionNumber ?? $"RX-{id}"}.pdf";

                // Return inline so browser displays it directly or allows download
                Response.Headers.Append("Content-Disposition", $"inline; filename=\"{fileName}\"");
                return File(pdfBytes, "application/pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate PDF for consultation {Id}", id);
                TempData["ErrorMessage"] = "Could not generate PDF summary. Please try again.";
                return RedirectToAction(nameof(Details), new { id });
            }
        }
    }
}
