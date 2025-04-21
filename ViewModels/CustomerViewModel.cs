using System.ComponentModel.DataAnnotations;

namespace CleanHub.ViewModels
{
    public class CustomerViewModel
    {
        public int Id { get; set; }

        public string CustomerInfo { get; set; }

        public string? Adress { get; set; }
        public int? Subscription { get; set; }
        public string? PhoneNumber { get; set; }

        [EmailAddress]
        public string? Email { get; set; }

        public string? Web { get; set; }

        public string? PartnerOpis { get; set; }

        public bool Garage { get; set; }

        public bool? Inactive { get; set; }

        public DateOnly? InactiveDatum { get; set; }

        public int BuildingId { get; set; }

        public BuildingViewModel? Building { get; set; }

        public int ActivityId { get; set; }
        public ActivityViewModel? Activity { get; set; }
        public bool? PhysicalPerson { get; set; }

        public List<BookFinancialViewModel>? BookFinancials { get; set; }
        public List<DocumentViewModel>? Documents { get; set; }
    }
}
