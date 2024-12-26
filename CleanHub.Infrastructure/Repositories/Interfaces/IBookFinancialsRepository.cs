using CleanHub.Entities;
using CleanHub.Entities.Enums;
using CleanHub.ViewModels;

namespace CleanHub.CleanHub.Infrastructure.Repositories.Interfaces
{
    public interface IBookFinancialsRepository : IRepository<BookFinancial>
    {
        List<BookFinancial> GetBuldingReserve(int buildingId, int? invoiceId, int? status);
        (int owes, int demands) GetBuildingReserve(int buildingId, int? invoiceId, int? status);
        void SetOwesAndDemandsToDocument(int buildingId, int? invoiceId, int? status, DocumentViewModel document);
    }
}
