using CleanHub.Config;
using CleanHub.Entities;
using System.ComponentModel.DataAnnotations.Schema;
using AutoMapper.Configuration.Annotations;
using CleanHub.Entities.Enums;

namespace CleanHub.ViewModels
{
    public class DocumentViewModel
    {
        public int Id { get; set; }
        public int? Number { get; set; }
        public DateOnly? Date { get; set; }
        public int? CustomerId { get; set; }

        public CustomerViewModel? Customer { get; set; }
        [NotMapped]
        public CompanyConfig? Company { get; set; }  
        public string? ToDocument { get; set; }
        public string? Description { get; set; }
        public DateOnly? DueDate { get; set; }
        [Ignore]
        public int? Delay { get; set; }
        [Ignore]
        public int? NewTotal { get; set; }
        public float? TotalInput { get; set; }
        public float? TotalOutput { get; set; }
        public DateTime? CreatedTime { get; set; }
        public bool IsForPdf { get; set; }
        public DateTime? DateTimeChanged { get; set; }
        public string? PaymentNumber { get; set; }
        public List<BookViewModel>? Books { get; set; }
        public PaymentStatus PaymentStatus { get; set; }
        public List<BuildingViewModel>? Buildings { get; set; }
        public int? BuildingId { get; set; }
        public BuildingViewModel? Building { get; set; } = new BuildingViewModel();
        public int TotalBuildingDemands { get; set; }
        public int TotalBuildingOwes { get; set; }

        public PaymentType PaymentType { get; set; }
        public DateOnly? PaymentDate { get; set; }
    }
}
