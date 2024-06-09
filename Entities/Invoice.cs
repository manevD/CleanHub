namespace CleanHub.Entities;

public partial class Invoice
{
    public int Id { get; set; }

    public string? Description { get; set; }

    public bool? KarticaPar { get; set; }

    public bool? FromBilance { get; set; }
}