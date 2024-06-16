using System.ComponentModel.DataAnnotations.Schema;

namespace CleanHub.Entities;

public partial class BookFinancial
{
    public int Id { get; set; }

    public int? OrderN { get; set; }

    [ForeignKey(nameof(Invoice))]
    public int? SmetkaId { get; set; }
    public Invoice Invoice { get; set; }

    [ForeignKey(nameof(Customer))]
    public int? CustomerId { get; set; }
    public Customer Customer { get; set; }

    [ForeignKey(nameof(DocumentTyp))]
    public int? DocumentTypId { get; set; }
    public DocumentTyp DocumentTyp { get; set; }

    public string? Description { get; set; }

    public DateOnly? DatumF { get; set; }

    public double Owes { get; set; }

    public double Demands { get; set; }

    public DateTime? Time { get; set; }
    public DateTime? DateTimeChanges { get; set; }

}
