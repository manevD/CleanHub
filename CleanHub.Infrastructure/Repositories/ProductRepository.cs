using CleanHub.CleanHub.Infrastructure.Data;
using CleanHub.CleanHub.Infrastructure.Repositories.Interfaces;
using CleanHub.Entities;
using CleanHub.Repositories;

namespace CleanHub.CleanHub.Infrastructure.Repositories
{
    public class ProductRepository : GenericRepository<Product>, IProductRepository
    {
        private readonly ApplicationDbContext _context;

        public ProductRepository(ApplicationDbContext context) : base(context)
        {
            _context = context; 
        }
    }
}
