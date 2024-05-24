//using CleanHub.Entities;
//using CleanHub.Models;
//using System.ComponentModel.DataAnnotations.Schema;

//namespace CleanHub
//{
//    public class ResidentViewModel
//    {
//        public int Id { get; set; }
//        public string FirstName { get; set; }
//        public string LastName { get; set; }
//        public string? Email { get; set; }
//        public string? Web { get; set; }
//        public string? PhoneNumber { get; set; }
//        public bool Inactive { get; set; }
//        public DateTime? InactiveFrom { get; set; }
//        public decimal? ReserveMoney { get; set; }

//        // Foreign key for the associated building
//        public int BuildingId { get; set; }
//        [NotMapped]
//        public string BuildingName { get; set; }

//        // Navigation property for the associated building
//        public Building Building { get; set; }

//        // Navigation property for invoices
//        public ICollection<Invoice> Invoices { get; set; }
//    }
//}
