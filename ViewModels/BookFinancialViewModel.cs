using CleanHub.Entities;
using System.ComponentModel.DataAnnotations.Schema;

namespace CleanHub.ViewModels
{
    public class BookFinancialViewModel
    {
        public int Id { get; set; }
        public int? OrderN { get; set; }

        [ForeignKey(nameof(Invoice))]
        public int? InvoiceId { get; set; }
        public Invoice Invoice { get; set; }
        public int? DocumentId { get; set; }
        public DocumentViewModel Document { get; set; }
        public int? CustomerId { get; set; }
        public CustomerViewModel Customer { get; set; }

        [ForeignKey(nameof(DocumentTyp))]
        public int? DocumentTypId { get; set; }
        public DocumentTyp DocumentTyp { get; set; }

        public string? Description { get; set; }

        public DateOnly? DatumF { get; set; }

        public double Owes { get; set; }

        public double Demands { get; set; }
        public DateTime? Time { get; set; }
        public DateTime? DateTimeChanges { get; set; }
        public PaymentStatus Status {get; set; }
    }
}
