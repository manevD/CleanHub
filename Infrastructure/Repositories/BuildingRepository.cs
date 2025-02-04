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
    }
}
