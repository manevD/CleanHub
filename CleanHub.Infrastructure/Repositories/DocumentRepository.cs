using System.Linq.Expressions;
using CleanHub.CleanHub.Infrastructure.Data;
using CleanHub.CleanHub.Infrastructure.Repositories.Interfaces;
using CleanHub.Entities;
using CleanHub.Repositories;
using Microsoft.EntityFrameworkCore;

namespace CleanHub.CleanHub.Infrastructure.Repositories
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
