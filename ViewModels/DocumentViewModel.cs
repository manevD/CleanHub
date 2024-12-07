using CleanHub.Config;
using CleanHub.Entities;
using System.ComponentModel.DataAnnotations.Schema;

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
            }
        }

        public float? TotalInput { get; set; }
        public float? TotalOutput { get; set; }
        public DateTime? CreatedTime { get; set; }
        public bool IsForPdf { get; set; }
        public DateTime? DateTimeChanged { get; set; }
        public List<BookViewModel>? Books { get; set; }
        public PaymentStatus PaymentStatus { get; set; }
        public List<BuildingViewModel>? Buildings { get; set; }
        public int? BuildingId { get; set; }
        public BuildingViewModel? Building { get; set; } = new BuildingViewModel();

    }
}
