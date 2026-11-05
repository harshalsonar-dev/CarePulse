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
    public class AppointmentsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ISchedulingService _schedulingService;
        private readonly INotificationService _notificationService;
        private readonly ILogger<AppointmentsController> _logger;

        public AppointmentsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ISchedulingService schedulingService,
            INotificationService notificationService,
            ILogger<AppointmentsController> logger)
        {
            _context = context;
            _userManager = userManager;
            _schedulingService = schedulingService;
            _notificationService = notificationService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? statusFilter, string? dateFilter)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var query = _context.Appointments
                .Include(a => a.Doctor).ThenInclude(d => d!.User)
                .Include(a => a.Doctor).ThenInclude(d => d!.Department)
                .Include(a => a.Patient).ThenInclude(p => p!.User)
                .Include(a => a.Consultation).ThenInclude(c => c!.Prescription)
                .AsQueryable();

            if (User.IsInRole("Doctor"))
            {
                var doctor = await _context.DoctorProfiles.FirstOrDefaultAsync(d => d.UserId == user.Id);
                if (doctor == null) return Forbid();
                query = query.Where(a => a.DoctorId == doctor.Id);
            }
            else if (User.IsInRole("Patient"))
            {
                var patient = await _context.PatientProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id);
                if (patient == null) return Forbid();
                query = query.Where(a => a.PatientId == patient.Id);
            }
            // Admin sees all appointments

            // Apply filters
            if (!string.IsNullOrEmpty(statusFilter) && Enum.TryParse<AppointmentStatus>(statusFilter, true, out var status))
            {
                query = query.Where(a => a.Status == status);
            }

            if (dateFilter == "today")
            {
                query = query.Where(a => a.AppointmentDate.Date == DateTime.Today);
            }
            else if (dateFilter == "upcoming")
            {
                query = query.Where(a => a.AppointmentDate.Date >= DateTime.Today && a.Status == AppointmentStatus.Confirmed);
            }

            var appointments = await query
                .OrderByDescending(a => a.AppointmentDate)
                .ThenBy(a => a.StartTime)
                .ToListAsync();

            ViewBag.StatusFilter = statusFilter;
            ViewBag.DateFilter = dateFilter;

            return View(appointments);
        }

        [HttpGet]
        public async Task<IActionResult> Book(int? doctorId, string? date)
        {
            var model = new BookAppointmentViewModel
            {
                Departments = await _context.Departments.Where(d => d.IsActive).ToListAsync(),
                Doctors = await _context.DoctorProfiles
                    .Include(d => d.User)
                    .Include(d => d.Department)
                    .Where(d => d.IsAvailable)
                    .ToListAsync(),
                AppointmentDate = !string.IsNullOrEmpty(date) && DateTime.TryParse(date, out var parsedDate) 
                    ? parsedDate 
                    : DateTime.Today.AddDays(1)
            };

            if (doctorId.HasValue)
            {
                model.DoctorId = doctorId.Value;
                model.SelectedDoctor = await _context.DoctorProfiles
                    .Include(d => d.User)
                    .Include(d => d.Department)
                    .FirstOrDefaultAsync(d => d.Id == doctorId.Value);
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Book(BookAppointmentViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            // Resolve Patient
            int patientId;
            if (User.IsInRole("Patient"))
            {
                var patientProfile = await _context.PatientProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id);
                if (patientProfile == null)
                {
                    ModelState.AddModelError(string.Empty, "Patient profile not found. Please complete your profile first.");
                    await ReloadDropdownsAsync(model);
                    return View(model);
                }
                patientId = patientProfile.Id;
            }
            else if (model.PatientId.HasValue)
            {
                patientId = model.PatientId.Value;
            }
            else
            {
                // Default fallback if admin books for first patient
                var firstPatient = await _context.PatientProfiles.FirstOrDefaultAsync();
                if (firstPatient == null)
                {
                    ModelState.AddModelError(string.Empty, "No patient profiles available in system.");
                    await ReloadDropdownsAsync(model);
                    return View(model);
                }
                patientId = firstPatient.Id;
            }

            // Parse time slot: Expected format "HH:mm-HH:mm" e.g. "09:00-09:30"
            if (string.IsNullOrWhiteSpace(model.SelectedTimeSlot) || !model.SelectedTimeSlot.Contains('-'))
            {
                ModelState.AddModelError("SelectedTimeSlot", "Please select a valid consultation time slot.");
                await ReloadDropdownsAsync(model);
                return View(model);
            }

            var parts = model.SelectedTimeSlot.Split('-');
            if (!TimeSpan.TryParse(parts[0], out var startTime) || !TimeSpan.TryParse(parts[1], out var endTime))
            {
                ModelState.AddModelError("SelectedTimeSlot", "Invalid time slot format.");
                await ReloadDropdownsAsync(model);
                return View(model);
            }

            // Conflict Prevention Logic Check
            var conflictCheck = await _schedulingService.ValidateBookingConflictAsync(
                model.DoctorId,
                patientId,
                model.AppointmentDate,
                startTime,
                endTime
            );

            if (conflictCheck.HasConflict)
            {
                ModelState.AddModelError(string.Empty, $"Scheduling Conflict: {conflictCheck.ConflictReason}");
                await ReloadDropdownsAsync(model);
                return View(model);
            }

            // Create Appointment
            var appointmentNumber = await _schedulingService.GenerateAppointmentNumberAsync();

            var appointment = new Appointment
            {
                AppointmentNumber = appointmentNumber,
                DoctorId = model.DoctorId,
                PatientId = patientId,
                AppointmentDate = model.AppointmentDate.Date,
                StartTime = startTime,
                EndTime = endTime,
                Status = AppointmentStatus.Confirmed,
                ChiefComplaint = model.ChiefComplaint,
                PatientNotes = model.PatientNotes,
                CreatedAt = DateTime.UtcNow
            };

            await _context.Appointments.AddAsync(appointment);
            await _context.SaveChangesAsync();

            // Background & Email Notification Dispatch
            await _notificationService.NotifyAppointmentBookedAsync(appointment);

            _logger.LogInformation("Appointment {AppointmentNumber} successfully scheduled for Patient ID {PatientId} with Doctor ID {DoctorId}",
                appointmentNumber, patientId, model.DoctorId);

            TempData["SuccessMessage"] = $"Appointment {appointmentNumber} booked successfully for {appointment.AppointmentDate:dd MMM yyyy} at {DateTime.Today.Add(startTime):hh:mm tt}!";
            return RedirectToAction(nameof(Details), new { id = appointment.Id });
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var appointment = await _context.Appointments
                .Include(a => a.Doctor).ThenInclude(d => d!.User)
                .Include(a => a.Doctor).ThenInclude(d => d!.Department)
                .Include(a => a.Patient).ThenInclude(p => p!.User)
                .Include(a => a.Consultation).ThenInclude(c => c!.Prescription)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (appointment == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            // RBAC Check
            if (User.IsInRole("Patient") && appointment.Patient?.UserId != user.Id)
            {
                return Forbid();
            }
            if (User.IsInRole("Doctor") && appointment.Doctor?.UserId != user.Id && !User.IsInRole("Admin"))
            {
                return Forbid();
            }

            var viewModel = new AppointmentDetailsViewModel
            {
                Appointment = appointment,
                CanCancel = appointment.Status == AppointmentStatus.Confirmed || appointment.Status == AppointmentStatus.Pending,
                CanConsult = (User.IsInRole("Doctor") || User.IsInRole("Admin")) && appointment.Status != AppointmentStatus.Cancelled,
                HasPrescription = appointment.Consultation?.Prescription != null
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id, string reason)
        {
            var appointment = await _context.Appointments
                .Include(a => a.Doctor).ThenInclude(d => d!.User)
                .Include(a => a.Patient).ThenInclude(p => p!.User)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (appointment == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            // RBAC Authorization
            if (User.IsInRole("Patient") && appointment.Patient?.UserId != user.Id)
            {
                return Forbid();
            }
            if (User.IsInRole("Doctor") && appointment.Doctor?.UserId != user.Id && !User.IsInRole("Admin"))
            {
                return Forbid();
            }

            if (appointment.Status == AppointmentStatus.Completed)
            {
                TempData["ErrorMessage"] = "Cannot cancel an appointment that has already been completed.";
                return RedirectToAction(nameof(Details), new { id });
            }

            appointment.Status = AppointmentStatus.Cancelled;
            appointment.CancelledAt = DateTime.UtcNow;
            appointment.CancellationReason = string.IsNullOrWhiteSpace(reason) ? "Cancelled by user" : reason;
            appointment.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Dispatch cancellation notification
            await _notificationService.NotifyAppointmentCancelledAsync(appointment, appointment.CancellationReason);

            _logger.LogInformation("Appointment {AppointmentNumber} was cancelled by {UserName}. Reason: {Reason}",
                appointment.AppointmentNumber, user.UserName, appointment.CancellationReason);

            TempData["SuccessMessage"] = $"Appointment {appointment.AppointmentNumber} has been successfully cancelled.";
            return RedirectToAction(nameof(Details), new { id });
        }

        private async Task ReloadDropdownsAsync(BookAppointmentViewModel model)
        {
            model.Departments = await _context.Departments.Where(d => d.IsActive).ToListAsync();
            model.Doctors = await _context.DoctorProfiles
                .Include(d => d.User)
                .Include(d => d.Department)
                .Where(d => d.IsAvailable)
                .ToListAsync();

            if (model.DoctorId > 0)
            {
                model.SelectedDoctor = await _context.DoctorProfiles
                    .Include(d => d.User)
                    .Include(d => d.Department)
                    .FirstOrDefaultAsync(d => d.Id == model.DoctorId);
            }
        }
    }
}
