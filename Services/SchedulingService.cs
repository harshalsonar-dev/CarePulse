using CarePulse.Data;
using CarePulse.Models.Enums;
using CarePulse.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CarePulse.Services
{
    public class SchedulingService : ISchedulingService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SchedulingService> _logger;

        public SchedulingService(ApplicationDbContext context, ILogger<SchedulingService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<TimeSlotDto>> GetAvailableSlotsAsync(int doctorId, DateTime date)
        {
            var slots = new List<TimeSlotDto>();
            var targetDate = date.Date;

            var doctor = await _context.DoctorProfiles
                .Include(d => d.User)
                .FirstOrDefaultAsync(d => d.Id == doctorId && d.IsAvailable);

            if (doctor == null)
            {
                return slots;
            }

            // Check if doctor works on this day of week
            var dayOfWeekName = targetDate.DayOfWeek.ToString();
            var workingDays = (doctor.WorkingDays ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            bool worksOnThisDay = workingDays.Any(d => d.Equals(dayOfWeekName, StringComparison.OrdinalIgnoreCase));
            if (!worksOnThisDay)
            {
                return slots;
            }

            // Fetch existing non-cancelled appointments for this doctor on targetDate
            var bookedAppointments = await _context.Appointments
                .Where(a => a.DoctorId == doctorId 
                            && a.AppointmentDate.Date == targetDate 
                            && a.Status != AppointmentStatus.Cancelled)
                .Select(a => new { a.StartTime, a.EndTime })
                .ToListAsync();

            var slotDuration = TimeSpan.FromMinutes(doctor.SlotDurationMinutes > 0 ? doctor.SlotDurationMinutes : 30);
            var currentSlotStart = doctor.DailyStartTime;
            var nowTime = DateTime.UtcNow; // Or local clinic time

            while (currentSlotStart + slotDuration <= doctor.DailyEndTime)
            {
                var currentSlotEnd = currentSlotStart + slotDuration;

                // Check overlap with any existing appointment:
                // Overlap exists if max(start1, start2) < min(end1, end2)
                bool isBooked = bookedAppointments.Any(b =>
                    (currentSlotStart < b.EndTime) && (currentSlotEnd > b.StartTime)
                );

                // Check if slot is already in the past if date is today
                bool isPast = false;
                if (targetDate == DateTime.Today && currentSlotStart < DateTime.Now.TimeOfDay)
                {
                    isPast = true;
                }

                bool isAvailable = !isBooked && !isPast;
                string? reason = isBooked ? "Reserved / Booked" : (isPast ? "Time slot has passed" : null);

                slots.Add(new TimeSlotDto
                {
                    StartTime = currentSlotStart,
                    EndTime = currentSlotEnd,
                    FormattedTime = $"{DateTime.Today.Add(currentSlotStart):hh:mm tt} - {DateTime.Today.Add(currentSlotEnd):hh:mm tt}",
                    IsAvailable = isAvailable,
                    ReasonUnavailable = reason
                });

                currentSlotStart = currentSlotEnd;
            }

            return slots;
        }

        public async Task<ConflictCheckResult> ValidateBookingConflictAsync(
            int doctorId, 
            int patientId, 
            DateTime date, 
            TimeSpan startTime, 
            TimeSpan endTime, 
            int? excludeAppointmentId = null)
        {
            var targetDate = date.Date;

            // 1. Check if date is in the past
            if (targetDate < DateTime.Today)
            {
                return new ConflictCheckResult
                {
                    HasConflict = true,
                    ConflictReason = "Cannot schedule an appointment for a past date."
                };
            }

            if (targetDate == DateTime.Today && startTime < DateTime.Now.TimeOfDay)
            {
                return new ConflictCheckResult
                {
                    HasConflict = true,
                    ConflictReason = "Selected appointment time has already passed."
                };
            }

            // 2. Doctor availability & Working Hours check
            var doctor = await _context.DoctorProfiles.FirstOrDefaultAsync(d => d.Id == doctorId);
            if (doctor == null || !doctor.IsAvailable)
            {
                return new ConflictCheckResult
                {
                    HasConflict = true,
                    ConflictReason = "Doctor is currently unavailable or inactive."
                };
            }

            var dayOfWeekName = targetDate.DayOfWeek.ToString();
            var workingDays = (doctor.WorkingDays ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (!workingDays.Any(d => d.Equals(dayOfWeekName, StringComparison.OrdinalIgnoreCase)))
            {
                return new ConflictCheckResult
                {
                    HasConflict = true,
                    ConflictReason = $"Doctor does not consult on {dayOfWeekName}s."
                };
            }

            if (startTime < doctor.DailyStartTime || endTime > doctor.DailyEndTime)
            {
                return new ConflictCheckResult
                {
                    HasConflict = true,
                    ConflictReason = $"Requested slot is outside doctor's consultation hours ({DateTime.Today.Add(doctor.DailyStartTime):hh:mm tt} - {DateTime.Today.Add(doctor.DailyEndTime):hh:mm tt})."
                };
            }

            // 3. Conflict: Doctor double booking
            var doctorConflict = await _context.Appointments
                .AnyAsync(a => a.DoctorId == doctorId
                               && a.AppointmentDate.Date == targetDate
                               && a.Status != AppointmentStatus.Cancelled
                               && (excludeAppointmentId == null || a.Id != excludeAppointmentId.Value)
                               && (startTime < a.EndTime && endTime > a.StartTime));

            if (doctorConflict)
            {
                return new ConflictCheckResult
                {
                    HasConflict = true,
                    ConflictReason = "The doctor is already booked for an appointment during this time window."
                };
            }

            // 4. Conflict: Patient overlapping booking across ANY doctor
            var patientConflict = await _context.Appointments
                .AnyAsync(a => a.PatientId == patientId
                               && a.AppointmentDate.Date == targetDate
                               && a.Status != AppointmentStatus.Cancelled
                               && (excludeAppointmentId == null || a.Id != excludeAppointmentId.Value)
                               && (startTime < a.EndTime && endTime > a.StartTime));

            if (patientConflict)
            {
                return new ConflictCheckResult
                {
                    HasConflict = true,
                    ConflictReason = "Patient already has an active clinical appointment scheduled during this time slot."
                };
            }

            return new ConflictCheckResult { HasConflict = false };
        }

        public async Task<string> GenerateAppointmentNumberAsync()
        {
            var todayStr = DateTime.UtcNow.ToString("yyyyMMdd");
            var countToday = await _context.Appointments
                .CountAsync(a => a.AppointmentDate.Date == DateTime.UtcNow.Date);

            return $"CP-{todayStr}-{(countToday + 1):D3}";
        }
    }
}
