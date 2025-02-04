using CleanHub.Infrastructure.Data;
using CleanHub.Infrastructure.Repositories.Interfaces;
using CleanHub.Entities;

namespace CleanHub.Infrastructure.Repositories
{
    public class DocumentRepository : GenericRepository<Document>, IDocumentsRepository
    {
        private readonly ApplicationDbContext _context;

        public DocumentRepository(ApplicationDbContext context) : base(context)
        {
            _context = context; 
        }

    }
}
