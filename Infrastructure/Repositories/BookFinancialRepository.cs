using CleanHub.Entities;
using CleanHub.Infrastructure.Data;
using CleanHub.Infrastructure.Repositories.Interfaces;
using CleanHub.ViewModels;

namespace CleanHub.Infrastructure.Repositories
{
    public class BookFinancialRepository : GenericRepository<BookFinancial>,IBookFinancialsRepository
    {
        private readonly ApplicationDbContext _context;

        public BookFinancialRepository(ApplicationDbContext context) : base(context)
        {
            _context = context;
        }

        public List<BookFinancial> GetBuldingReserve(int buildingId, int? invoiceId)
        {
            var query = new List<BookFinancial>();
            var building = _context.Buildings.FirstOrDefault(x => x.Id == buildingId);
            if (invoiceId.HasValue)
            {
                query = _context.BookFinancials.Where(bf =>
                    bf.CustomerId != null && ((bf.CustomerId == building.CustomerRefId.Value && bf.InvoiceId == invoiceId.Value) ||
                                              (_context.Customers.Where(c => c.BuildingId == buildingId).Select(c => c.Id).Contains(bf.CustomerId.Value) && bf.InvoiceId == invoiceId.Value))
                ).ToList();
            }
            return query;
        }

        public (int owes, int demands) GetBuildingReserve(int buildingId, int? invoiceId, int? status)
        {
            var owes = 0;
            var demands = 0;

            var building = _context.Buildings.FirstOrDefault(x => x.Id == buildingId);
            if (invoiceId.HasValue && invoiceId.Value == 1201)
            {
                var query = _context.BookFinancials.Where(bf =>
                    building != null && building.CustomerRefId != null &&
                    (
                        (bf.CustomerId == building.CustomerRefId.Value && bf.InvoiceId == invoiceId.Value) ||
                        (_context.Customers.Where(c => c.BuildingId == buildingId)
                            .Select(c => c.Id)
                            .Contains(bf.CustomerId.Value) && bf.InvoiceId == invoiceId.Value)
                    )
                ).ToList();

                // Summiere owes und demands basierend auf der Abfrage
                owes = (int)query.Sum(bf => bf.Owes);
                demands = (int)query.Sum(bf => bf.Demands);
            }

            return (owes, demands);
        }
        public void SetOwesAndDemandsToDocument(int buildingId, int? invoiceId, int? status, DocumentViewModel document)
        {
            var (owes, demands) = GetBuildingReserve(buildingId, invoiceId, status);

            // Zuweisung der Werte zu Document
            document.TotalBuildingOwes = owes;
            document.TotalBuildingDemands = demands;
        }
    }
}
