using CleanHub.CleanHub.Infrastructure.Data;
using CleanHub.CleanHub.Infrastructure.Repositories.Interfaces;
using CleanHub.Entities;
using CleanHub.Repositories;

namespace CleanHub.CleanHub.Infrastructure.Repositories
{
    public class BookRepository : GenericRepository<Book>, IBookRepository
    {
        private readonly ApplicationDbContext _context;

        public BookRepository(ApplicationDbContext context) : base(context)
        {
            _context = context;
        }
    }
}
