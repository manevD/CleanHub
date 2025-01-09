using CleanHub.Entities;

namespace CleanHub.CleanHub.Infrastructure.Repositories.Interfaces
{
    public interface ICustomerRepository: IRepository<Customer>
    {
        Task<List<Customer>> GetCustomersByBuildingIdAsync(int buildingId);

    }
}
