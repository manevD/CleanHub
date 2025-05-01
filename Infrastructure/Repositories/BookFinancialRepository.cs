using CleanHub.Entities;
using CleanHub.Infrastructure.Data;
using CleanHub.Infrastructure.Repositories.Interfaces;
using CleanHub.ViewModels;

namespace CleanHub.Infrastructure.Repositories
{
    public class BookFinancialRepository(ApplicationDbContext context) : GenericRepository<BookFinancial>(context),
        IBookFinancialsRepository
    {
        public List<BookFinancial> GetBuldingReserve(int buildingId, int? invoiceId)
        {
            var query = new List<BookFinancial>();
            var building = context.Buildings.FirstOrDefault(x => x.Id == buildingId);
            if (invoiceId.HasValue)
            {
                query = context.BookFinancials
                    .Where(bf => bf.CustomerId != null
                                 && invoiceId.HasValue
                                 && (
                                     (building.CustomerRefId.HasValue && bf.CustomerId == building.CustomerRefId.Value && bf.InvoiceId == invoiceId.Value)
                                     ||
                                     (bf.InvoiceId == invoiceId.Value &&
                                      context.Customers
                                          .Where(c => c.BuildingId == buildingId)
                                          .Select(c => c.Id)
                                          .Contains(bf.CustomerId.Value))
                                 ))
                    .ToList();
            }
            return query;
        }
        public (int owes, int demands) GetBuildingReserve(int buildingId, int? invoiceId, int? status)
        {
            var owes = 0;
            var demands = 0;

            var building = context.Buildings.FirstOrDefault(x => x.Id == buildingId);
            if (building == null || building.CustomerRefId == null || !invoiceId.HasValue || invoiceId.Value != (int)InvoiceTyp.Reserve)
            {
                return (owes, demands);
            }

            var customerIdsInBuilding = context.Customers
                .Where(c => c.BuildingId == buildingId)
                .Select(c => c.Id)
                .ToList();

            var query = context.BookFinancials
                .Where(bf => bf.CustomerId != null
                             && bf.InvoiceId == invoiceId.Value
                             && (
                                 bf.CustomerId == building.CustomerRefId.Value || bf.CustomerId == building.Id
                                 || customerIdsInBuilding.Contains(bf.CustomerId.Value)
                             ))
                .ToList();

            owes = (int)query.Sum(bf => bf.Owes);
            demands = (int)query.Sum(bf => bf.Demands);

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
