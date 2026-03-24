using System.ComponentModel.DataAnnotations.Schema;

namespace CleanHub.Entities;

public partial class Book
{
    public long Id { get; set; }

    [ForeignKey(nameof(Document))]
    public int DocId { get; set; }
    public Document Document { get; set; }

    [ForeignKey(nameof(Article))]
    public int? ArticleId { get; set; }

    public Article Article { get; set; }

    public float? Input { get; set; }

    public float? Output { get; set; }

    public float? Quantity { get; set; }
    public float? Price { get; set; }

    public float? PriceWithTax { get; set; }

    public float? Tax { get; set; }

    public float? Total { get; set; }

    public string? ArticleNotes { get; set; }

    public string? UnitOfMeasurement { get; set; }
    public bool Hide { get; set; }
}
