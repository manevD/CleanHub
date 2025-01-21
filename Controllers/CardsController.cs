using CleanHub.CleanHub.Infrastructure.Data;
using CleanHub.Entities;
using CleanHub.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace CleanHub.Controllers
{
    public class CardsController(IUnitOfWork _unitOfWork) : Controller
    {
        public List<Building> BuildingsList { get; set; } = _unitOfWork.Buildings.GetAll().ToList();
        public List<Customer> CustomersList { get; set; } = _unitOfWork.Customers.GetAll().ToList();

        [Route("КартицаЗгради")]
        public async Task<IActionResult> Buildings(int? buildingId, string dateFrom, string dateTo)
        {
            var cardsViewModel = new CardsViewModel();
            var newBuilding = new Building { Name = "Сите", Id = 0 };
            var bookFinancials = new List<BookFinancialViewModel>();
            BuildingsList.Add(newBuilding);
            ViewBag.Buildings = new SelectList(BuildingsList, "Id", "Name");
            if (buildingId != null || !string.IsNullOrEmpty(dateFrom))
            {
                var DateFrom = DateOnly.ParseExact(dateFrom, "dd.MM.yyyy", null);
                ViewBag.DateFrom = DateFrom;

                if (buildingId != 0)
                {
                    ViewBag.Buildings = new SelectList(BuildingsList, "Id", "Name", buildingId);
                    // Hole das Building anhand der ID
                    var building = await _unitOfWork.Buildings.GetByIdAsync(
                        x => buildingId != null && x.Id == buildingId.Value);

                    if (building == null)
                    {
                        throw new Exception("Building not found.");
                    }
                    ViewBag.Selected = building.Name;
                    var customers = _unitOfWork.Customers.GetAllNoTrakcing(inc =>
                        inc.Include(c => c.BookFinancials)
                            .Where(x => building.CustomerRefId == x.Id || x.BuildingId == buildingId.Value));
                    if (!string.IsNullOrEmpty(dateTo))
                    {
                        var DateTo = DateOnly.ParseExact(dateTo, "dd.MM.yyyy", null);
                        ViewBag.DateTo = DateTo;
                        // Filtere die BookFinancials basierend auf Datum und BuildingId
                        bookFinancials = App.FullMapper.Map<List<BookFinancialViewModel>>(customers
                            .SelectMany(c => c.BookFinancials)
                            .Where(bf =>
                                (bf.DatumF >= DateFrom && bf.DatumF <= DateTo)).ToList());
                    }
                    // Setze den ViewBag-Wert

                    else
                    {
                        bookFinancials = App.FullMapper.Map<List<BookFinancialViewModel>>(customers
                           .SelectMany(c => c.BookFinancials).Where(x =>
                               (x.DatumF >= DateFrom))
                           .ToList());
                    }
                }
                else
                {
                    if (!string.IsNullOrEmpty(dateTo))
                    {
                        var DateTo = DateOnly.ParseExact(dateTo, "dd.MM.yyyy", null);
                        ViewBag.DateTo = DateTo;
                        bookFinancials = _unitOfWork.BookFinancials.GetAllNoTrakcing()
                       .Select(bf => new BookFinancialViewModel()
                       {
                           Description = bf.Description,
                           InvoiceId = bf.InvoiceId.Value,
                           Owes = bf.Owes,
                           Demands = bf.Demands,
                           DatumF = bf.DatumF
                       }).Where(x =>
                           x.DatumF >= DateFrom && x.DatumF <= DateTo).ToList();
                    }
                    else
                    {
                        bookFinancials = _unitOfWork.BookFinancials.GetAllNoTrakcing()
                            .Select(bf => new BookFinancialViewModel()
                            {
                                Description = bf.Description,
                                InvoiceId = bf.InvoiceId.Value,
                                Owes = bf.Owes,
                                Demands = bf.Demands,
                                DatumF = bf.DatumF
                            }).Where(x =>
                           (x.DatumF >= DateFrom)).ToList();
                    }
                }
            }
            cardsViewModel.BuildingFinancial = bookFinancials;

            return View(cardsViewModel);
        }

        [Route("КартицаСтанари")]
        public async Task<IActionResult> Customers(int? customerId, string dateFrom, string dateTo)
        {
            var cardsViewModel = new CardsViewModel();
            var newCustomer = new Customer { CustomerInfo = "Сите", Id = 0 };
            CustomersList.Add(newCustomer);
            var bookFinancials = new List<BookFinancialViewModel>();
            ViewBag.Customers = new SelectList(CustomersList, "Id", "CustomerInfo");
            if (customerId.HasValue)
            {
                var DateFrom = DateOnly.ParseExact(dateFrom, "dd.MM.yyyy", null);
                ViewBag.DateFrom = DateFrom;
                if (customerId == 0)
                {
                    var customers = new List<Customer>();
                    if (!string.IsNullOrEmpty(dateTo))
                    {
                        var DateTo = DateOnly.ParseExact(dateTo, "dd.MM.yyyy", null);
                        ViewBag.DateTo = DateTo;

                        bookFinancials = _unitOfWork.BookFinancials.GetAllNoTrakcing().Where(x=>x.DatumF >= DateFrom && x.DatumF <= DateTo).ToList().Select(bf => new BookFinancialViewModel()
                        {
                            Description = bf.Description,
                            InvoiceId = bf.InvoiceId.Value,
                            Owes = bf.Owes,
                            Demands = bf.Demands,
                            DatumF = bf.DatumF
                        }).ToList();
                        bookFinancials.AddRange(_unitOfWork.Documents.GetAllNoTrakcing().Where(x => x.Date >= DateFrom && x.Date <= DateTo).ToList().Select(bf => new BookFinancialViewModel()
                        {
                            Description = bf.ToDocument,
                            Owes = bf.TotalOutput.Value,
                            InvoiceId = 1200,
                            DatumF = bf.Date
                        }).ToList());
                    }
                    else
                    {
                        bookFinancials = _unitOfWork.BookFinancials.GetAllNoTrakcing().Where(x => x.DatumF >= DateFrom).ToList().Select(bf => new BookFinancialViewModel()
                        {
                            Description = bf.Description,
                            InvoiceId = bf.InvoiceId.Value,
                            Owes = bf.Owes,
                            Demands = bf.Demands,
                            DatumF = bf.DatumF
                        }).ToList();
                        bookFinancials.AddRange(_unitOfWork.Documents.GetAllNoTrakcing().Where(x => x.Date >= DateFrom).ToList().Select(bf => new BookFinancialViewModel()
                        {
                            Description = bf.ToDocument,
                            Owes = bf.TotalOutput.Value,
                            InvoiceId = 1200,
                            DatumF = bf.Date
                        }).ToList());
                    }
                }
                else
                {
                    if (!string.IsNullOrEmpty(dateTo))
                    {
                        var DateTo = DateOnly.ParseExact(dateTo, "dd.MM.yyyy", null);
                        ViewBag.DateTo = DateTo;
                        bookFinancials = _unitOfWork.BookFinancials.GetAllNoTrakcing().Where(x => x.DatumF >= DateFrom && x.DatumF <= DateTo && x.CustomerId == customerId.Value).ToList().Select(bf => new BookFinancialViewModel()
                        {
                            Description = bf.Description,
                            InvoiceId = bf.InvoiceId.Value,
                            Owes = bf.Owes,
                            Demands = bf.Demands,
                            DatumF = bf.DatumF
                        }).ToList(); ;
                        bookFinancials.AddRange(_unitOfWork.Documents.GetAllNoTrakcing().Where(x => x.Date >= DateFrom && x.Date <= DateTo && x.CustomerId == customerId.Value).ToList().Select(bf => new BookFinancialViewModel()
                        {
                            Description = bf.ToDocument,
                            Owes = bf.TotalOutput.Value,
                            InvoiceId = 1200,
                            DatumF = bf.Date
                        }).ToList());
                    }
                    else
                    {
                        bookFinancials = _unitOfWork.BookFinancials.GetAllNoTrakcing().Where(x => x.DatumF >= DateFrom && x.CustomerId == customerId.Value).ToList().Select(bf => new BookFinancialViewModel()
                        {
                            Description = bf.Description,
                            InvoiceId = bf.InvoiceId.Value,
                            Owes = bf.Owes,
                            Demands = bf.Demands,
                            DatumF = bf.DatumF
                        }).ToList();
                        bookFinancials.AddRange(_unitOfWork.Documents.GetAllNoTrakcing().Where(x => x.Date >= DateFrom && x.CustomerId == customerId.Value).ToList().Select(bf => new BookFinancialViewModel()
                        {
                            Description = bf.ToDocument,
                            Owes = bf.TotalOutput.Value,
                            InvoiceId = 1200,
                            DatumF = bf.Date
                        }).ToList());
                    }
                }
            }

            cardsViewModel.CustomerFinanfical = bookFinancials;

            return View(cardsViewModel);
        }

    }
}
