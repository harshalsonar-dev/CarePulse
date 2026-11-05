using CarePulse.Models.Entities;

namespace CarePulse.Services.Interfaces
{
    public class TimeSlotDto
    {
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string FormattedTime { get; set; } = string.Empty;
        public bool IsAvailable { get; set; }
        public string? ReasonUnavailable { get; set; }
    }

    public class ConflictCheckResult
    {
        public bool HasConflict { get; set; }
        public string? ConflictReason { get; set; }
    }

    public interface ISchedulingService
    {
        Task<List<TimeSlotDto>> GetAvailableSlotsAsync(int doctorId, DateTime date);
        Task<ConflictCheckResult> ValidateBookingConflictAsync(int doctorId, int patientId, DateTime date, TimeSpan startTime, TimeSpan endTime, int? excludeAppointmentId = null);
        Task<string> GenerateAppointmentNumberAsync();
    }
}
