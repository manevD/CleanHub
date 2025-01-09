using CleanHub.CleanHub.Infrastructure.Data;
using CleanHub.CleanHub.Infrastructure.Repositories.Interfaces;
using CleanHub.Entities;
using CleanHub.Repositories;
using Microsoft.EntityFrameworkCore;

namespace CleanHub.CleanHub.Infrastructure.Repositories
{
    public class CustomersRepository : GenericRepository<Customer>, ICustomerRepository
    {
        private readonly ApplicationDbContext _context;

        public CustomersRepository(ApplicationDbContext context) : base(context)
        {
            _context = context; 
        }

        public async Task<List<Customer>> GetCustomersByBuildingIdAsync(int buildingId)
        {
                return await _context.Set<Building>()
                    .Where(x => x.Id == buildingId)
                    .Include(x => x.Customers)
                    .SelectMany(d => d.Customers)
                    .ToListAsync();
        }
    }
}
