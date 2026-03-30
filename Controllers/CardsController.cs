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
        public List<Customer> CustomersList { get; set; } = _unitOfWork.Customers.GetAll().Where(x => !x.Hide).ToList();

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
                building.CustomerRefId = building.CustomerRefId ?? building.Id;

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
        public async Task<IActionResult> BuildingsInvoice1200()
        {
            var buildings = _unitOfWork.Buildings.GetAll(x => x.Include(c => c.Customers).ThenInclude(d => d.Documents)).ToList();
            return View(buildings);
        }
        public async Task<IActionResult> BuildingInvoice1200(int? buildingId)
        {
            var customers = new List<Customer>();
            if (buildingId.HasValue)
            {
                ViewBag.Buildings = new SelectList(BuildingsList, "Id", "Name", buildingId.Value);
                ViewBag.SelectedBuildingName = BuildingsList.FirstOrDefault(x=>x.Id == buildingId.Value).Name;

                ViewBag.BuildingId = buildingId.Value;
                customers =  _unitOfWork.Customers.GetAllNoTrakcing(inc => inc.Include(c => c.Documents).Where(x => x.BuildingId == buildingId.Value)).ToList();
                return View(customers);
            }
            ViewBag.Buildings = new SelectList(BuildingsList, "Id", "Name",1);
            ViewBag.SelectedBuildingName = BuildingsList.FirstOrDefault().Name;

            ViewBag.BuildingId = 1;
            customers = _unitOfWork.Customers.GetAllNoTrakcing(inc => inc.Include(c => c.Documents).Where(x => x.BuildingId == 1)).ToList();

            return View(customers);
        }

        public async Task<IActionResult> BuildingsReserve(int? buildingId, string dateFrom, string dateTo)
        {
            var cardsViewModel = new CardsViewModel();
            ViewBag.Buildings = new SelectList(BuildingsList, "Id", "Name");
            if (buildingId.HasValue)
            {
                var bookFinancials = new List<BookFinancialViewModel>();
                var building = await _unitOfWork.Buildings.GetByIdAsync(x => x.Id == buildingId.Value);
                var customerId = building.CustomerRefId ?? building.Id;
                if (customerId != null)
                {
                    ViewBag.Buildings = new SelectList(BuildingsList, "Id", "Name", building.Id);
                    ViewBag.SelectedBuildingName = building.Name;
                    ViewBag.BuildingId = building.Id;
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
                            x.InvoiceId == (int)InvoiceTyp.Reserve && !x.Description.Contains("салдо") &&
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

                    //Summen
                    //cardsViewModel.CustomerDemandsTotal = _unitOfWork.BookFinancials
                    //    .GetAllNoTrakcing()
                    //    .Where(x => x.CustomerId == customerId && x.InvoiceId == (int)InvoiceTyp.Reserve)
                    //    .Sum(x => x.Demands);

                    //cardsViewModel.CustomerOwesTotal = _unitOfWork.Documents
                    //    .GetAllNoTrakcing()
                    //    .Where(x => x.CustomerId == customerId)
                    //    .Sum(x => x.TotalOutput ?? 0);
                }

                cardsViewModel.BuildingFinancial = bookFinancials;

                if (cardsViewModel.BuildingFinancial != null && cardsViewModel.BuildingFinancial.Any())
                {
                    cardsViewModel.BuildingFinancial = cardsViewModel.BuildingFinancial
                        .OrderBy(x => x.DatumF)
                        .ToList();
                }

            }
            return View(cardsViewModel);
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

                ViewBag.Buildings = new SelectList(BuildingsList, "Id", "Name", building.Id);
                ViewBag.SelectedBuildingName = building.Name;
                ViewBag.BuildingId = building.Id;

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

                foreach (var customer in customers.Where(x => !x.Hide))
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
                        Owes = filteredDocuments.Sum(bf => bf.TotalOutput ?? 0),
                        Demands = filteredFinancials.Sum(bf => bf.Demands)
                    };

                    cardsViewModel.BuildingFinanceCardViewModels.Add(buildingFinanceCard);
                }

                cardsViewModel.CustomerOwesTotal = customers
                    .SelectMany(x => x.Documents)
                    .Where(d => (!DateFrom.HasValue || d.Date >= DateFrom.Value) &&
                                (!DateTo.HasValue || d.Date <= DateTo.Value))
                    .Sum(d => d.TotalOutput ?? 0);

                cardsViewModel.CustomerDemandsTotal = (float)customers
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
                var customer = await _unitOfWork.Customers.GetByIdAsync(x => x.Id == customerId.Value);
                DateOnly? DateFrom = null;
                DateOnly? DateTo = null;
                ViewBag.Customers = new SelectList(CustomersList, "Id", "CustomerInfo", customer.CustomerInfo);
                ViewBag.SelectedCustomerName = customer.CustomerInfo;
                ViewBag.CustomerId = customer.Id;
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
                        DocumentTyp = "Фактура",
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
                                (x.InvoiceId == (int)InvoiceTyp.Recieve || x.DocumentTypId == 11))
                    .Select(cc => new CustomerDataDTO
                    {
                        Description = cc.Description,
                        Owes = cc.Owes,
                        Demands = cc.Demands,
                        DocumentTyp = string.Equals(cc.Description, "салдо", StringComparison.OrdinalIgnoreCase)
                            ? "затварање"
                            : "Каса прими",
                        Date = cc.DatumF.Value,
                        Number = 0,
                        DontSum = cc.DontSum,
                        NumberNalog = cc.OrderN ?? 0
                    })
                    .ToList();

                cardsViewModel.CustomerData.AddRange(dataDocument);
                cardsViewModel.CustomerData.AddRange(dataBookFinancial);
                cardsViewModel.CustomerDemandsTotal = dataBookFinancial.Where(x => !x.DontSum && x.Date > new DateOnly(2021, 1, 1)).Sum(x => x.Demands);
                cardsViewModel.CustomerOwesTotal = (float)dataDocument.Where(x => x.Date > new DateOnly(2021, 1, 1)).Sum(x => x.Owes);

                if (dataBookFinancial.Any(x => x.Owes != 0 && x.Date >= new DateOnly(2021, 1, 1)))
                {
                    cardsViewModel.CustomerOwesTotal += (float)dataBookFinancial.Where(x => x.Owes != 0 && x.Date >= new DateOnly(2021, 1, 1)).Sum(x => x.Owes);
                }
                // Summen (unabhängig vom Datum)
                //cardsViewModel.CustomerDemandsTotal = _unitOfWork.BookFinancials
                //    .GetAllNoTrakcing()
                //    .Where(x => x.CustomerId == customerId && x.InvoiceId == (int)InvoiceTyp.Recieve)
                //    .Sum(x => x.Demands);

                //cardsViewModel.CustomerOwesTotal = _unitOfWork.Documents
                //    .GetAllNoTrakcing()
                //    .Where(x => x.CustomerId == customerId)
                //    .Sum(x => x.TotalOutput ?? 0);
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
                var customer = await _unitOfWork.Customers.GetByIdAsync(x => x.Id == customerId.Value);

                ViewBag.Customers = new SelectList(CustomersList, "Id", "CustomerInfo", customer.CustomerInfo);
                ViewBag.SelectedCustomerName = customer.CustomerInfo;
                ViewBag.CustomerId = customer.Id;
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

                cardsViewModel.CustomerOwesTotal = (float)_unitOfWork.BookFinancials
                    .GetAllNoTrakcing()
                    .Where(x => x.CustomerId == customerId && x.InvoiceId == (int)InvoiceTyp.Reserve)
                    .Sum(x => x.Owes);
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
