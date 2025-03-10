using CleanHub.Entities.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace CleanHub.Entities;

public partial class Document
{
    public int Id { get; set; }

    public int? Number { get; set; }

    public DateOnly? Date { get; set; }

    [ForeignKey(nameof(Customer))]
    public int? CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    public string? ToDocument { get; set; }

    public string? Description { get; set; }

    private DateOnly? _dateReceived;
    private DateOnly? _dueDate;

    public DateOnly? DateReceived { get; set; }

    public DateOnly? DueDate { get; set; }
  
    public int? NewTotal { get; set; }
    public string? PaymentNumber { get; set; }
    public float? TotalInput { get; set; }
    public float? TotalOutput { get; set; }
    public DateTime? CreatedTime { get; set; }
    public DateTime? DateTimeChanged { get; set; }
    public List<Book> Books { get; set; } = null!;
    public PaymentStatus PaymentStatus { get; set; }
    public PaymentType PaymentType { get; set; }
    public DateOnly? PaymentDate { get; set; }
}