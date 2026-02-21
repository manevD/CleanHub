using CleanHub.Entities;
using CleanHub.Infrastructure.Data;
using CleanHub.Infrastructure.Repositories.Interfaces;
using CleanHub.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace CleanHub.Infrastructure.Repositories
{
    public class BookFinancialRepository(ApplicationDbContext context) : GenericRepository<BookFinancial>(context),
        IBookFinancialsRepository
    {
        public List<BookFinancial> GetBuldingReserve(int buildingId, int? invoiceId)
        {
            if (!invoiceId.HasValue)
                return new List<BookFinancial>();

            var building = context.Buildings
                .FirstOrDefault(x => x.Id == buildingId);

            if (building == null)
                return new List<BookFinancial>();

            var customerIds = context.Customers
        .Where(c => c.BuildingId == buildingId)
        .Select(c => c.Id)
        .ToList();

            var query = context.BookFinancials
                .Include(x => x.Customer)
                .Where(bf =>
                    bf.CustomerId != null &&
                    bf.InvoiceId == invoiceId.Value &&
                    (
                           (building.CustomerRefId.HasValue && bf.CustomerId == building.CustomerRefId.Value)
                        || (!building.CustomerRefId.HasValue && bf.CustomerId == building.Id)
                        || customerIds.Contains(bf.CustomerId.Value)
                    )
                );

            return query.ToList();
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
                                 (building.CustomerRefId.HasValue && bf.CustomerId == building.CustomerRefId.Value)
                                 || (!building.CustomerRefId.HasValue && bf.CustomerId == building.Id)
                                 || customerIdsInBuilding.Contains(bf.CustomerId.Value)
                             ))
                .ToList();

            owes = (int)query.Sum(bf => bf.Owes);
            demands = (int)query.Sum(bf => bf.Demands);

            return (owes, demands);
        }

        public void SetOwesAndDemandsToDocument(int buildingId, int? invoiceId, int? status, DocumentViewModel document)
        {
            // Zuweisung der Werte zu Document
            document.TotalBuildingOwes = GetOwes(buildingId);
            document.TotalBuildingDemands = GetDemands(buildingId);
        }
        public double GetDemands(int buildingId)
        {
            var customers = context.Customers.Where(x => x.BuildingId == buildingId);
            if (customers != null && customers.Any())
            {
                return context.BookFinancials
                    .Where(x =>
                        customers.Any(cus => cus.Id == x.CustomerId) &&
                        x.InvoiceId == (int)InvoiceTyp.Reserve &&
                        (x.Description == null || !x.Description.Contains("салдо"))
                    )
                    .Sum(su => su.Demands);
            }
            return 0;
        }

        public double GetOwes(int buildingId)
        {
            var customerRefId = context.Buildings.FirstOrDefault(x => x.Id == buildingId).CustomerRefId;
            if (customerRefId.HasValue)
            {
                return context.BookFinancials
                    .Where(x =>
                        x.CustomerId == customerRefId.Value &&
                        (x.Description == null || !x.Description.Contains("салдо"))
                    )
                    .Sum(su => su.Owes);
            }
            return 0;
        }
    }
}
