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

    public DateOnly? DateReceived
    {
        get => _dateReceived;
        set
        {
            _dateReceived = value;
            // Automatically update DueDate when DateReceived is set
            if (_dateReceived.HasValue)
            {
                DueDate = _dateReceived.Value.AddMonths(1);
            }
            else
            {
                DueDate = null; // Clear DueDate if DateReceived is null
            }
        }
    }

    public DateOnly? DueDate
    {
        get => _dueDate;
        set
        {
            // Ensure DueDate can only be set if DateReceived has a value
            if (DateReceived != null && DateReceived.HasValue)
            {
                _dueDate = value;
            }
            else
            {
                return;
            }
        }
    }
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