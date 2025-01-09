using CleanHub.Entities;
using CleanHub.ViewModels;

namespace CleanHub.CleanHub.Infrastructure.Repositories.Interfaces
{
    public interface ISpecialInvoiceRepository : IRepository<SpecialInvoice>
    {
        void UpdateSpecialInvoices(DocumentViewModel document, SpecialInvoice specialInvoice);
    }
}
