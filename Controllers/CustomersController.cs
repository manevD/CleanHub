using CleanHub.Attribute;
using CleanHub.Config;
using CleanHub.Entities;
using CleanHub.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Net;
using System.Net.Mail;

namespace CleanHub.Controllers
{
    [RequireLogin]

    public class CustomersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private SMTPConfig _smtpConfig;
        private static DateOnly DateFrom = DateOnly.FromDateTime(DateTime.Now);
        private static DateOnly DateTo = DateOnly.FromDateTime(DateTime.Now);

        private readonly IMemoryCache _cache;
        private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(20); // Adjust expiration time as needed

        public CustomersController(ApplicationDbContext context, IMemoryCache cache, IOptions<SMTPConfig> config)
        {
            _context = context;
            _cache = cache;
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
            List<Customer> customers;
            if (!string.IsNullOrEmpty(customersJson))
            {
                customers = JsonConvert.DeserializeObject<List<Customer>>(customersJson, settings);
            }
            else
            {
                customers = await _context.Customers.Include(r => r.Building).Include(i => i.BookFinancials).AsNoTracking().ToListAsync();
                // Load Customers for each invoice separately

                HttpContext.Session.SetString("Customers", JsonConvert.SerializeObject(customers, settings));
            }
            return View(customers);
        }

        // GET: Customers/Details/5
        public async Task<IActionResult> Details(int? id, string? dateFrom, string? dateTo)
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
                customers = JsonConvert.DeserializeObject<List<Customer>>(customersJson, settings);
                customer = customers
             .FirstOrDefault(x => x.Id == id);
                customer.Documents = customer.Documents.Where(inv => inv.DueDate >= DateFrom && inv.DueDate <= DateTo).ToList();

                // Iterate through each invoice of the customer
                foreach (var invoice in customer.Documents)
                {
                    // Set the customer property of the invoice to the current customer
                    invoice.Customer = customer;
                }

            }
            else if (!string.IsNullOrEmpty(dateFrom))
            {
                DateFrom = DateOnly.ParseExact(dateFrom, "dd.MM.yyyy", null);
                ViewBag.DateFrom = DateFrom.ToString("dd.MM.yyyy");
                customers = JsonConvert.DeserializeObject<List<Customer>>(customersJson, settings);
                customer = customers
             .FirstOrDefault(customer => customer.Id == id && customer.Documents.Any(inv => inv.DueDate >= DateFrom));
                customer.Documents = customer.Documents.Where(inv => inv.DueDate >= DateFrom).ToList();
                foreach (var document in customer.Documents)
                {
                    // Set the customer property of the invoice to the current customer
                    document.Customer = customer;
                }
            }
            else
            {
                customers = JsonConvert.DeserializeObject<List<Customer>>(customersJson, settings);
                customer = customers.FirstOrDefault(x => x.Id == id);
                foreach (var document in customer.Documents)
                {
                    // Set the customer property of the invoice to the current customer
                    document.Customer = customer;
                }
            }

            if (customer == null)
            {
                return NotFound();
            }

            return View(customer);
        }

        // GET: customers/Create
        public IActionResult Create()
        {
            var customer = new Customer
            {
                BuildingList = new SelectList(_context.Buildings.ToList(), nameof(Building.Id), nameof(Building.Name))
            }; return View(customer);
        }

        // POST: customer/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        public async Task<IActionResult> Create(Customer customer)
        {
            if (ModelState.IsValid)
            {
                _context.Add(customer);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            _cache.Remove("CustomersList");
            ViewData["BuildingId"] = new SelectList(_context.Buildings, "Id", "Id", customer.BuildingId);
            return View(customer);
        }

        // GET: customer/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }
            _cache.TryGetValue("CustomersList", out List<Customer> customers);

            var customer = customers.FirstOrDefault(x => x.Id == id);
            if (customer == null)
            {
                return NotFound();
            }
            ViewData["BuildingId"] = new SelectList(_context.Buildings, "Id", "Id", customer.BuildingId);
            return View(customer);
        }

        // POST: customers/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id,  Customer customer)
        {
            if (id != customer.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(customer);
                    await _context.SaveChangesAsync();
                    _cache.Remove("CustomersList");
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
            ViewData["BuildingId"] = new SelectList(_context.Buildings, "Id", "Id", customer.BuildingId);
            return View(customer);
        }

        // GET: customers/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }
            _cache.TryGetValue("CustomersList", out List<Customer> customers);

            var customer = customers.FirstOrDefault(m => m.Id == id);
            if (customer == null)
            {
                return NotFound();
            }

            return View(customer);
        }

        public async Task<IActionResult> ExportInvoices(DateTime datum, int customerId)
        {
            using (SmtpClient smtpClient = new SmtpClient(_smtpConfig.Server))
            {
                smtpClient.Credentials = new NetworkCredential(_smtpConfig.Email, _smtpConfig.Passwort);
                smtpClient.EnableSsl = true;

                MailMessage mailMessage = new MailMessage
                {
                    Subject = string.Concat("Фактура Марти Хигиена за", datum.Day, datum.Month, datum.Year, " за ден"),
                    //Body = sb.ToString(),
                    IsBodyHtml = true
                };
                mailMessage.From = new MailAddress(_smtpConfig.Email);
                mailMessage.To.Add(_smtpConfig.Recipient);

                // Send the email
                smtpClient.Send(mailMessage);
            }
            var customer = _context.Customers.FirstOrDefault(x => x.Id == customerId);
            return RedirectToAction("Details", customer);
        }

        // POST: customers/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (!_cache.TryGetValue("CustomersList", out List<Customer> customers))
            {
                // Data not in cache, retrieve from database
                customers = await _context.Customers.Include(r => r.).Include(i => i.Documents).ToListAsync();

                // Cache the data
                _cache.Set("CustomersList", customers, _cacheExpiration);
            }
            var customer = customers.FirstOrDefault(x => x.Id == id);
            if (customer != null)
            {
                _context.Customers.Remove(customer);
            }

            await _context.SaveChangesAsync();
            _cache.Remove("CustomersList");
            return RedirectToAction(nameof(Index));
        }

        private bool CustomertExists(int id)
        {
            return _context.Customers.Any(e => e.Id == id);
        }
    }
}
