using CarePulse.Models.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CarePulse.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Department> Departments { get; set; } = null!;
        public DbSet<DoctorProfile> DoctorProfiles { get; set; } = null!;
        public DbSet<PatientProfile> PatientProfiles { get; set; } = null!;
        public DbSet<Appointment> Appointments { get; set; } = null!;
        public DbSet<Consultation> Consultations { get; set; } = null!;
        public DbSet<Prescription> Prescriptions { get; set; } = null!;
        public DbSet<PrescriptionItem> PrescriptionItems { get; set; } = null!;
        public DbSet<MedicalRecordAttachment> MedicalRecordAttachments { get; set; } = null!;
        public DbSet<Notification> Notifications { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Scheduling performance & conflict indexing
            builder.Entity<Appointment>()
                .HasIndex(a => new { a.DoctorId, a.AppointmentDate, a.StartTime });

            builder.Entity<Appointment>()
                .HasIndex(a => a.AppointmentNumber)
                .IsUnique();

            builder.Entity<Prescription>()
                .HasIndex(p => p.PrescriptionNumber)
                .IsUnique();

            // Decimal precision configurations
            builder.Entity<DoctorProfile>()
                .Property(d => d.ConsultationFee)
                .HasPrecision(18, 2);

            builder.Entity<Consultation>()
                .Property(c => c.TemperatureF)
                .HasPrecision(5, 2);

            builder.Entity<Consultation>()
                .Property(c => c.WeightKg)
                .HasPrecision(5, 2);

            builder.Entity<Consultation>()
                .Property(c => c.HeightCm)
                .HasPrecision(5, 2);

            // Relationships - Use Restrict/NoAction to prevent SQL Server Error 1785 (multiple cascade paths / cycles)
            builder.Entity<DoctorProfile>()
                .HasOne(d => d.User)
                .WithOne(u => u.DoctorProfile)
                .HasForeignKey<DoctorProfile>(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<PatientProfile>()
                .HasOne(p => p.User)
                .WithOne(u => u.PatientProfile)
                .HasForeignKey<PatientProfile>(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Appointment>()
                .HasOne(a => a.Doctor)
                .WithMany(d => d.Appointments)
                .HasForeignKey(a => a.DoctorId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Appointment>()
                .HasOne(a => a.Patient)
                .WithMany(p => p.Appointments)
                .HasForeignKey(a => a.PatientId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Consultation>()
                .HasOne(c => c.Appointment)
                .WithOne(a => a.Consultation)
                .HasForeignKey<Consultation>(c => c.AppointmentId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Consultation>()
                .HasOne(c => c.Doctor)
                .WithMany(d => d.Consultations)
                .HasForeignKey(c => c.DoctorId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Consultation>()
                .HasOne(c => c.Patient)
                .WithMany(p => p.Consultations)
                .HasForeignKey(c => c.PatientId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Prescription>()
                .HasOne(p => p.Consultation)
                .WithOne(c => c.Prescription)
                .HasForeignKey<Prescription>(p => p.ConsultationId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<PrescriptionItem>()
                .HasOne(pi => pi.Prescription)
                .WithMany(p => p.Items)
                .HasForeignKey(pi => pi.PrescriptionId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<MedicalRecordAttachment>()
                .HasOne(m => m.Patient)
                .WithMany(p => p.MedicalAttachments)
                .HasForeignKey(m => m.PatientId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Notification>()
                .HasOne(n => n.User)
                .WithMany(u => u.Notifications)
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
