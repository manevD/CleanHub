using CleanHub.Attribute;
using CleanHub.Config;
using CleanHub.Entities;
using CleanHub.Infrastructure.Data;
using CleanHub.Services;
using CleanHub.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
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
        public async Task<IActionResult> Index()
        {
            string customersJson = HttpContext.Session.GetString("Customers");
            var settings = new JsonSerializerSettings
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            };
            List<CustomerViewModel> customers;
            if (!string.IsNullOrEmpty(customersJson))
            {
                customers = JsonConvert.DeserializeObject<List<CustomerViewModel>>(customersJson, settings);
            }
            else
            {
                var customersEntity = await _context.Customers.AsNoTracking().Select(c => new Customer
                {
                    Id = c.Id,
                    CustomerInfo = c.CustomerInfo ?? string.Empty, // Handle null
                    Email = c.Email,
                    PhoneNumber = c.PhoneNumber,
                    Inactive = c.Inactive,
                    Adress =c.Adress
                }).ToListAsync();
                customers = App.ReaderMapper.Map<List<CustomerViewModel>>(customersEntity);

                HttpContext.Session.SetString("Customers", JsonConvert.SerializeObject(customers, settings));
            }
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
            {
                return NotFound();
            }

            string customersJson = HttpContext.Session.GetString("Customers");
            var settings = new JsonSerializerSettings
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            };

            List<Customer> customers;
            var customer = new Customer();
            if (!string.IsNullOrEmpty(dateFrom) && !string.IsNullOrEmpty(dateTo))
            {
                DateFrom = DateOnly.ParseExact(dateFrom, "dd.MM.yyyy", null);
                DateTo = DateOnly.ParseExact(dateTo, "dd.MM.yyyy", null);
                ViewBag.DateFrom = DateFrom.ToString("dd.MM.yyyy");
                ViewBag.DateTo = DateTo.ToString("dd.MM.yyyy");
                customers = await _context.Customers.Include(x=>x.Documents).ToListAsync();
                customer =   customers
             .FirstOrDefault(x => x.Id == id);
                customer.Documents =  customer.Documents?.Where(inv => inv.Date >= DateFrom && inv.Date <= DateTo).ToList();
            }
            else if (!string.IsNullOrEmpty(dateFrom))
            {
                DateFrom = DateOnly.ParseExact(dateFrom, "dd.MM.yyyy", null);
                ViewBag.DateFrom = DateFrom.ToString("dd.MM.yyyy");
                customers = await _context.Customers.Include(x => x.Documents).ToListAsync();
                customer = customers
             .FirstOrDefault(customer => customer.Id == id && customer.Documents.Any(inv => inv.Date >= DateFrom));
                customer.Documents = customer?.Documents?.Where(inv => inv.Date >= DateFrom).ToList();
            }
            else
            {
                customers = _context.Customers.Include(x => x.Documents).ToList();
                customer = customers.FirstOrDefault(x => x.Id == id);
            }

            if (customer == null)
            {
                return NotFound();
            }
            var viewModel = App.FullMapper.Map<CustomerViewModel>(customer);
            return View("Details", viewModel);
        }

        // GET: Customers/Create
        public IActionResult CreateWithBuilding(int buildingId)
        {
           
            ViewData["BuildingId"] = new SelectList(_context.Buildings, "Id", "Name",buildingId);
            var customer = new CustomerViewModel
            {
                BuildingId = buildingId
            };
            return View(nameof(Create),customer);
        }

        public IActionResult CreateWithModel(CustomerViewModel customer)
        {
            ViewData["BuildingId"] = new SelectList(_context.Buildings, "Id", "Name", customer.BuildingId);
            ViewData["ActivityId"] = new SelectList(_context.Activity, "Id", "Name", customer.ActivityId);

            return View(nameof(Create), customer);
        }

        // GET: customers/Create
        public IActionResult Create()
        {
            var customer = new CustomerViewModel();
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
            ViewData["BuildingId"] = new SelectList(_context.Buildings, "Id", "Name", customer.BuildingId);
            ViewData["ActivityId"] = new SelectList(_context.Activity, "Id", "Name", customer.ActivityId);

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
            foreach( var doc in customerEntity.Documents)
            {
                var year  = DocumentService.ExtractYear(doc.ToDocument);
                var month = DocumentService.ExtractMonth(doc.ToDocument);
                var searchCriteria = string.Concat( month , "/" , year);

                var bookFinancial = await _context.BookFinancials.FirstOrDefaultAsync(x=>x.Description.Contains(searchCriteria) && x.SmetkaId == 1200);
                doc.PaymentStatus = DocumentService.GetStatus(bookFinancial,doc);
            }
            var customer = App.FullMapper.Map<CustomerViewModel>(customerEntity);

            ViewData["BuildingId"] = new SelectList(_context.Buildings, "Id", "Name", customer.BuildingId);
            ViewData["ActivityId"] = new SelectList(_context.Activity, "Id", "Name", customer.ActivityId);

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
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var navigateToCreate = false;
                    var customerEntity = _context.Customers.FirstOrDefault(x => x.Id == id);
                    if ((customerEntity!.Inactive == null && customer.Inactive == true) || (customerEntity.Inactive == false && customer.Inactive == true))
                    {
                        navigateToCreate = true;
                    }
                    _context.Entry(customerEntity).State = EntityState.Detached;

                    var customerEntityToUpdate = App.FullMapper.Map<Customer>(customer);
                    _context.Update(customerEntityToUpdate);
                    await _context.SaveChangesAsync();
                    HttpContext.Session.Remove("Customers");
                    if (navigateToCreate)
                    {
                        customer.Inactive = false;
                        customer.PhoneNumber = null;
                        customer.Web = null;
                        customer.Email = null;
                        customer.PartnerOpis = null;
                        customer.InactiveDatum = null;
                        return RedirectToAction(nameof(CreateWithModel), customer);
                    }
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CustomertExists(customer.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["BuildingId"] = new SelectList(_context.Buildings, "Id", "Name", customer.BuildingId);
            ViewData["ActivityId"] = new SelectList(_context.Activity, "Id", "Name", customer.ActivityId);

            return View(customer);
        }

        // GET: customers/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var customer =  _context.Customers.FirstOrDefault(m => m.Id == id);
            if (customer == null)
            {
                return NotFound();
            }
            else
            {
                _context.Customers.Remove(customer);   
               await  _context.SaveChangesAsync();
                HttpContext.Session.Remove("Customers");
            }

            return RedirectToAction("Index");
        }

     
        private bool CustomertExists(int id)
        {
            return _context.Customers.Any(e => e.Id == id);
        }
    }
}
