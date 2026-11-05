namespace CarePulse.Models.Enums
{
    public enum AppointmentStatus
    {
        Pending = 0,
        Confirmed = 1,
        InConsultation = 2,
        Completed = 3,
        Cancelled = 4,
        NoShow = 5
    }

    public enum Gender
    {
        Male = 1,
        Female = 2,
        Other = 3
    }

    public enum BloodGroupType
    {
        APositive,
        ANegative,
        BPositive,
        BNegative,
        ABPositive,
        ABNegative,
        OPositive,
        ONegative,
        Unknown
    }

    public enum NotificationType
    {
        AppointmentBooked,
        AppointmentConfirmed,
        AppointmentCancelled,
        AppointmentReminder,
        PrescriptionReady,
        SystemAlert
    }
}
