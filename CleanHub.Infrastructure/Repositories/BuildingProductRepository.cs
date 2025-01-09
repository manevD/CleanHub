using CleanHub.CleanHub.Infrastructure.Data;
using CleanHub.CleanHub.Infrastructure.Repositories.Interfaces;
using CleanHub.Entities;
using CleanHub.Repositories;

namespace CleanHub.CleanHub.Infrastructure.Repositories
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
