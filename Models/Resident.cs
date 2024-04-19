namespace CleanHub.Models
{
    public class Resident
    {
        public int Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }

        // Foreign key for the associated building
        public int BuildingId { get; set; }
        // Navigation property for the associated building
        public Building Building { get; set; }

        // Navigation property for invoices
        public ICollection<Invoice> Invoices { get; set; }
    }
}
