using DataAnnotationsExtensions;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations.Schema;

namespace CleanHub.Entities;

public partial class Customer
{
    public int Id { get; set; }

    public string? CustomerInfo { get; set; }

    public string? Adress { get; set; }

    public string? PhoneNumber { get; set; }

    [Email]
    public string? Email { get; set; }

    public string? Web { get; set; }

    public string? PartnerOpis { get; set; }

    public bool? Inactive { get; set; }

    public DateOnly? InactiveDatum { get; set; }

    [ForeignKey(nameof(Building))]
    public int? BuildingId { get; set; }

    public Building? Building { get; set; }

    [ForeignKey(nameof(Activity))]
    public int? ActivityId { get; set; }
    public Activity? Activity { get; set; }
    public bool? PhysicalPerson { get; set; }

    public List<BookFinancial>? BookFinancials {  get; set; }
    public List<Document>? Documents { get; set; }
}