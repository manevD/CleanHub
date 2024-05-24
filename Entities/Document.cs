using System.ComponentModel.DataAnnotations.Schema;

namespace CleanHub.Entities;

public partial class Document
{
    public int Id { get; set; }

    public int? Number { get; set; }

    public DateOnly? Date { get; set; }

    [ForeignKey(nameof(Customer))]
    public int? CustomerId { get; set; }
    public Customer Customer { get; set; }

    public string? ToDocument { get; set; }

    public string? Description { get; set; }

    public DateOnly? DateReceived  { get; set; }

    public DateOnly? DueDate
    {
        get
        {
            if (DateReceived.HasValue)
            {
                return DateReceived.Value.AddMonths(1);
            }
            else
            {
                return null;
            }
        }
    }

    public float? TotalInput { get; set; }

    public float? TotalOutput { get; set; }

    public DateTime? CreatedTime { get; set; }

    public DateTime? DateTimeChanged { get; set; }
}