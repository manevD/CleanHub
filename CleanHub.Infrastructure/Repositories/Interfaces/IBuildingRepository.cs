using CleanHub.Entities;
using CleanHub.Entities.Enums;

namespace CleanHub.CleanHub.Infrastructure.Repositories.Interfaces
{
    public interface IBuildingRepository : IRepository<Building>
    {
       // IEnumerable<Building> GetBuldingReserve(int buildingId,int? invoiceId, PaymentStatus? status);
    }
}
