using CleanHub.Attribute;
using CleanHub.Config;
using CleanHub.Entities;
using CleanHub.Infrastructure.Data;
using CleanHub.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace CleanHub.Controllers
{
    [RequireLogin]

    public class CustomersController(IUnitOfWork _unitOfWork, IOptions<SMTPConfig> _smtpConfig) : Controller
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
               return App.ReaderMapper.Map <List<CustomerViewModel>>(_unitOfWork.Customers.GetAllNoTrakcing().Select(c => new Customer
               {
                   Id = c.Id,
                   CustomerInfo = c.CustomerInfo ?? string.Empty,
                   Email = c.Email,
                   Subscription = c.Subscription ?? 0,
                   PhoneNumber = c.PhoneNumber,
                   Inactive = c.Inactive,
                   Adress = c.Adress
               }));
            });
           
            return View(customers);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var customer = await _unitOfWork.Customers.GetByIdAsync(cus => cus.Id == id,x=>x.Include(c => c.Activity).Include(c=>c.Documents));
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
                return RedirectToAction(nameof(Index));
            }
            PopulateViewData(customer.BuildingId, customer.ActivityId);

            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();

            ViewBag.Errors = errors;
            ViewBag.ShowErrorModal = true;

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
            var customerEntity = await _unitOfWork.Customers.GetByIdAsync(c => c.Id == id,cu=>cu.Include(cust => cust.Activity).Include(cus => cus.Documents).Include(x => x.Activity).Include(d => d.Documents));
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

            if (!ModelState.IsValid)
            {
                PopulateViewData(customer.BuildingId, customer.ActivityId);
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                ViewBag.Errors = errors;
                ViewBag.ShowErrorModal = true;
                return View(customer);
            }

            try
            {
                var existingCustomer = await _unitOfWork.Customers.GetByIdAsync(x => x.Id == id);
                if (existingCustomer == null)
                    return NotFound();

                // Prüfe Inaktivitätslogik
                var navigateToCreate = (!existingCustomer.Inactive.HasValue && customer.Inactive == true)
                                       || (existingCustomer.Inactive == false && customer.Inactive == true);

                // Aktualisiere NUR die Eigenschaften von existingCustomer
                App.FullMapper.Map(customer, existingCustomer);
                if (customer.BuildingId != null)
                {
                    existingCustomer.BuildingId = customer.BuildingId;
                    existingCustomer.Building = BuildingsList.FirstOrDefault(b => b.Id == customer.BuildingId);
                }
                if (customer.ActivityId != null)
                {
                    existingCustomer.ActivityId = customer.ActivityId;
                    existingCustomer.Activity = ActivitiesList.FirstOrDefault(b => b.Id == customer.ActivityId);
                }
                _unitOfWork.Customers.Update(existingCustomer);
                //_context.Entry(existingCustomer).CurrentValues.SetValues(App.FullMapper.Map<Customer>(customer));
                await _unitOfWork.SaveChangesAsync();

                if (navigateToCreate)
                {
                    return RedirectToAction(nameof(CreateWithModel), new CustomerViewModel { BuildingId = customer.BuildingId });
                }
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!CustomertExists(customer.Id))
                    return NotFound();
                throw;
            }

            return RedirectToAction(nameof(Index));
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

            var customer = await _unitOfWork.Customers.GetByIdAsync(x=>x.Id == id);
            if (customer == null)
                return NotFound();

            try
            {
                _unitOfWork.Customers.Delete(customer);
                await _unitOfWork.SaveChangesAsync();
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