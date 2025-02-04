using CleanHub.Infrastructure.Repositories;
using CleanHub.Infrastructure.Repositories.Interfaces;

namespace CleanHub.Infrastructure.Data
{
    public interface IUnitOfWork 
    {
        ICustomerRepository Customers { get; }
        IBuildingProductRepository BuildingProducts { get; }
        IBookRepository Books { get; }
        IDocumentsRepository Documents { get; }
        IProductRepository Products { get; }
        IBuildingRepository Buildings { get; }
        IBookFinancialsRepository BookFinancials { get; }
        ISpecialInvoiceRepository SpecialInvoices { get; }
        Task SaveChangesAsync();
    }

    public class UnitOfWork : IUnitOfWork
    {
        private readonly ApplicationDbContext _context;

        public UnitOfWork(ApplicationDbContext context)
        {
            _context = context;
            Buildings = new BuildingRepository(context);
            Documents = new DocumentRepository(context);
            Products = new ProductRepository(context);
            BookFinancials = new BookFinancialRepository(context);
            SpecialInvoices = new SpecialInvoiceRepository(context);
            Books = new BookRepository(context);
            Customers = new CustomersRepository(context);
            BuildingProducts = new BuildingProductRepository(context);
        }
        public IBuildingProductRepository BuildingProducts { get; private set; }
        public ICustomerRepository Customers { get; private set; }
        public IBookFinancialsRepository BookFinancials { get; private set; }
        public IDocumentsRepository Documents { get; private set; }
        public IBookRepository Books { get; private set; }
        public IProductRepository Products { get; private set; }
        public ISpecialInvoiceRepository SpecialInvoices { get; private set; }
        public IBuildingRepository Buildings { get; private set; }

        public async Task SaveChangesAsync()
        {
           await _context.SaveChangesAsync();
        }
    }
}
