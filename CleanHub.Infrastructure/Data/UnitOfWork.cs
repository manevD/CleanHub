using CleanHub.CleanHub.Infrastructure.Repositories;
using CleanHub.CleanHub.Infrastructure.Repositories.Interfaces;

namespace CleanHub.CleanHub.Infrastructure.Data
{
    public interface IUnitOfWork : IDisposable
    {
        IBuildingRepository Buildings { get; }
        IBookFinancialsRepository BookFinancials { get; }

        void Save();
    }

    public class UnitOfWork : IUnitOfWork
    {
        private readonly ApplicationDbContext _context;

        public UnitOfWork(ApplicationDbContext context)
        {
            _context = context;
            Buildings = new BuildingRepository(context);
            BookFinancials = new BookFinancialRepository(context);
        }

        public IBookFinancialsRepository BookFinancials { get; private set; }

        public IBuildingRepository Buildings { get; private set; }

        public void Save()
        {
            _context.SaveChanges();
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}
