using CleanHub.Config;

namespace CleanHub.ViewModels
{
    public class DocumentViewModel
    {
        public int Id { get; set; }
        public int? Number { get; set; }
        public DateOnly? Date { get; set; }
        public int? CustomerId { get; set; }
        public CustomerViewModel Customer { get; set; }
        public CompanyConfig Company { get; set; }  
        public string? ToDocument { get; set; }

        public string? Description { get; set; }

        public DateOnly? DateReceived { get; set; }

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
        public List<BookViewModel> Books { get; set; }
    }
}
