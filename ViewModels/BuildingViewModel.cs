namespace CleanHub.ViewModels
{
    public class BuildingViewModel
    {
        public int Id { get; set; }

        public string? Name { get; set; }

        public string? BankAccount { get; set; }
        public List<CustomerViewModel> Customers { get; set; }  
    }
}
