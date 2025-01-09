using CleanHub.CleanHub.Infrastructure.Data;
using CleanHub.CleanHub.Infrastructure.Repositories.Interfaces;
using CleanHub.Repositories;
using CleanHub.ViewModels;
using SpecialInvoice = CleanHub.Entities.SpecialInvoice;

namespace CleanHub.CleanHub.Infrastructure.Repositories
{
    public class SpecialInvoiceRepository : GenericRepository<SpecialInvoice>, ISpecialInvoiceRepository
    {
        private static ApplicationDbContext _context = null!;

        public SpecialInvoiceRepository(ApplicationDbContext context) : base(context)
        {
            _context = context; 
        }

        public void UpdateSpecialInvoices(DocumentViewModel document, SpecialInvoice specialInvoice)
        {
           _context.Entry(specialInvoice).Property(s => s.BuildingId).IsModified = true;

            // Alternatively, attach the entity and set the BuildingId explicitly
            _context.Attach(specialInvoice);
            specialInvoice.BuildingId = document.BuildingId;

            // Add the SpecialInvoice to the context
            _context.SpecialInvoices.Add(specialInvoice);
        }
    }
}
