using CleanHub.Entities;
using CleanHub.Infrastructure.Data;
using CleanHub.Infrastructure.Repositories.Interfaces;

namespace CleanHub.Infrastructure.Repositories
{
    public class ActivityRepository : GenericRepository<Activity>, IActivitiesRepository
    {
        private readonly ApplicationDbContext _context;

        public ActivityRepository(ApplicationDbContext context) : base(context)
        {
            _context = context;
        }
    }
}
