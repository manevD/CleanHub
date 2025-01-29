using CleanHub.DTO;
using CleanHub.Entities;

namespace CleanHub.ViewModels
{
    public class CustomerCard1200ViewModel
    {
        public List<CustomerDataDTO> CustomerData { get; set; }
        public float CustomerOwesTotal { get; set; }
        public double CustomerDemandsTotal { get; set; }
        public double Total => CustomerOwesTotal - CustomerDemandsTotal;
    }
}
