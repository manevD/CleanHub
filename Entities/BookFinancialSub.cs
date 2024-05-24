using System.ComponentModel.DataAnnotations.Schema;

namespace CleanHub.Entities;

public partial class BookFinancialSub
{
    public int Id { get; set; }

    [ForeignKey(nameof(BookFinancial))]
    public int? BookFinancialId { get; set; }
    public BookFinancial BookFinancial { get; set; }

    public DateOnly? Date { get; set; }

    public float? Owes { get; set; }

    public float? Demands { get; set; }
}
