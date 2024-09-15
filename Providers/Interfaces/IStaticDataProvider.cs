using CleanHub.Entities;

namespace CleanHub.Providers.Interfaces
{
    public interface IStaticDataProvider
    {
        Task<List<Building>> GetBuildings();

        Task<List<Product>> GetProducts();
    }
}
