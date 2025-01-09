using System.Runtime.Serialization;
using AutoMapper.Configuration.Annotations;

namespace CleanHub.ViewModels
{
    public class BuildingViewModel
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? BankAccount { get; set; }
        public int? ReserveFund {  get; set; }
        public int? CustomerRefId { get; set; }

        public int CustomersCount
        {
            get
            {
                if (Customers != null) return Customers.Count;
                return 0;
            }
        }
        [Ignore]
        [IgnoreDataMember]
        public int? ReserveTotal { get; set; }

        public List<BuildingProductViewModel> BuildingProducts { get; set; } = null!;
        public ICollection<CustomerViewModel>? Customers { get; set; }  
    }
}
