using CarePulse.Models.Entities;

namespace CarePulse.Services.Interfaces
{
    public interface IPrescriptionPdfService
    {
        Task<byte[]> GeneratePrescriptionPdfAsync(int consultationId);
    }
}
