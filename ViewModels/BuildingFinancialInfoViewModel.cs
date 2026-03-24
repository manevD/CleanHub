using CleanHub.Entities.Enums;

namespace CleanHub.ViewModels
{
    public class BookFinancialInfoViewModel
    {
        public long Id { get; set; }
        public PaymentStatus Status { get; set; }
        public int InvoiceId { get; set; }
        public string Description { get; set; }
        public int DocumentTypId { get; set; }
        public DateOnly DatumF { get; set; }
        public double Owes { get; set; }
        public double Demands { get; set; }
        public string FormattedDatumF => DatumF.ToString("yyyy/MM/dd");
        public int? Delay { get; set; }
        public int? NewTotal { get; set; }
        public bool DontSum { get; set; }
    }
}
