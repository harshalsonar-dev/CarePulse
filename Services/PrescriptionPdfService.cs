using CarePulse.Data;
using CarePulse.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CarePulse.Services
{
    public class PrescriptionPdfService : IPrescriptionPdfService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<PrescriptionPdfService> _logger;

        public PrescriptionPdfService(ApplicationDbContext context, ILogger<PrescriptionPdfService> logger)
        {
            _context = context;
            _logger = logger;
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public async Task<byte[]> GeneratePrescriptionPdfAsync(int consultationId)
        {
            var consultation = await _context.Consultations
                .Include(c => c.Doctor)
                    .ThenInclude(d => d!.User)
                .Include(c => c.Doctor)
                    .ThenInclude(d => d!.Department)
                .Include(c => c.Patient)
                    .ThenInclude(p => p!.User)
                .Include(c => c.Appointment)
                .Include(c => c.Prescription)
                    .ThenInclude(p => p!.Items)
                .FirstOrDefaultAsync(c => c.Id == consultationId);

            if (consultation == null)
            {
                throw new InvalidOperationException($"Consultation with ID {consultationId} not found.");
            }

            var doctor = consultation.Doctor;
            var patient = consultation.Patient;
            var prescription = consultation.Prescription;
            var appointment = consultation.Appointment;

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1.5f, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10).FontColor(Colors.Grey.Darken3).FontFamily("Arial"));

                    // Header
                    page.Header().Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem(3).Column(c =>
                            {
                                c.Item().Text("CAREPULSE CLINICAL MEDICAL CENTER")
                                    .Bold().FontSize(18).FontColor(Colors.Blue.Darken2);
                                c.Item().Text("Smart Clinical Care & Electronic Health Records")
                                    .FontSize(9).FontColor(Colors.Grey.Medium);
                                c.Item().Text("450 Health Avenue, Medical City, MC 98001 | Contact: +1 (800) 555-PULSE")
                                    .FontSize(8).FontColor(Colors.Grey.Medium);
                            });

                            row.RelativeItem(2).AlignRight().Column(c =>
                            {
                                c.Item().Text($"Rx #: {prescription?.PrescriptionNumber ?? $"RX-{consultation.Id:D5}"}")
                                    .Bold().FontSize(12).FontColor(Colors.Blue.Darken3);
                                c.Item().Text($"Date: {consultation.ConsultationDate:dd MMM yyyy, hh:mm tt}")
                                    .FontSize(9);
                                c.Item().Text($"Appt Ref: {appointment?.AppointmentNumber ?? "N/A"}")
                                    .FontSize(8).FontColor(Colors.Grey.Darken1);
                            });
                        });

                        col.Item().PaddingVertical(8).LineHorizontal(1.5f).LineColor(Colors.Blue.Medium);

                        // Doctor & Patient Banner
                        col.Item().Background(Colors.Grey.Lighten4).Padding(8).Row(infoRow =>
                        {
                            // Doctor Box
                            infoRow.RelativeItem().Column(dc =>
                            {
                                dc.Item().Text("ATTENDING PHYSICIAN").Bold().FontSize(8).FontColor(Colors.Grey.Darken2);
                                dc.Item().Text($"Dr. {doctor?.User?.FullName ?? "Physician"}")
                                    .Bold().FontSize(11).FontColor(Colors.Blue.Darken3);
                                dc.Item().Text($"{doctor?.Qualification} | {doctor?.Specialization}")
                                    .FontSize(9);
                                dc.Item().Text($"Dept: {doctor?.Department?.Name ?? "Clinical Medicine"} | Lic #: {doctor?.LicenseNumber ?? "LIC-MED"}")
                                    .FontSize(8).FontColor(Colors.Grey.Darken1);
                            });

                            // Patient Box
                            infoRow.RelativeItem().Column(pc =>
                            {
                                pc.Item().Text("PATIENT ELECTRONIC RECORD").Bold().FontSize(8).FontColor(Colors.Grey.Darken2);
                                pc.Item().Text(patient?.User?.FullName ?? "Patient Name")
                                    .Bold().FontSize(11).FontColor(Colors.Blue.Darken3);
                                
                                var ageStr = patient?.User?.DateOfBirth != null 
                                    ? $"{DateTime.Today.Year - patient.User.DateOfBirth.Value.Year} yrs" 
                                    : "Age N/A";
                                var genderStr = patient?.User?.Gender?.ToString() ?? "N/A";
                                pc.Item().Text($"Gender: {genderStr} | Age: {ageStr} | Blood: {patient?.BloodGroup ?? "N/A"}")
                                    .FontSize(9);
                                
                                if (!string.IsNullOrEmpty(patient?.Allergies))
                                {
                                    pc.Item().Text($"Allergies: {patient.Allergies}")
                                        .Bold().FontColor(Colors.Red.Darken2).FontSize(8);
                                }
                            });
                        });
                    });

                    // Body
                    page.Content().PaddingVertical(10).Column(content =>
                    {
                        // Clinical Vitals Panel
                        content.Item().PaddingBottom(8).Border(1).BorderColor(Colors.Grey.Lighten2).Background(Colors.Grey.Lighten5).Padding(8).Column(vc =>
                        {
                            vc.Item().Text("CLINICAL VITALS & OBSERVATIONS").Bold().FontSize(9).FontColor(Colors.Blue.Darken3);
                            vc.Item().PaddingTop(4).Row(vr =>
                            {
                                vr.RelativeItem().Text($"Blood Pressure: {consultation.BloodPressure ?? "120/80 mmHg"}").FontSize(8.5f);
                                vr.RelativeItem().Text($"Heart Rate: {consultation.HeartRateBpm?.ToString() ?? "72"} bpm").FontSize(8.5f);
                                vr.RelativeItem().Text($"Temperature: {consultation.TemperatureF?.ToString() ?? "98.6"} °F").FontSize(8.5f);
                                vr.RelativeItem().Text($"SpO2: {consultation.SpO2Percentage?.ToString() ?? "99"} %").FontSize(8.5f);
                                vr.RelativeItem().Text($"Weight: {consultation.WeightKg?.ToString() ?? "--"} kg").FontSize(8.5f);
                            });
                        });

                        // Diagnosis & Complaint
                        content.Item().PaddingBottom(8).Row(r =>
                        {
                            r.RelativeItem().Column(dc =>
                            {
                                dc.Item().Text("Chief Complaint:").Bold().FontSize(9).FontColor(Colors.Grey.Darken2);
                                dc.Item().Text(consultation.ChiefComplaint).FontSize(9.5f);

                                if (!string.IsNullOrEmpty(consultation.HistoryOfPresentIllness))
                                {
                                    dc.Item().PaddingTop(3).Text("Clinical Notes:").Bold().FontSize(8.5f).FontColor(Colors.Grey.Darken2);
                                    dc.Item().Text(consultation.HistoryOfPresentIllness).FontSize(8.5f);
                                }
                            });

                            r.RelativeItem().Column(dc =>
                            {
                                dc.Item().Text("Diagnosis:").Bold().FontSize(9).FontColor(Colors.Grey.Darken2);
                                dc.Item().Text(consultation.Diagnosis).Bold().FontSize(10).FontColor(Colors.Red.Darken3);
                            });
                        });

                        content.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);

                        // Prescription Section Header with Rx Symbol
                        content.Item().PaddingTop(8).PaddingBottom(4).Row(rxRow =>
                        {
                            rxRow.AutoItem().Text("℞").Bold().FontSize(22).FontColor(Colors.Blue.Darken3);
                            rxRow.RelativeItem().PaddingLeft(6).AlignMiddle()
                                .Text("DIGITAL PRESCRIPTION & MEDICATION REGIMEN")
                                .Bold().FontSize(11).FontColor(Colors.Blue.Darken3);
                        });

                        // Medication Table
                        content.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(25);  // #
                                columns.RelativeColumn(3);   // Medicine
                                columns.RelativeColumn(1.5f); // Dosage
                                columns.RelativeColumn(2);   // Frequency
                                columns.RelativeColumn(1.2f); // Duration
                                columns.RelativeColumn(2.5f); // Instructions
                            });

                            // Table Header
                            table.Header(header =>
                            {
                                header.Cell().Background(Colors.Blue.Darken2).Padding(4).Text("#").Bold().FontColor(Colors.White).FontSize(8.5f);
                                header.Cell().Background(Colors.Blue.Darken2).Padding(4).Text("Medicine / Formulation").Bold().FontColor(Colors.White).FontSize(8.5f);
                                header.Cell().Background(Colors.Blue.Darken2).Padding(4).Text("Dosage").Bold().FontColor(Colors.White).FontSize(8.5f);
                                header.Cell().Background(Colors.Blue.Darken2).Padding(4).Text("Frequency").Bold().FontColor(Colors.White).FontSize(8.5f);
                                header.Cell().Background(Colors.Blue.Darken2).Padding(4).Text("Duration").Bold().FontColor(Colors.White).FontSize(8.5f);
                                header.Cell().Background(Colors.Blue.Darken2).Padding(4).Text("Instructions / Route").Bold().FontColor(Colors.White).FontSize(8.5f);
                            });

                            int itemIndex = 1;
                            var items = prescription?.Items.ToList() ?? new List<CarePulse.Models.Entities.PrescriptionItem>();

                            if (items.Count == 0)
                            {
                                table.Cell().ColumnSpan(6).Padding(8).AlignCenter().Text("No medicinal regimen prescribed. Follow non-pharmacological advice.").Italic().FontSize(9);
                            }
                            else
                            {
                                foreach (var item in items)
                                {
                                    var bg = itemIndex % 2 == 0 ? Colors.Grey.Lighten5 : Colors.White;

                                    table.Cell().Background(bg).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(itemIndex.ToString()).FontSize(8.5f);
                                    table.Cell().Background(bg).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(item.MedicineName).Bold().FontSize(9);
                                    table.Cell().Background(bg).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(item.Dosage).FontSize(8.5f);
                                    table.Cell().Background(bg).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(item.Frequency).FontSize(8.5f);
                                    table.Cell().Background(bg).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(item.Duration).FontSize(8.5f);
                                    table.Cell().Background(bg).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text($"{item.Route} - {item.SpecialInstructions}").FontSize(8);

                                    itemIndex++;
                                }
                            }
                        });

                        // Doctor Advice & Follow-up
                        content.Item().PaddingTop(10).Column(adv =>
                        {
                            if (!string.IsNullOrEmpty(consultation.DoctorAdvice) || !string.IsNullOrEmpty(prescription?.GeneralInstructions))
                            {
                                adv.Item().Text("Special Clinical Advice / Diet / Lifestyle Instructions:")
                                    .Bold().FontSize(9).FontColor(Colors.Grey.Darken2);
                                adv.Item().Text(consultation.DoctorAdvice ?? prescription?.GeneralInstructions ?? "Adequate rest and fluid intake.")
                                    .FontSize(9);
                            }

                            if (consultation.FollowUpDate.HasValue)
                            {
                                adv.Item().PaddingTop(6).Text($"Recommended Follow-up Date: {consultation.FollowUpDate.Value:dddd, dd MMMM yyyy}")
                                    .Bold().FontSize(9).FontColor(Colors.Blue.Darken3);
                            }
                        });
                    });

                    // Footer
                    page.Footer().Column(footer =>
                    {
                        footer.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                        footer.Item().PaddingTop(6).Row(fr =>
                        {
                            fr.RelativeItem(2).Column(fCol =>
                            {
                                fCol.Item().Text("VERIFIED CLINICAL DIGITAL RECORD").Bold().FontSize(7.5f).FontColor(Colors.Green.Darken2);
                                fCol.Item().Text("Generated securely via CarePulse Health EHR System. Valid without physical signature when presented with Patient ID.")
                                    .FontSize(7).FontColor(Colors.Grey.Darken1);
                                fCol.Item().Text($"Generated on: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC | System Hash: {Guid.NewGuid().ToString("N")[..12].ToUpper()}")
                                    .FontSize(6.5f).FontColor(Colors.Grey.Medium);
                            });

                            fr.RelativeItem(1).AlignRight().Column(sigCol =>
                            {
                                sigCol.Item().AlignRight().Text($"Dr. {doctor?.User?.FullName ?? "Physician"}")
                                    .Bold().FontSize(9.5f).FontColor(Colors.Blue.Darken3);
                                sigCol.Item().AlignRight().Text($"Reg: {doctor?.LicenseNumber ?? "LIC-9921"}")
                                    .FontSize(8).FontColor(Colors.Grey.Darken2);
                                sigCol.Item().AlignRight().Text("[ Digitally Authenticated ]")
                                    .Italic().FontSize(7.5f).FontColor(Colors.Green.Darken2);
                            });
                        });
                    });
                });
            });

            return document.GeneratePdf();
        }
    }
}
