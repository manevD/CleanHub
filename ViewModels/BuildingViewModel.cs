namespace CleanHub.ViewModels
{
    public class BuildingViewModel
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? BankAccount { get; set; }
        public int? ReserveFund {  get; set; }
        public int CustomersCount
        {
            get
            {
                if (Customers != null) return Customers.Count;
                return 0;
            }
        }

        public List<BuildingProductViewModel> BuildingProducts { get; set; }
        public ICollection<CustomerViewModel> Customers { get; set; }  
    }
}
