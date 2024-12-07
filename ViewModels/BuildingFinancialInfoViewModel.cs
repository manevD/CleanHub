using CleanHub.Entities;

namespace CleanHub.ViewModels
{
    public class BookFinancialInfoViewModel
    {
        public int Id { get; set; }
        public string BuildingName { get; set; }
        public string CustomerInfo { get; set; }
        public PaymentStatus Status { get; set; }
        public int InvoiceId { get; set; }
        public string Description { get; set; }
        public DateOnly DatumF { get; set; }
        public double Owes { get; set; }
        public double Demands { get; set; }
        public string FormattedDatumF => DatumF.ToString("MM/yyyy");

    }
}
