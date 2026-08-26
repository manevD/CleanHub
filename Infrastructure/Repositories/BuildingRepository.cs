using CleanHub.Infrastructure.Data;
using CleanHub.Entities;
using CleanHub.Infrastructure.Repositories.Interfaces;

namespace CleanHub.Infrastructure.Repositories
{
    public class BuildingRepository : GenericRepository<Building>, IBuildingRepository
    {
        private readonly ApplicationDbContext _context;

        public BuildingRepository(ApplicationDbContext context) : base(context)
        {
            _context = context; 
        }

        public IEnumerable<BuildingProduct> GetAllBuildingProducts(int buildingId)
        {
            return _context.BuildingProducts.Where(x => x.BuildingId == buildingId);
        }

        public Building GetBuildingByCustomerRefId(int customerId)
        {
            return _context.Buildings.FirstOrDefault(x => x.CustomerRefId == customerId);
        }
    }
}
