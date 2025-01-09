using System.Runtime.Serialization;
using CleanHub.Entities;
using CleanHub.Entities.Enums;

namespace CleanHub.ViewModels
{
    public class SpecialInvoiceViewModel
    {
        public int Id { get; set; }
        public int InvoiceId { get; set; }
        public InvoiceViewModel Invoice { get; set; } = null!;
        public int? BuildingId { get; set; }
        public BuildingViewModel Building { get; set; } = null!;
        public int? CustomerId { get; set; }
        public CustomerViewModel Customer { get; set; } = null!;
        public DateOnly? ForDate { get; set; }
        public decimal Total { get; set; }
        public PaymentStatus Status { get; set; }
        public string BuildingName { get; set; } = null!;
        public string CustomerName { get; set; } = null!;

        [IgnoreDataMember]
        public List<InvoiceViewModel> Invoices { get; set; } = null!;
    }
}
