using CarePulse.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarePulse.Controllers.Api
{
    [ApiController]
    [Route("api/v1/ehr")]
    [Produces("application/json")]
    public class EhrApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public EhrApiController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("patients/{patientId}")]
        public async Task<IActionResult> GetPatientHealthRecord(int patientId)
        {
            var patient = await _context.PatientProfiles
                .Include(p => p.User)
                .Include(p => p.Consultations).ThenInclude(c => c.Prescription).ThenInclude(r => r!.Items)
                .Include(p => p.MedicalAttachments)
                .FirstOrDefaultAsync(p => p.Id == patientId);

            if (patient == null) return NotFound(new { message = "Patient record not found." });

            return Ok(new
            {
                patientId = patient.Id,
                fullName = patient.User?.FullName,
                bloodGroup = patient.BloodGroup,
                allergies = patient.Allergies,
                insurancePolicy = patient.InsurancePolicyNumber,
                consultationsCount = patient.Consultations.Count,
                consultations = patient.Consultations.OrderByDescending(c => c.ConsultationDate).Select(c => new
                {
                    c.Id,
                    c.ConsultationDate,
                    c.ChiefComplaint,
                    c.Diagnosis,
                    vitals = new
                    {
                        bp = c.BloodPressure,
                        hr = c.HeartRateBpm,
                        temp = c.TemperatureF,
                        spo2 = c.SpO2Percentage,
                        weight = c.WeightKg
                    },
                    prescription = c.Prescription != null ? new
                    {
                        c.Prescription.PrescriptionNumber,
                        items = c.Prescription.Items.Select(i => new
                        {
                            i.MedicineName,
                            i.Dosage,
                            i.Frequency,
                            i.Duration
                        })
                    } : null
                }),
                attachments = patient.MedicalAttachments.Select(a => new
                {
                    a.Id,
                    a.Title,
                    a.RecordType,
                    a.FileUrl,
                    a.UploadedAt
                })
            });
        }
    }
}
