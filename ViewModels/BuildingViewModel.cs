using CleanHub.Entities;

namespace CleanHub.ViewModels
{
    public class BuildingViewModel
    {
        public int Id { get; set; }

        public string? Name { get; set; }

        public string? BankAccount { get; set; }
        public int? ReserveFund {  get; set; }
        public List<BuildingProductViewModel> BuildingProducts { get; set; } = new List<BuildingProductViewModel>();

        public ICollection<CustomerViewModel>? Customers { get; set; }  
    }
}
