using CleanHub.CleanHub.Infrastructure.Data;
using CleanHub.Entities;
using CleanHub.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CleanHub.Controllers
{
    public class CardsController(IUnitOfWork _unitOfWork) : Controller
    {
        public List<Building> BuildingsList { get; set; } = _unitOfWork.Buildings.GetAll().ToList();
        public List<Customer> CustomersList { get; set; } = _unitOfWork.Customers.GetAll().ToList();

        [Route("КартицаЗгради")]
        public async Task<IActionResult> Buildings(int? buildingId, int? paymentStatusId, string dateFrom, string dateTo)
        {
            ViewBag.Buildings = new SelectList(BuildingsList, "Id", "Name");
            var building = await _unitOfWork.Buildings.GetByIdAsync(
                x => buildingId != null && x.Id == buildingId.Value,
                inc => inc.Include(d => d.Customers)
                    .ThenInclude(c => c.BookFinancials)
            );

            // Jetzt extrahierst du nur die BookFinancial-Elemente
            var bookFinancials =App.FullMapper.Map<List<BookFinancialInfoViewModel>>(building?.Customers
                .SelectMany(c => c.BookFinancials)
                .ToList()); 

            return View(bookFinancials);
        }

        [Route("КартицаСтанари")]
        public async Task<IActionResult> Customers(int? customerId, int? paymentStatusId, string dateFrom, string dateTo)
        {
            ViewBag.Customers = new SelectList(CustomersList, "Id", "CustomerInfo");
            var customer = await _unitOfWork.Customers.GetByIdAsync(x => customerId != null && x.Id == customerId.Value);

            return View();
        }
    }
}
