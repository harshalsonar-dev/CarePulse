using CarePulse.Data;
using CarePulse.Models.Entities;
using CarePulse.Models.ViewModels;
using CarePulse.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarePulse.Controllers
{
    [Authorize]
    public class EhrController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAzureBlobStorageService _blobService;
        private readonly ILogger<EhrController> _logger;

        public EhrController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IAzureBlobStorageService blobService,
            ILogger<EhrController> logger)
        {
            _context = context;
            _userManager = userManager;
            _blobService = blobService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> MyRecord()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var patient = await _context.PatientProfiles
                .Include(p => p.User)
                .Include(p => p.Appointments)
                .Include(p => p.Consultations).ThenInclude(c => c.Doctor).ThenInclude(d => d!.User)
                .Include(p => p.Consultations).ThenInclude(c => c.Prescription).ThenInclude(rx => rx!.Items)
                .Include(p => p.MedicalAttachments)
                .FirstOrDefaultAsync(p => p.UserId == user.Id);

            if (patient == null)
            {
                if (User.IsInRole("Admin") || User.IsInRole("Doctor"))
                {
                    return RedirectToAction(nameof(PatientsList));
                }
                return NotFound("Patient profile record could not be found.");
            }

            var viewModel = new PatientEhrViewModel
            {
                Patient = patient,
                Appointments = patient.Appointments.OrderByDescending(a => a.AppointmentDate).ToList(),
                Consultations = patient.Consultations.OrderByDescending(c => c.ConsultationDate).ToList(),
                Attachments = patient.MedicalAttachments.OrderByDescending(m => m.UploadedAt).ToList(),
                IsDoctorOrAdmin = false
            };

            return View("RecordView", viewModel);
        }

        [HttpGet]
        [Authorize(Roles = "Doctor,Admin")]
        public async Task<IActionResult> Patient(int id)
        {
            var patient = await _context.PatientProfiles
                .Include(p => p.User)
                .Include(p => p.Appointments).ThenInclude(a => a.Doctor).ThenInclude(d => d!.User)
                .Include(p => p.Consultations).ThenInclude(c => c.Doctor).ThenInclude(d => d!.User)
                .Include(p => p.Consultations).ThenInclude(c => c.Prescription).ThenInclude(rx => rx!.Items)
                .Include(p => p.MedicalAttachments)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (patient == null) return NotFound();

            var viewModel = new PatientEhrViewModel
            {
                Patient = patient,
                Appointments = patient.Appointments.OrderByDescending(a => a.AppointmentDate).ToList(),
                Consultations = patient.Consultations.OrderByDescending(c => c.ConsultationDate).ToList(),
                Attachments = patient.MedicalAttachments.OrderByDescending(m => m.UploadedAt).ToList(),
                IsDoctorOrAdmin = true
            };

            return View("RecordView", viewModel);
        }

        [HttpGet]
        [Authorize(Roles = "Doctor,Admin")]
        public async Task<IActionResult> PatientsList(string? search)
        {
            var query = _context.PatientProfiles
                .Include(p => p.User)
                .Include(p => p.Appointments)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(p => (p.User != null && p.User.FullName.Contains(search)) ||
                                         (p.User != null && p.User.Email!.Contains(search)) ||
                                         p.BloodGroup.Contains(search));
            }

            var patients = await query.ToListAsync();
            ViewBag.SearchTerm = search;
            return View(patients);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadAttachment(UploadAttachmentViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var patient = await _context.PatientProfiles.FirstOrDefaultAsync(p => p.Id == model.PatientId);
            if (patient == null) return NotFound();

            // RBAC Check: Only the patient or medical staff can upload
            if (User.IsInRole("Patient") && patient.UserId != user.Id)
            {
                return Forbid();
            }

            if (model.File == null || model.File.Length == 0)
            {
                TempData["ErrorMessage"] = "Please select a valid file to upload.";
                return RedirectToAction(User.IsInRole("Patient") ? nameof(MyRecord) : nameof(Patient), new { id = model.PatientId });
            }

            using var stream = model.File.OpenReadStream();
            var uploadResult = await _blobService.UploadMedicalDocumentAsync(
                stream,
                model.File.FileName,
                model.File.ContentType
            );

            if (!uploadResult.Success)
            {
                TempData["ErrorMessage"] = $"Upload failed: {uploadResult.ErrorMessage}";
                return RedirectToAction(User.IsInRole("Patient") ? nameof(MyRecord) : nameof(Patient), new { id = model.PatientId });
            }

            var attachment = new MedicalRecordAttachment
            {
                PatientId = model.PatientId,
                Title = model.Title,
                RecordType = model.RecordType,
                FileUrl = uploadResult.FileUrl,
                BlobName = uploadResult.BlobName,
                ContentType = uploadResult.ContentType,
                FileSizeBytes = uploadResult.FileSize,
                UploadedAt = DateTime.UtcNow,
                DoctorNotes = model.DoctorNotes
            };

            await _context.MedicalRecordAttachments.AddAsync(attachment);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Medical attachment {Title} uploaded for patient ID {PatientId}", model.Title, model.PatientId);
            TempData["SuccessMessage"] = "Medical document uploaded and attached to health record successfully.";

            return RedirectToAction(User.IsInRole("Patient") ? nameof(MyRecord) : nameof(Patient), new { id = model.PatientId });
        }
    }
}
