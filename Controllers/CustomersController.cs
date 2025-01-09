using CleanHub.Attribute;
using CleanHub.CleanHub.Infrastructure.Data;
using CleanHub.Config;
using CleanHub.Entities;
using CleanHub.Helpers;
using CleanHub.Services;
using CleanHub.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace CleanHub.Controllers
{
    [RequireLogin]

    public class CustomersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private SMTPConfig _smtpConfig;
        private static DateOnly DateFrom = DateOnly.FromDateTime(DateTime.Now);
        private static DateOnly DateTo = DateOnly.FromDateTime(DateTime.Now);

        private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(20); // Adjust expiration time as needed

        public CustomersController(ApplicationDbContext context, IOptions<SMTPConfig> config)
        {
            _context = context;
            _smtpConfig = config.Value;
        }

        // GET: Customers
        [Route("Станари")]
        public async Task<IActionResult> Index([FromServices] IMemoryCache cache)
        {
            var customers = await cache.GetOrCreateAsync("Customers", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
               return App.ReaderMapper.Map <List<CustomerViewModel>>(await _context.Customers.AsNoTracking().Select(c => new Customer
               {
                   Id = c.Id,
                   CustomerInfo = c.CustomerInfo ?? string.Empty,
                   Email = c.Email,
                   Subscription = c.Subscription ?? 0,
                   PhoneNumber = c.PhoneNumber,
                   Inactive = c.Inactive,
                   Adress = c.Adress
               }).ToListAsync());
            });
           
            return View(customers);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var customer = await _context.Customers.Include(x => x.Activity).Include(d => d.Documents).FirstOrDefaultAsync(c => c.Id == id);

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

            var customer = await _context.Customers
                .Include(c => c.Documents)
                .FirstOrDefaultAsync(c => c.Id == id);

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

            ViewData["BuildingId"] = new SelectList(_context.Buildings, "Id", "Name", buildingId);
            var customer = new CustomerViewModel
            {
                BuildingId = buildingId
            };
            return View(nameof(Create), customer);
        }

        public IActionResult CreateWithModel(CustomerViewModel customer)
        {
            ViewData["BuildingId"] = new SelectList(_context.Buildings, "Id", "Name", customer.BuildingId);
            ViewData["ActivityId"] = new SelectList(_context.Activity, "Id", "Name", customer.ActivityId);

            return View(nameof(Create), customer);
        }

        // GET: customers/Create
        public IActionResult Create(int? buildingId, CustomerViewModel? customer)
        {
            customer ??= new CustomerViewModel();
            if (buildingId.HasValue)
                customer.BuildingId = buildingId.Value;

            ViewData["BuildingId"] = new SelectList(_context.Buildings, "Id", "Name", customer.BuildingId);
            ViewData["ActivityId"] = new SelectList(_context.Activity, "Id", "Name", customer.ActivityId);

            return View(customer);
        }
        // POST: customer/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        public async Task<IActionResult> Create(CustomerViewModel customer)
        {
            if (ModelState.IsValid)
            {
                var customerEntity = App.FullMapper.Map<Customer>(customer);
                customerEntity.Inactive = false;
                _context.Add(customerEntity);
                await _context.SaveChangesAsync();
                HttpContext.Session.Remove("Customers");
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
            var customerEntity = await _context.Customers.Include(x => x.Activity).Include(d => d.Documents).FirstOrDefaultAsync(c => c.Id == id);
            if (customerEntity?.Documents != null && customerEntity.Documents.Any())
            {
                foreach (var doc in customerEntity.Documents)
                {
                    if (doc.ToDocument != null)
                    {
                        var year = DocumentService.ExtractYear(doc.ToDocument);
                        var month = DocumentService.ExtractMonth(doc.ToDocument);
                        var searchCriteria = string.Concat(month, "/", year);

                        var bookFinancial = await _context.BookFinancials.FirstOrDefaultAsync(x => x.Description != null && x.Description.Contains(searchCriteria) && x.InvoiceId == Constants.Recieve);
                        doc.PaymentStatus = DocumentService.GetStatus(bookFinancial, doc);
                    }
                }
            }
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
                ViewData["BuildingId"] = new SelectList(_context.Buildings, "Id", "Name", customer.BuildingId);
                ViewData["ActivityId"] = new SelectList(_context.Activity, "Id", "Name", customer.ActivityId);
                return View(customer);
            }

            try
            {
                var existingCustomer = await _context.Customers.FindAsync(id);
                if (existingCustomer == null)
                    return NotFound();

                // Prüfe Inaktivitätslogik
                var navigateToCreate = !existingCustomer.Inactive.HasValue && customer.Inactive == true
                                       || existingCustomer.Inactive == false && customer.Inactive == true;

                // Aktualisiere Felder
                _context.Entry(existingCustomer).CurrentValues.SetValues(App.FullMapper.Map<Customer>(customer));
                await _context.SaveChangesAsync();
                HttpContext.Session.Remove("Customers");

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
            ViewData["BuildingId"] = new SelectList(_context.Buildings, "Id", "Name", buildingId);
            ViewData["ActivityId"] = new SelectList(_context.Activity, "Id", "Name", activityId);
        }
        // GET: customers/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var customer = await _context.Customers.FindAsync(id);
            if (customer == null)
                return NotFound();

            try
            {
                _context.Customers.Remove(customer);
                await _context.SaveChangesAsync();
                HttpContext.Session.Remove("Customers");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Löschen fehlgeschlagen: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }


        private bool CustomertExists(int id)
        {
            return _context.Customers.Any(e => e.Id == id);
        }
    }
}
