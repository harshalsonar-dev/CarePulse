using CarePulse.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarePulse.Controllers
{
    public class DoctorsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DoctorsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int? departmentId, string? search)
        {
            var query = _context.DoctorProfiles
                .Include(d => d.User)
                .Include(d => d.Department)
                .Where(d => d.IsAvailable)
                .AsQueryable();

            if (departmentId.HasValue && departmentId.Value > 0)
            {
                query = query.Where(d => d.DepartmentId == departmentId.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(d => (d.User != null && d.User.FullName.Contains(search)) ||
                                         d.Specialization.Contains(search) ||
                                         d.Qualification.Contains(search));
            }

            var doctors = await query.ToListAsync();
            ViewBag.Departments = await _context.Departments.Where(d => d.IsActive).ToListAsync();
            ViewBag.SelectedDepartmentId = departmentId;
            ViewBag.SearchTerm = search;

            return View(doctors);
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var doctor = await _context.DoctorProfiles
                .Include(d => d.User)
                .Include(d => d.Department)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (doctor == null) return NotFound();

            return View(doctor);
        }
    }
}
