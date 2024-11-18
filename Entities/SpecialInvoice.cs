using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CleanHub.Entities
{
    public class SpecialInvoice
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey(nameof(Invoice))]
        public int InvoiceId { get; set; }
        public Invoice Invoice { get; set; }
        public DateOnly ForDate { get; set; }

        [ForeignKey(nameof(Building))]
        public int? BuildingId { get; set; }
        public Building Building { get; set; }

        [ForeignKey(nameof(Customer))]
        public int? CustomerId { get; set; }
        public Customer Customer { get; set; }

        public decimal Total { get; set; }
        public PaymentStatus Status { get; set; }
        [NotMapped]
        public string BuildingName { get; set; }
    }
}
