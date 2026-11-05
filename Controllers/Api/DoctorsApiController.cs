using CarePulse.Data;
using CarePulse.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarePulse.Controllers.Api
{
    [ApiController]
    [Route("api/v1/doctors")]
    [Produces("application/json")]
    public class DoctorsApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ISchedulingService _schedulingService;

        public DoctorsApiController(ApplicationDbContext context, ISchedulingService schedulingService)
        {
            _context = context;
            _schedulingService = schedulingService;
        }

        /// <summary>
        /// Retrieves list of all active doctors with specialties and consultation rates.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetDoctors([FromQuery] int? departmentId)
        {
            var query = _context.DoctorProfiles
                .Include(d => d.User)
                .Include(d => d.Department)
                .Where(d => d.IsAvailable)
                .AsQueryable();

            if (departmentId.HasValue)
            {
                query = query.Where(d => d.DepartmentId == departmentId.Value);
            }

            var doctors = await query.Select(d => new
            {
                d.Id,
                FullName = d.User != null ? d.User.FullName : "Physician",
                Email = d.User != null ? d.User.Email : "",
                d.Specialization,
                d.Qualification,
                d.ExperienceYears,
                d.ConsultationFee,
                Department = d.Department != null ? d.Department.Name : "",
                d.RoomNumber,
                d.WorkingDays,
                DailyStart = d.DailyStartTime.ToString(@"hh\:mm"),
                DailyEnd = d.DailyEndTime.ToString(@"hh\:mm"),
                d.SlotDurationMinutes
            }).ToListAsync();

            return Ok(doctors);
        }

        /// <summary>
        /// Retrieves single doctor details by ID.
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetDoctorById(int id)
        {
            var doctor = await _context.DoctorProfiles
                .Include(d => d.User)
                .Include(d => d.Department)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (doctor == null) return NotFound(new { message = $"Doctor with ID {id} was not found." });

            return Ok(new
            {
                doctor.Id,
                FullName = doctor.User?.FullName,
                doctor.Specialization,
                doctor.Qualification,
                doctor.ExperienceYears,
                doctor.ConsultationFee,
                Department = doctor.Department?.Name,
                doctor.RoomNumber,
                doctor.Bio,
                doctor.WorkingDays,
                DailyStart = doctor.DailyStartTime.ToString(@"hh\:mm"),
                DailyEnd = doctor.DailyEndTime.ToString(@"hh\:mm"),
                doctor.SlotDurationMinutes
            });
        }

        /// <summary>
        /// Real-time slot availability calendar endpoint for a doctor on a specific date.
        /// </summary>
        [HttpGet("{id}/availability")]
        public async Task<IActionResult> GetDoctorAvailability(int id, [FromQuery] string date)
        {
            if (!DateTime.TryParse(date, out var parsedDate))
            {
                return BadRequest(new { message = "Invalid date format. Expected YYYY-MM-DD." });
            }

            var slots = await _schedulingService.GetAvailableSlotsAsync(id, parsedDate);

            return Ok(new
            {
                doctorId = id,
                date = parsedDate.ToString("yyyy-MM-dd"),
                totalSlots = slots.Count,
                availableSlots = slots.Count(s => s.IsAvailable),
                slots = slots.Select(s => new
                {
                    startTime = s.StartTime.ToString(@"hh\:mm"),
                    endTime = s.EndTime.ToString(@"hh\:mm"),
                    formatted = s.FormattedTime,
                    isAvailable = s.IsAvailable,
                    reason = s.ReasonUnavailable
                })
            });
        }
    }
}
