using CarePulse.Data;
using CarePulse.Models.Entities;
using CarePulse.Models.Enums;
using CarePulse.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarePulse.Controllers.Api
{
    public class BookAppointmentApiRequest
    {
        public int DoctorId { get; set; }
        public int PatientId { get; set; }
        public DateTime AppointmentDate { get; set; }
        public string StartTime { get; set; } = string.Empty; // e.g. "09:00"
        public string EndTime { get; set; } = string.Empty;   // e.g. "09:30"
        public string ChiefComplaint { get; set; } = string.Empty;
        public string? PatientNotes { get; set; }
    }

    [ApiController]
    [Route("api/v1/appointments")]
    [Produces("application/json")]
    public class AppointmentsApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ISchedulingService _schedulingService;
        private readonly INotificationService _notificationService;

        public AppointmentsApiController(
            ApplicationDbContext context,
            ISchedulingService schedulingService,
            INotificationService notificationService)
        {
            _context = context;
            _schedulingService = schedulingService;
            _notificationService = notificationService;
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetAppointment(int id)
        {
            var appt = await _context.Appointments
                .Include(a => a.Doctor).ThenInclude(d => d!.User)
                .Include(a => a.Patient).ThenInclude(p => p!.User)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (appt == null) return NotFound(new { message = "Appointment not found." });

            return Ok(new
            {
                appt.Id,
                appt.AppointmentNumber,
                appt.AppointmentDate,
                StartTime = appt.StartTime.ToString(@"hh\:mm"),
                EndTime = appt.EndTime.ToString(@"hh\:mm"),
                Status = appt.Status.ToString(),
                Doctor = appt.Doctor?.User?.FullName,
                Patient = appt.Patient?.User?.FullName,
                appt.ChiefComplaint
            });
        }

        [HttpPost]
        public async Task<IActionResult> BookAppointment([FromBody] BookAppointmentApiRequest request)
        {
            if (!TimeSpan.TryParse(request.StartTime, out var startTime) || !TimeSpan.TryParse(request.EndTime, out var endTime))
            {
                return BadRequest(new { message = "Invalid StartTime or EndTime format. Expected HH:mm." });
            }

            // Conflict check
            var conflict = await _schedulingService.ValidateBookingConflictAsync(
                request.DoctorId,
                request.PatientId,
                request.AppointmentDate,
                startTime,
                endTime
            );

            if (conflict.HasConflict)
            {
                return Conflict(new { message = conflict.ConflictReason });
            }

            var apptNumber = await _schedulingService.GenerateAppointmentNumberAsync();
            var appt = new Appointment
            {
                AppointmentNumber = apptNumber,
                DoctorId = request.DoctorId,
                PatientId = request.PatientId,
                AppointmentDate = request.AppointmentDate.Date,
                StartTime = startTime,
                EndTime = endTime,
                Status = AppointmentStatus.Confirmed,
                ChiefComplaint = request.ChiefComplaint,
                PatientNotes = request.PatientNotes,
                CreatedAt = DateTime.UtcNow
            };

            await _context.Appointments.AddAsync(appt);
            await _context.SaveChangesAsync();

            await _notificationService.NotifyAppointmentBookedAsync(appt);

            return CreatedAtAction(nameof(GetAppointment), new { id = appt.Id }, new
            {
                appt.Id,
                appt.AppointmentNumber,
                message = "Appointment scheduled successfully."
            });
        }

        [HttpPost("{id}/cancel")]
        public async Task<IActionResult> CancelAppointment(int id, [FromBody] string? reason)
        {
            var appt = await _context.Appointments.FindAsync(id);
            if (appt == null) return NotFound(new { message = "Appointment not found." });

            if (appt.Status == AppointmentStatus.Completed)
            {
                return BadRequest(new { message = "Cannot cancel completed appointment." });
            }

            appt.Status = AppointmentStatus.Cancelled;
            appt.CancelledAt = DateTime.UtcNow;
            appt.CancellationReason = string.IsNullOrWhiteSpace(reason) ? "Cancelled via API" : reason;
            await _context.SaveChangesAsync();

            await _notificationService.NotifyAppointmentCancelledAsync(appt, appt.CancellationReason);

            return Ok(new { message = "Appointment successfully cancelled.", appointmentId = id });
        }
    }
}
