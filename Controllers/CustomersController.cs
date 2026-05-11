using CleanHub.Attribute;
using CleanHub.Entities;
using CleanHub.Infrastructure.Data;
using CleanHub.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CleanHub.Controllers
{
    [RequireLogin]

    public class CustomersController(IUnitOfWork _unitOfWork, ApplicationDbMartiContext _context) : Controller
    {
        private static DateOnly DateFrom = DateOnly.FromDateTime(DateTime.Now);
        private static DateOnly DateTo = DateOnly.FromDateTime(DateTime.Now);
        public List<Building> BuildingsList { get; set; } = _unitOfWork.Buildings.GetAll().ToList();
        public List<Activity> ActivitiesList { get; set; } = _unitOfWork.Activities.GetAll().ToList();

        // GET: Customers
        [Route("Станари")]
        public async Task<IActionResult> Index([FromServices] IMemoryCache cache)
        {
            var customers = await cache.GetOrCreateAsync("Customers", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);

                return _unitOfWork.Customers
                    .GetAllNoTrakcing()
                    .Where(x => !x.Hide)
                    .Select(c => new CustomerViewModel
                    {
                        Id = c.Id,
                        CustomerInfo = c.CustomerInfo ?? string.Empty,
                        Email = c.Email,
                        Subscription = c.Subscription ?? 0,
                        PhoneNumber = c.PhoneNumber,
                        Inactive = c.Inactive ?? false,
                        Adress = c.Adress
                    }).ToList();
            });

            return View(customers);
        }


        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var customer = await _unitOfWork.Customers.GetByIdAsync(cus => cus.Id == id, x => x.Include(c => c.Activity).Include(c => c.Documents));
            if (customer == null)
            {
                return NotFound();
            }
            var customerViewModel = App.FullMapper.Map<CustomerViewModel>(customer);

            return View(customerViewModel);
        }

        // GET: Customers/Details/5
        public async Task<IActionResult> DetailsFiltered(int? id, string? dateFrom, string? dateTo)
        {
            if (id == null)
                return NotFound();

            DateFrom = !string.IsNullOrEmpty(dateFrom)
                ? DateOnly.ParseExact(dateFrom, "dd.MM.yyyy", null)
                : DateFrom;

            DateTo = !string.IsNullOrEmpty(dateTo)
                ? DateOnly.ParseExact(dateTo, "dd.MM.yyyy", null)
                : DateTo;

            ViewBag.DateFrom = DateFrom.ToString("dd.MM.yyyy");
            ViewBag.DateTo = DateTo.ToString("dd.MM.yyyy");

            var customer = await _unitOfWork.Customers.GetByIdAsync(x => x.Id == id, c => c.Include(cust => cust.Documents));

            if (customer == null)
                return NotFound();

            // Filtere Dokumente nach Datum
            if (customer.Documents != null)
            {
                customer.Documents = customer.Documents
                    .Where(d => d.Date >= DateFrom && d.Date <= DateTo)
                    .ToList();
            }

            var viewModel = App.FullMapper.Map<CustomerViewModel>(customer);
            return View("Details", viewModel);
        }

        // GET: Customers/Create
        public IActionResult CreateWithBuilding(int buildingId)
        {
            ViewData["ActivityId"] = new SelectList(ActivitiesList, "Id", "Name");
            ViewData["BuildingId"] = new SelectList(BuildingsList, "Id", "Name", buildingId);
            var customer = new CustomerViewModel
            {
                BuildingId = buildingId
            };
            return View(nameof(Create), customer);
        }

        public IActionResult CreateWithModel(CustomerViewModel customer)
        {
            ViewData["BuildingId"] = new SelectList(BuildingsList, "Id", "Name", customer.BuildingId);
            ViewData["ActivityId"] = new SelectList(ActivitiesList, "Id", "Name", customer.ActivityId);

            return View(nameof(Create), customer);
        }

        // GET: customers/Create
        [Route("КреирајСтанар")]
        [HttpGet("Create")] // Ermöglicht Zugriff auf /Customers/Create
        public IActionResult Create(int? buildingId, CustomerViewModel? customer)
        {
            customer ??= new CustomerViewModel();
            if (buildingId.HasValue)
                customer.BuildingId = buildingId.Value;

            ViewData["BuildingId"] = new SelectList(BuildingsList, "Id", "Name", customer.BuildingId);
            ViewData["ActivityId"] = new SelectList(ActivitiesList, "Id", "Name", customer.ActivityId);

            return View(customer);
        }
        // POST: customer/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost("Create")]
        public async Task<IActionResult> Create(CustomerViewModel customer)
        {
            if (ModelState.IsValid)
            {
                var customerEntity = App.FullMapper.Map<Customer>(customer);
                customerEntity.Inactive = false;
                _unitOfWork.Customers.Add(customerEntity);
                await _unitOfWork.SaveChangesAsync();
                var partner = new PartneriTest
                {
                    PartnerID = customerEntity.Id,
                    parAdresa = customerEntity.Adress,
                    Partner = customerEntity.CustomerInfo,
                };
                _context.PartneriTest.Add(partner);
                _context.SaveChanges();
                return RedirectToAction(nameof(Index));
            }
            PopulateViewData(customer.BuildingId, customer.ActivityId);

            return View(customer);
        }

        // GET: customer/Edit/5
        [HttpGet]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }
            var customerEntity = await _unitOfWork.Customers.GetByIdAsync(c => c.Id == id, cu => cu.Include(cust => cust.Activity).Include(cus => cus.Documents).Include(x => x.Activity).Include(d => d.Documents));
            //if (customerEntity?.Documents != null && customerEntity.Documents.Any())
            //{
            //    foreach (var doc in customerEntity.Documents)
            //    {
            //        if (doc.ToDocument != null)
            //        {
            //            var year = DocumentService.ExtractYear(doc.ToDocument);
            //            var month = DocumentService.ExtractMonth(doc.ToDocument);
            //            var searchCriteria = string.Concat(month, "/", year);

            //            var bookFinancial = await _context.BookFinancials.FirstOrDefaultAsync(x => x.Description != null && x.Description.Contains(searchCriteria) && x.InvoiceId == Constants.Recieve);
            //            doc.PaymentStatus = DocumentService.GetStatus(bookFinancial, doc);
            //        }
            //    }
            //}
            var customer = App.FullMapper.Map<CustomerViewModel>(customerEntity);

            PopulateViewData(customer.BuildingId, customer.ActivityId);

            return View(customer);
        }

        // POST: customers/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, CustomerViewModel customer)
        {
            if (id != customer.Id)
                return NotFound();

            try
            {
                if (ModelState.IsValid)
                {
                    var existingCustomer = await _unitOfWork.Customers.GetByIdAsync(x => x.Id == id);
                    
                    if (existingCustomer == null)
                        return NotFound();
                   
                    // Prüfe Inaktivitätslogik
                    var navigateToCreate = (!existingCustomer.Inactive.HasValue && customer.Inactive == true)
                                           || (existingCustomer.Inactive == false && customer.Inactive == true);
                    customer.Building = App.FullMapper.Map<BuildingViewModel>(BuildingsList.FirstOrDefault(b => b.Id == customer.BuildingId));
                    if (customer.Building != null && customer.Building.CustomerRefId.HasValue)
                    {
                        var buildingCustomer = await _unitOfWork.Customers
                            .GetByIdAsync(x => x.Id == customer.Building.CustomerRefId);

                        if (buildingCustomer != null)
                        {
                            if (customer.Saldo1201 != existingCustomer.Saldo1201)
                            {
                                var diff1201 = customer.Saldo1201 - existingCustomer.Saldo1201;

                                buildingCustomer.Saldo1201 -= diff1201;
                            }

                            if (customer.Saldo != existingCustomer.Saldo)
                            {
                                var diffSaldo = customer.Saldo - existingCustomer.Saldo;

                                buildingCustomer.Saldo -= diffSaldo;
                            }
                            _unitOfWork.Customers.Update(buildingCustomer);
                        }
                    }
                    // Aktualisiere NUR die Eigenschaften von existingCustomer
                    App.FullMapper.Map(customer, existingCustomer);

                    existingCustomer.BuildingId = customer.BuildingId;

                    existingCustomer.ActivityId = customer.ActivityId;
                    existingCustomer.Activity = ActivitiesList.FirstOrDefault(b => b.Id == customer.ActivityId);
                   

                    _unitOfWork.Customers.Update(existingCustomer);
                    //_context.Entry(existingCustomer).CurrentValues.SetValues(App.FullMapper.Map<Customer>(customer));
                    await _unitOfWork.SaveChangesAsync();
                    var partner = _context.PartneriTest.FirstOrDefault(x => x.PartnerID == existingCustomer.Id);
                    if (partner != null)
                    {
                        partner.Partner = existingCustomer.CustomerInfo;
                        partner.parAdresa = existingCustomer.Adress;
                        _context.PartneriTest.Update(partner);
                        _context.SaveChanges();
                    }
                    if (navigateToCreate)
                    {
                        return RedirectToAction(nameof(CreateWithModel), new CustomerViewModel { BuildingId = customer.BuildingId });
                    }
                }

                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!CustomertExists(customer.Id))
                    return NotFound();
                throw;
            }
        }
        private void PopulateViewData(int? buildingId = null, int? activityId = null)
        {
            ViewData["BuildingId"] = new SelectList(BuildingsList, "Id", "Name", buildingId);
            ViewData["ActivityId"] = new SelectList(ActivitiesList, "Id", "Name", activityId);
        }
        // GET: customers/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var customer = await _unitOfWork.Customers.GetByIdAsync(x => x.Id == id);
            if (customer == null)
                return NotFound();

            try
            {
                _unitOfWork.Customers.Delete(customer);
                await _unitOfWork.SaveChangesAsync();
                var partnerToDelete = _context.PartneriTest.FirstOrDefault(x => x.PartnerID == id);
                if (partnerToDelete != null)
                {
                    _context.PartneriTest.Remove(partnerToDelete);
                    _context.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Löschen fehlgeschlagen: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        private bool CustomertExists(int id)
        {
            var customer = _unitOfWork.Customers.GetByIdAsync(e => e.Id == id).Result;
            return customer != null;
        }
    }
}