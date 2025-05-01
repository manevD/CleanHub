using CleanHub.DTO;
using CleanHub.Entities;
using CleanHub.Infrastructure.Data;
using CleanHub.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CleanHub.Controllers
{
    public class CardsController(IUnitOfWork _unitOfWork) : Controller
    {
        public List<Building> BuildingsList { get; set; } = _unitOfWork.Buildings.GetAll(x => x.Include(c => c.Customers)).ToList();
        public List<Customer> CustomersList { get; set; } = _unitOfWork.Customers.GetAll().ToList();

        [Route("КартицаЗгради")]
        public async Task<IActionResult> Buildings(string dateFrom, string dateTo)
        {
            var buildingCards = new List<BuildingsCardViewModel>();
            ViewBag.Buildings = new SelectList(BuildingsList, "Id", "Name");

            DateOnly? DateFrom = null;
            DateOnly? DateTo = null;

            if (!string.IsNullOrEmpty(dateFrom))
            {
                DateFrom = DateOnly.ParseExact(dateFrom, "dd.MM.yyyy", null);
                ViewBag.DateFrom = DateFrom;
            }

            if (!string.IsNullOrEmpty(dateTo))
            {
                DateTo = DateOnly.ParseExact(dateTo, "dd.MM.yyyy", null);
                ViewBag.DateTo = DateTo;
            }

            foreach (var building in BuildingsList)
            {
                if (building.CustomerRefId == null)
                    continue;

                var bookFinancial = _unitOfWork.BookFinancials.GetAllNoTrakcing(
                    inc => inc.Include(x => x.Customer).Where(d =>
                        d.Customer.BuildingId == building.Id &&
                        (!DateFrom.HasValue || d.DatumF >= DateFrom.Value) &&
                        (!DateTo.HasValue || d.DatumF <= DateTo.Value)
                    )).ToList();

                var cost = _unitOfWork.BookFinancials.GetAllNoTrakcing(
                    inc => inc.Include(x => x.Customer).Where(d =>
                        d.CustomerId == building.CustomerRefId &&
                        (!DateFrom.HasValue || d.DatumF >= DateFrom.Value) &&
                        (!DateTo.HasValue || d.DatumF <= DateTo.Value) &&
                        d.DocumentTypId == 5 &&
                        d.InvoiceId == (int)InvoiceTyp.Reserve
                    )).Sum(bf => bf.Owes);

                var buildingsCard = new BuildingsCardViewModel()
                {
                    Name = building.Name,
                    PayedMoney = bookFinancial.Where(d => d.InvoiceId == (int)InvoiceTyp.Recieve).Sum(bf => bf.Demands),
                    ReserveFund = bookFinancial.Where(d => d.InvoiceId == (int)InvoiceTyp.Reserve).Sum(bf => bf.Demands),
                    Cost = cost
                };

                buildingCards.Add(buildingsCard);
            }

            return View(buildingCards);
        }


        [Route("ФинансоваКартица")]
        public async Task<IActionResult> BuildingsFinance(int? buildingId, string dateFrom, string dateTo)
        {
            var cardsViewModel = new CardsViewModel
            {
                BuildingFinanceCardViewModels = new List<BuildingFinanceCardViewModel>()
            };

            ViewBag.Buildings = new SelectList(BuildingsList, "Id", "Name");

            if (buildingId != null || !string.IsNullOrEmpty(dateFrom))
            {
                var building = await _unitOfWork.Buildings.GetByIdAsync(x => buildingId != null && x.Id == buildingId.Value);
                if (building == null)
                    throw new Exception("Building not found.");

                ViewBag.Selected = building.Name;
                ViewBag.Buildings = new SelectList(BuildingsList, "Id", "Name", buildingId);

                DateOnly? DateFrom = null;
                DateOnly? DateTo = null;

                if (!string.IsNullOrEmpty(dateFrom))
                {
                    DateFrom = DateOnly.ParseExact(dateFrom, "dd.MM.yyyy", null);
                    ViewBag.DateFrom = DateFrom;
                }

                if (!string.IsNullOrEmpty(dateTo))
                {
                    DateTo = DateOnly.ParseExact(dateTo, "dd.MM.yyyy", null);
                    ViewBag.DateTo = DateTo;
                }

                var customers = _unitOfWork.Customers.GetAllNoTrakcing(inc =>
                    inc.Include(c => c.BookFinancials.Where(d => d.InvoiceId == (int)InvoiceTyp.Recieve))
                       .Include(c => c.Documents)
                       .Where(x => building.CustomerRefId == x.Id || x.BuildingId == buildingId.Value));

                foreach (var customer in customers)
                {
                    var filteredDocuments = customer.Documents.Where(c =>
                        (!DateFrom.HasValue || c.Date >= DateFrom.Value) &&
                        (!DateTo.HasValue || c.Date <= DateTo.Value));

                    var filteredFinancials = customer.BookFinancials.Where(c =>
                        (!DateFrom.HasValue || c.DatumF >= DateFrom.Value) &&
                        (!DateTo.HasValue || c.DatumF <= DateTo.Value));

                    var buildingFinanceCard = new BuildingFinanceCardViewModel
                    {
                        Adress = customer.Adress,
                        CustomerInfo = customer.CustomerInfo,
                        Demands = filteredDocuments.Sum(bf => bf.TotalOutput ?? 0),
                        Owes = filteredFinancials.Sum(bf => bf.Demands)
                    };

                    cardsViewModel.BuildingFinanceCardViewModels.Add(buildingFinanceCard);
                }

                // Gesamtwerte berechnen
                cardsViewModel.CustomerDemandsTotal = customers
                    .SelectMany(x => x.Documents)
                    .Where(d => (!DateFrom.HasValue || d.Date >= DateFrom.Value) &&
                                (!DateTo.HasValue || d.Date <= DateTo.Value))
                    .Sum(d => d.TotalOutput ?? 0);

                cardsViewModel.CustomerOwesTotal = (float)customers
                    .SelectMany(x => x.BookFinancials)
                    .Where(d => (!DateFrom.HasValue || d.DatumF >= DateFrom.Value) &&
                                (!DateTo.HasValue || d.DatumF <= DateTo.Value))
                    .Sum(d => d.Demands);
            }

            if (cardsViewModel.BuildingFinancial != null && cardsViewModel.BuildingFinancial.Any())
            {
                cardsViewModel.BuildingFinancial = cardsViewModel.BuildingFinancial
                    .OrderBy(x => x.DatumF)
                    .ToList();
            }

            return View(cardsViewModel);
        }


        [Route("КартицаСтанари")]
        public async Task<IActionResult> Customers(int? customerId, string dateFrom, string dateTo)
        {
            var cardsViewModel = new CustomerCard1200ViewModel();
            var newCustomer = new Customer { CustomerInfo = "Сите", Id = 0 };
            CustomersList.Add(newCustomer);
            ViewBag.Customers = new SelectList(CustomersList, "Id", "CustomerInfo");

            if (customerId.HasValue)
            {
                cardsViewModel.CustomerData = new List<CustomerDataDTO>();

                DateOnly? DateFrom = null;
                DateOnly? DateTo = null;

                if (!string.IsNullOrEmpty(dateFrom))
                {
                    DateFrom = DateOnly.ParseExact(dateFrom, "dd.MM.yyyy", null);
                    ViewBag.DateFrom = DateFrom;
                }

                if (!string.IsNullOrEmpty(dateTo))
                {
                    DateTo = DateOnly.ParseExact(dateTo, "dd.MM.yyyy", null);
                    ViewBag.DateTo = DateTo;
                }

                // Dokumente laden
                var dataDocument = _unitOfWork.Documents.GetAllNoTrakcing()
                    .Where(x => x.CustomerId == customerId &&
                                (!DateFrom.HasValue || x.Date >= DateFrom.Value) &&
                                (!DateTo.HasValue || x.Date <= DateTo.Value))
                    .Select(cc => new CustomerDataDTO
                    {
                        Description = cc.ToDocument,
                        Owes = (double)(cc.TotalOutput ?? 0),
                        Demands = 0,
                        DocumentTyp = "Каса прими",
                        Date = cc.Date.Value,
                        Number = cc.Number ?? 0,
                        NumberNalog = 0
                    })
                    .ToList();

                // Finanzdaten laden
                var dataBookFinancial = _unitOfWork.BookFinancials.GetAllNoTrakcing()
                    .Where(x => x.CustomerId == customerId &&
                                (!DateFrom.HasValue || x.DatumF >= DateFrom.Value) &&
                                (!DateTo.HasValue || x.DatumF <= DateTo.Value) &&
                                x.InvoiceId == (int)InvoiceTyp.Recieve)
                    .Select(cc => new CustomerDataDTO
                    {
                        Description = cc.Description,
                        Owes = 0,
                        Demands = cc.Demands,
                        DocumentTyp = "Фактура",
                        Date = cc.DatumF.Value,
                        Number = 0,
                        NumberNalog = cc.OrderN ?? 0
                    })
                    .ToList();

                cardsViewModel.CustomerData.AddRange(dataDocument);
                cardsViewModel.CustomerData.AddRange(dataBookFinancial);

                // Summen (unabhängig vom Datum)
                cardsViewModel.CustomerDemandsTotal = _unitOfWork.BookFinancials
                    .GetAllNoTrakcing()
                    .Where(x => x.CustomerId == customerId && x.InvoiceId == (int)InvoiceTyp.Recieve)
                    .Sum(x => x.Demands);

                cardsViewModel.CustomerOwesTotal = _unitOfWork.Documents
                    .GetAllNoTrakcing()
                    .Where(x => x.CustomerId == customerId)
                    .Sum(x => x.TotalOutput ?? 0);
            }

            if (cardsViewModel.CustomerData != null && cardsViewModel.CustomerData.Any())
            {
                cardsViewModel.CustomerData = cardsViewModel.CustomerData
                    .OrderBy(x => x.Date)
                    .ToList();
            }

            return View(cardsViewModel);
        }


        [Route("КартицаСтанари1201")]
        public async Task<IActionResult> CustomersReserve(int? customerId, string dateFrom, string dateTo)
        {
            var cardsViewModel = new CardsViewModel();
            var newCustomer = new Customer { CustomerInfo = "Сите", Id = 0 };
            CustomersList.Add(newCustomer);

            var bookFinancials = new List<BookFinancialViewModel>();
            ViewBag.Customers = new SelectList(CustomersList, "Id", "CustomerInfo");

            if (customerId.HasValue)
            {
                DateOnly? DateFrom = null;
                DateOnly? DateTo = null;

                if (!string.IsNullOrEmpty(dateFrom))
                {
                    DateFrom = DateOnly.ParseExact(dateFrom, "dd.MM.yyyy", null);
                    ViewBag.DateFrom = DateFrom;
                }

                if (!string.IsNullOrEmpty(dateTo))
                {
                    DateTo = DateOnly.ParseExact(dateTo, "dd.MM.yyyy", null);
                    ViewBag.DateTo = DateTo;
                }

                var rawFinancials = _unitOfWork.BookFinancials.GetAllNoTrakcing()
                    .Where(x =>
                        x.CustomerId == customerId &&
                        x.InvoiceId == (int)InvoiceTyp.Reserve &&
                        (!DateFrom.HasValue || x.DatumF >= DateFrom.Value) &&
                        (!DateTo.HasValue || x.DatumF <= DateTo.Value))
                    .ToList();

                bookFinancials = rawFinancials
                    .Select(bf => new BookFinancialViewModel
                    {
                        Description = bf.Description,
                        InvoiceId = (int)InvoiceTyp.Reserve,
                        Owes = bf.Owes,
                        Demands = bf.Demands,
                        DatumF = bf.DatumF
                    })
                    .ToList();

                // Summen
                cardsViewModel.CustomerDemandsTotal = _unitOfWork.BookFinancials
                    .GetAllNoTrakcing()
                    .Where(x => x.CustomerId == customerId && x.InvoiceId == (int)InvoiceTyp.Reserve)
                    .Sum(x => x.Demands);

                cardsViewModel.CustomerOwesTotal = _unitOfWork.Documents
                    .GetAllNoTrakcing()
                    .Where(x => x.CustomerId == customerId)
                    .Sum(x => x.TotalOutput ?? 0);
            }

            cardsViewModel.CustomerFinanfical = bookFinancials;

            if (cardsViewModel.CustomerFinanfical != null && cardsViewModel.CustomerFinanfical.Any())
            {
                cardsViewModel.CustomerFinanfical = cardsViewModel.CustomerFinanfical
                    .OrderBy(x => x.DatumF)
                    .ToList();
            }

            return View(cardsViewModel);
        }
    }
}
