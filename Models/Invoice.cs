namespace CleanHub.Models
{
    public class Invoice
    {
        public int Id { get; set; }
        public decimal AmountDue { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime CreatedDate { get; set; }
        public PaymentStatus PaymentStatus { get; set; }

        // Foreign key for the associated resident
        public int ResidentId { get; set; }
        // Navigation property for the associated resident
        public Resident Resident { get; set; }

    }
    public enum PaymentStatus
    {
        Pending,
        Paid,
        Overdue
    }
}
