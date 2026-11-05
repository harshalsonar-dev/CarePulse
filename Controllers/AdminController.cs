using CarePulse.Data;
using CarePulse.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarePulse.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            var totalDoctors = await _context.DoctorProfiles.CountAsync();
            var totalPatients = await _context.PatientProfiles.CountAsync();
            var totalAppointments = await _context.Appointments.CountAsync();
            var todayAppointmentsCount = await _context.Appointments.CountAsync(a => a.AppointmentDate.Date == DateTime.Today);
            var completedCount = await _context.Appointments.CountAsync(a => a.Status == Models.Enums.AppointmentStatus.Completed);

            var recentAppointments = await _context.Appointments
                .Include(a => a.Doctor).ThenInclude(d => d!.User)
                .Include(a => a.Patient).ThenInclude(p => p!.User)
                .OrderByDescending(a => a.CreatedAt)
                .Take(8)
                .ToListAsync();

            var doctors = await _context.DoctorProfiles
                .Include(d => d.User)
                .Include(d => d.Department)
                .ToListAsync();

            var departments = await _context.Departments
                .Include(d => d.Doctors)
                .ToListAsync();

            var viewModel = new AdminDashboardViewModel
            {
                TotalDoctors = totalDoctors,
                TotalPatients = totalPatients,
                TotalAppointments = totalAppointments,
                TodayAppointmentsCount = todayAppointmentsCount,
                CompletedConsultationsCount = completedCount,
                RecentAppointments = recentAppointments,
                Doctors = doctors,
                Departments = departments
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleDoctorStatus(int doctorId)
        {
            var doctor = await _context.DoctorProfiles.FindAsync(doctorId);
            if (doctor == null) return NotFound();

            doctor.IsAvailable = !doctor.IsAvailable;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Doctor status updated to {(doctor.IsAvailable ? "Available" : "Unavailable")}.";
            return RedirectToAction(nameof(Dashboard));
        }
    }
}
