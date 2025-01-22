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
        public List<Building> BuildingsList { get; set; } = _unitOfWork.Buildings.GetAll(x => x.Include(c => c.Customers)).ToList();
        public List<Customer> CustomersList { get; set; } = _unitOfWork.Customers.GetAll().ToList();

        [Route("КартицаЗгради")]
        public async Task<IActionResult> Buildings(string dateFrom, string dateTo)
        {
            var cardsViewModels = new CardsViewModel();
            var buildingCards = new List<BuildingsCardViewModel>();
            ViewBag.Buildings = new SelectList(BuildingsList, "Id", "Name");
            if (!string.IsNullOrEmpty(dateFrom))
            {
                var DateFrom = DateOnly.ParseExact(dateFrom, "dd.MM.yyyy", null);
                ViewBag.DateFrom = DateFrom;

                if (!string.IsNullOrEmpty(dateTo))
                {
                    var DateTo = DateOnly.ParseExact(dateTo, "dd.MM.yyyy", null);
                    ViewBag.DateTo = DateTo;
                    foreach (var building in BuildingsList)
                    {
                        if (building.CustomerRefId == null)
                        {
                            continue;
                        }
                        var bookFinancial = _unitOfWork.BookFinancials
                            .GetAllNoTrakcing(
                                inc => inc.Include(x => x.Customer).Where(
                                    d => d.Customer.BuildingId == building.Id
                                         && d.DatumF >= DateFrom
                                         && d.DatumF <= DateTo)).ToList();
                        var buildingsCard = new BuildingsCardViewModel()
                        {
                            Name = building.Name,
                            PayedMoney = bookFinancial.Where(d => d.InvoiceId == (int)InvoiceTyp.Recieve).Sum(bf => bf.Demands),
                            ReserveFund = bookFinancial.Where(d => d.InvoiceId == (int)InvoiceTyp.Reserve).Sum(bf => bf.Demands),
                            Cost = _unitOfWork.BookFinancials.GetAllNoTrakcing(
                                        inc => inc.Include(x => x.Customer).Where(
                                            d => d.CustomerId == building.CustomerRefId
                                                 && d.DatumF >= DateFrom
                                                 && d.DatumF <= DateTo
                                                 && d.DocumentTypId == 5
                                                 && d.InvoiceId == (int)InvoiceTyp.Reserve)).Sum(bf => bf.Owes)

                        };
                        if (buildingsCard.Cost != 0)
                        {
                            
                        }
                        buildingCards.Add(buildingsCard);
                    }
                }
                else
                {
                    foreach (var building in BuildingsList)
                    {
                        var bookFinancial = _unitOfWork.BookFinancials
                            .GetAllNoTrakcing(
                                inc => inc.Include(x => x.Customer).Where(
                                    d => d.Customer.BuildingId == building.Id
                                         && d.DatumF >= DateFrom)).ToList();
                        var buildingsCard = new BuildingsCardViewModel()
                        {
                            Name = building.Name,
                            PayedMoney = bookFinancial.Where(d => d.InvoiceId == (int)InvoiceTyp.Recieve).Sum(bf => bf.Demands),
                            ReserveFund = bookFinancial.Where(d => d.InvoiceId == (int)InvoiceTyp.Reserve).Sum(bf => bf.Demands),
                            Cost = _unitOfWork.BookFinancials.GetAllNoTrakcing(
                                inc => inc.Include(x => x.Customer).Where(
                                    d => d.Customer.BuildingId == building.CustomerRefId
                                         && d.DatumF >= DateFrom
                                         && d.DocumentTypId.Value == 5
                                         && d.InvoiceId == (int)InvoiceTyp.Reserve)).Sum(bf => bf.Demands)

                        };
                        buildingCards.Add(buildingsCard);
                    }
                }
            }
            return View(buildingCards);
        }

        [Route("ФинансоваКартица")]
        public async Task<IActionResult> BuildingsFinance(int? buildingId, string dateFrom, string dateTo)
        {
            var cardsViewModel = new CardsViewModel();
            var bookFinancials = new List<BookFinancialViewModel>();
            ViewBag.Buildings = new SelectList(BuildingsList, "Id", "Name");

            if (buildingId != null || !string.IsNullOrEmpty(dateFrom))
            {
                var DateFrom = DateOnly.ParseExact(dateFrom, "dd.MM.yyyy", null);
                ViewBag.DateFrom = DateFrom;

                ViewBag.Buildings = new SelectList(BuildingsList, "Id", "Name", buildingId);

                var building = await _unitOfWork.Buildings.GetByIdAsync(
                    x => buildingId != null && x.Id == buildingId.Value);

                if (building == null)
                {
                    throw new Exception("Building not found.");
                }

                ViewBag.Selected = building.Name;
                var customers = _unitOfWork.Customers.GetAllNoTrakcing(inc =>
                    inc.Include(c => c.BookFinancials.Where(d => d.InvoiceId == (int)InvoiceTyp.Reserve))
                        .Where(x => building.CustomerRefId == x.Id || x.BuildingId == buildingId.Value));

                if (!string.IsNullOrEmpty(dateTo))
                {
                    var DateTo = DateOnly.ParseExact(dateTo, "dd.MM.yyyy", null);
                    ViewBag.DateTo = DateTo;
                    bookFinancials = App.FullMapper.Map<List<BookFinancialViewModel>>(customers
                        .SelectMany(c => c.BookFinancials.Where(d => d.InvoiceId == (int)InvoiceTyp.Reserve))
                        .Where(bf =>
                            bf.DatumF >= DateFrom && bf.DatumF <= DateTo).ToList());
                }
                else
                {
                    bookFinancials = App.FullMapper.Map<List<BookFinancialViewModel>>(customers
                        .SelectMany(c => c.BookFinancials.Where(d => d.InvoiceId == (int)InvoiceTyp.Reserve))
                        .Where(x => x.DatumF >= DateFrom).ToList());
                }

                cardsViewModel.BuildingFinancial = bookFinancials;
                return View(cardsViewModel);
            }

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

                if (!string.IsNullOrEmpty(dateTo))
                {
                    var DateTo = DateOnly.ParseExact(dateTo, "dd.MM.yyyy", null);
                    ViewBag.DateTo = DateTo;

                    bookFinancials = _unitOfWork.BookFinancials.GetAllNoTrakcing()
                        .Where(x => x.DatumF >= DateFrom && x.DatumF <= DateTo).ToList()
                        .Select(bf => new BookFinancialViewModel()
                        {
                            Description = bf.Description,
                            InvoiceId = bf.InvoiceId.Value,
                            Owes = bf.Owes,
                            Demands = bf.Demands,
                            DatumF = bf.DatumF
                        }).ToList();
                }
            }

            cardsViewModel.CustomerFinanfical = bookFinancials;
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
                var DateFrom = DateOnly.ParseExact(dateFrom, "dd.MM.yyyy", null);
                ViewBag.DateFrom = DateFrom;

                if (!string.IsNullOrEmpty(dateTo))
                {
                    var DateTo = DateOnly.ParseExact(dateTo, "dd.MM.yyyy", null);
                    ViewBag.DateTo = DateTo;
                    bookFinancials = _unitOfWork.BookFinancials.GetAllNoTrakcing()
                        .Where(x => x.DatumF >= DateFrom && x.DatumF <= DateTo &&
                                    x.InvoiceId == (int)InvoiceTyp.Reserve).ToList()
                        .Select(bf => new BookFinancialViewModel()
                        {
                            Description = bf.Description,
                            InvoiceId = (int)InvoiceTyp.Reserve,
                            Owes = bf.Owes,
                            Demands = bf.Demands,
                            DatumF = bf.DatumF
                        }).ToList();
                }
            }

            cardsViewModel.CustomerFinanfical = bookFinancials;
            return View(cardsViewModel);
        }
    }
}
