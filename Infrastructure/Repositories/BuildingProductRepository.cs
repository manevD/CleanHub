using CleanHub.Infrastructure.Data;
using CleanHub.Infrastructure.Repositories.Interfaces;
using CleanHub.Entities;

namespace CleanHub.Infrastructure.Repositories
{
    public class BuildingProductRepository : GenericRepository<BuildingProduct>, IBuildingProductRepository
    {
        private readonly ApplicationDbContext _context;

        public BuildingProductRepository(ApplicationDbContext context) : base(context)
        {
            _context = context; 
        }
    }
}
