using CleanHub.Entities;

namespace CleanHub.Infrastructure.Repositories.Interfaces
{
    public interface ICustomerRepository: IRepository<Customer>
    {
        Task<List<Customer>> GetCustomersByBuildingIdAsync(int buildingId);
        int GetBalance(int customerId);
    }
}
