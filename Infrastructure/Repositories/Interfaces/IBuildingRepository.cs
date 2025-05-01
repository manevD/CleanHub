using CleanHub.Entities;
using CleanHub.Entities.Enums;

namespace CleanHub.Infrastructure.Repositories.Interfaces
{
    public interface IBuildingRepository : IRepository<Building>
    {
        public IEnumerable<BuildingProduct> GetAllBuildingProducts(int buildingId);
    }
}
