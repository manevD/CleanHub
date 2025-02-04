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
            cardsViewModel.BuildingFinanceCardViewModels = new List<BuildingFinanceCardViewModel>();
            var bookFinancials = new List<BuildingFinanceCardViewModel>();
            ViewBag.Buildings = new SelectList(BuildingsList, "Id", "Name");
          
            if (buildingId != null || !string.IsNullOrEmpty(dateFrom))
            {
                var building = await _unitOfWork.Buildings.GetByIdAsync(
                    x => buildingId != null && x.Id == buildingId.Value);
                var customers = _unitOfWork.Customers.GetAllNoTrakcing(inc =>
                    inc.Include(c => c.BookFinancials.Where(d => d.InvoiceId == (int)InvoiceTyp.Recieve)).Include(c => c.Documents)
                        .Where(x => building.CustomerRefId == x.Id || x.BuildingId == buildingId.Value));
                var DateFrom = DateOnly.ParseExact(dateFrom, "dd.MM.yyyy", null);
                ViewBag.DateFrom = DateFrom;

                ViewBag.Buildings = new SelectList(BuildingsList, "Id", "Name", buildingId);


                if (building == null)
                {
                    throw new Exception("Building not found.");
                }

                ViewBag.Selected = building.Name;
         

                if (!string.IsNullOrEmpty(dateTo))
                {
                    var DateTo = DateOnly.ParseExact(dateTo, "dd.MM.yyyy", null);
                    ViewBag.DateTo = DateTo;
                    //Demands
                    //SUM(d.TotalOutput) AS TotalOutputSum
                    //FROM Documents d
                    //    WHERE d.CustomerId = 769
                    //AND d.Date BETWEEN '2024-05-01' AND '2024-11-01';

                    //Owes
                    //SELECT
                    //SUM(Demands) AS TotalDemands
                    //FROM BookFinancials
                    //WHERE CustomerId = 769
                    //AND DatumF BETWEEN '2024-05-01' AND '2024-11-01'
                    //AND InvoiceId = 1200;

                    foreach (var customer in customers)
                    {
                        var buildingFinanceCard= new BuildingFinanceCardViewModel
                        {
                            Adress =customer.Adress,
                            CustomerInfo = customer.CustomerInfo,
                            Demands = customer.Documents.Where( c => c.Date >= DateFrom
                                && c.Date <= DateTo).Sum(bf => bf.TotalOutput.Value),
                            Owes = customer.BookFinancials.Where(c => c.DatumF >= DateFrom
                                                                      && c.DatumF <= DateTo).Sum(bf => bf.Demands)
                        };
                        cardsViewModel.BuildingFinanceCardViewModels.Add(buildingFinanceCard);
                    }
                }
                else
                {
                    foreach (var customer in customers)
                    {
                        var buildingFinanceCard = new BuildingFinanceCardViewModel
                        {
                            Adress = customer.Adress,
                            CustomerInfo = customer.CustomerInfo,
                            Demands = customer.Documents.Where(c => c.CustomerId == customer.Id && c.Date >= DateFrom).Sum(bf => bf.TotalOutput.Value),
                            Owes = customer.BookFinancials.Where(c => c.DatumF >= DateFrom).Sum(bf => bf.Demands)
                        };
                        cardsViewModel.BuildingFinanceCardViewModels.Add(buildingFinanceCard);
                    }
                }
                cardsViewModel.CustomerDemandsTotal = customers.SelectMany(x => x.Documents).Sum(d => d.TotalOutput.Value);
                cardsViewModel.CustomerOwesTotal = (float)customers.SelectMany(x => x.BookFinancials).Sum(d => d.Demands);
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
                var DateFrom = DateOnly.ParseExact(dateFrom, "dd.MM.yyyy", null);
                ViewBag.DateFrom = DateFrom;
                if (!string.IsNullOrEmpty(dateTo))
                {
                    var DateTo = DateOnly.ParseExact(dateTo, "dd.MM.yyyy", null);
                    ViewBag.DateTo = DateTo;
                    var dataDocument = _unitOfWork.Documents.GetAllNoTrakcing().Where(x =>
                            x.CustomerId == customerId && x.Date >= DateFrom && x.Date <= DateTo)
                        .Select(cc => new CustomerDataDTO()
                        { 
                             Description = cc.ToDocument,
                             Owes = (double)cc.TotalOutput,
                             Demands = 0,
                             DocumentTyp = "Каса прими",
                             Date = cc.Date.Value,
                             Number = cc.Number.Value,
                             NumberNalog = 0
                        }).ToList();

                    var dataBookFinancial = _unitOfWork.BookFinancials.GetAllNoTrakcing().Where(x =>
                            x.CustomerId == customerId && x.DatumF >= DateFrom && x.DatumF <= DateTo && x.InvoiceId == (int)InvoiceTyp.Recieve)
                        .Select(cc => new CustomerDataDTO()
                        {
                            Description = cc.Description,
                            Owes = 0,
                            Demands = cc.Demands,
                            DocumentTyp = "Фактура",
                            Date = cc.DatumF.Value,
                            Number = 0,
                            NumberNalog =  cc.OrderN.Value
                        }).ToList();
                    cardsViewModel.CustomerData.AddRange(dataDocument);
                    cardsViewModel.CustomerData.AddRange(dataBookFinancial);
                }
                else
                {
                    var dataDocument = _unitOfWork.Documents.GetAllNoTrakcing().Where(x =>
                            x.CustomerId == customerId && x.Date >= DateFrom)
                        .Select(cc => new CustomerDataDTO()
                        {
                            Description = cc.ToDocument,
                            Owes = (double)cc.TotalOutput,
                            Demands = 0,
                            DocumentTyp = "Каса прими",
                            Date = cc.Date.Value,
                            Number = cc.Number.Value,
                            NumberNalog = 0
                        }).ToList();

                    var dataBookFinancial = _unitOfWork.BookFinancials.GetAllNoTrakcing().Where(x =>
                            x.CustomerId == customerId && x.DatumF >= DateFrom  && x.InvoiceId == (int)InvoiceTyp.Recieve)
                        .Select(cc => new CustomerDataDTO()
                        {
                            Description = cc.Description,
                            Owes = 0,
                            Demands = cc.Demands,
                            DocumentTyp = "Фактура",
                            Date = cc.DatumF.Value,
                            Number = 0,
                            NumberNalog = cc.OrderN.Value
                        }).ToList();
                    cardsViewModel.CustomerData.AddRange(dataDocument);
                    cardsViewModel.CustomerData.AddRange(dataBookFinancial);
                }

                cardsViewModel.CustomerDemandsTotal = _unitOfWork.BookFinancials.GetAllNoTrakcing().Where(x => x.CustomerId == customerId && x.InvoiceId == 1200).Sum(x => x.Demands);
                cardsViewModel.CustomerOwesTotal = _unitOfWork.Documents.GetAllNoTrakcing().Where(x =>
                    x.CustomerId == customerId).Sum(x=>x.TotalOutput.Value);
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
                var DateFrom = DateOnly.ParseExact(dateFrom, "dd.MM.yyyy", null);
                ViewBag.DateFrom = DateFrom;

                if (!string.IsNullOrEmpty(dateTo))
                {
                    var DateTo = DateOnly.ParseExact(dateTo, "dd.MM.yyyy", null);
                    ViewBag.DateTo = DateTo;
                    bookFinancials = _unitOfWork.BookFinancials.GetAllNoTrakcing()
                        .Where(x => x.DatumF >= DateFrom && x.DatumF <= DateTo &&
                                    x.InvoiceId == (int)InvoiceTyp.Reserve && x.CustomerId == customerId).ToList()
                        .Select(bf => new BookFinancialViewModel()
                        {
                            Description = bf.Description,
                            InvoiceId = (int)InvoiceTyp.Reserve,
                            Owes = bf.Owes,
                            Demands = bf.Demands,
                            DatumF = bf.DatumF
                        }).ToList();
                }
                else
                {
                        bookFinancials = _unitOfWork.BookFinancials.GetAllNoTrakcing()
                            .Where(x => x.DatumF >= DateFrom &&
                                        x.InvoiceId == (int)InvoiceTyp.Reserve && x.CustomerId == customerId).ToList()
                            .Select(bf => new BookFinancialViewModel
                            {
                                Description = bf.Description,
                                InvoiceId = (int)InvoiceTyp.Reserve,
                                Owes = bf.Owes,
                                Demands = bf.Demands,
                                DatumF = bf.DatumF
                            }).ToList();
                }
            }
            cardsViewModel.CustomerDemandsTotal = _unitOfWork.BookFinancials.GetAllNoTrakcing().Where(x => x.CustomerId == customerId && x.InvoiceId == 1200).Sum(x => x.Demands);
            cardsViewModel.CustomerOwesTotal = _unitOfWork.Documents.GetAllNoTrakcing().Where(x =>
                x.CustomerId == customerId).Sum(x => x.TotalOutput.Value); cardsViewModel.CustomerFinanfical = bookFinancials;
            return View(cardsViewModel);
        }
    }
}
