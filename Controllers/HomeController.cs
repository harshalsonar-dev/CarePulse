using System.Diagnostics;
using CarePulse.Data;
using CarePulse.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarePulse.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<HomeController> _logger;

        public HomeController(ApplicationDbContext context, ILogger<HomeController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            ViewBag.Departments = await _context.Departments
                .Where(d => d.IsActive)
                .Take(6)
                .ToListAsync();

            ViewBag.FeaturedDoctors = await _context.DoctorProfiles
                .Include(d => d.User)
                .Include(d => d.Department)
                .Where(d => d.IsAvailable)
                .Take(4)
                .ToListAsync();

            ViewBag.TotalDoctors = await _context.DoctorProfiles.CountAsync();
            ViewBag.TotalPatients = await _context.PatientProfiles.CountAsync();
            ViewBag.TotalAppointments = await _context.Appointments.CountAsync();

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
