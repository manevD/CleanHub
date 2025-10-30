using CleanHub.Infrastructure.Data;
using CleanHub.Infrastructure.Repositories.Interfaces;
using CleanHub.ViewModels;
using SpecialInvoice = CleanHub.Entities.SpecialInvoice;

namespace CleanHub.Infrastructure.Repositories
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
            var building = _context.Buildings.FirstOrDefault(x => x.Id == specialInvoice.BuildingId);
            if (building != null)
            {
                _context.Attach(building); // Stelle sicher, dass Building nur verfolgt wird
                specialInvoice.Building = building; // Setze die referenzierte Instanz
            }

            _context.SpecialInvoices.Add(specialInvoice);
        }
    }
}