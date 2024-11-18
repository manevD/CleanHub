using CleanHub.Entities;
using System.ComponentModel.DataAnnotations.Schema;

namespace CleanHub.ViewModels
{
    public class SpecialInvoiceViewModel
    {
        public int Id { get; set; }
        public int InvoiceId { get; set; }
        public InvoiceViewModel Invoice { get; set; }
        public int? BuildingId { get; set; }
        public BuildingViewModel Building { get; set; }
        public int? CustomerId { get; set; }
        public CustomerViewModel Customer { get; set; }
        public DateOnly ForDate { get; set; }
        public decimal Total { get; set; }
        public PaymentStatus Status { get; set; }
        public string BuildingName { get; set; }
    }
}
