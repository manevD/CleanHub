using CleanHub.Attribute;
using CleanHub.Config;
using CleanHub.Data;
using CleanHub.Models;
using Microsoft.AspNetCore.Http;
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

    public class ResidentsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private SMTPConfig _smtpConfig;
        private static DateTime DateFrom = DateTime.Now;
        private static DateTime DateTo = DateTime.Now;

        private readonly IMemoryCache _cache;
        private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(20); // Adjust expiration time as needed

        public ResidentsController(ApplicationDbContext context, IMemoryCache cache, IOptions<SMTPConfig> config)
        {
            _context = context;
            _cache = cache;
            _smtpConfig = config.Value;
        }

        // GET: Residents
        public async Task<IActionResult> Index()
        {
            string residentsJson = HttpContext.Session.GetString("Residents");
            var settings = new JsonSerializerSettings
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            };
            List<Resident> residents;
            if (!string.IsNullOrEmpty(residentsJson))
            {
                residents = JsonConvert.DeserializeObject<List<Resident>>(residentsJson, settings);
            }
            else
            {
                residents = await _context.Residents.Include(r => r.Building).Include(i => i.Invoices).AsNoTracking().ToListAsync();
                // Load residents for each invoice separately

                HttpContext.Session.SetString("Residents", JsonConvert.SerializeObject(residents, settings));
            }
            return View(residents);
        }

        // GET: Residents/Details/5
        public async Task<IActionResult> Details(int? id, string? dateFrom, string? dateTo)
        {
            if (id == null)
            {
                return NotFound();
            }

            string residentsJson = HttpContext.Session.GetString("Residents");
            var settings = new JsonSerializerSettings
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            };

            List<Resident> residents;
            var resident = new Resident();
            if (!string.IsNullOrEmpty(dateFrom) && !string.IsNullOrEmpty(dateTo))
            {
                DateFrom = DateTime.ParseExact(dateFrom, "dd.MM.yyyy", null);
                DateTo = DateTime.ParseExact(dateTo, "dd.MM.yyyy", null);
                ViewBag.DateFrom = DateFrom.ToString("dd.MM.yyyy");
                ViewBag.DateTo = DateTo.ToString("dd.MM.yyyy");
                residents = JsonConvert.DeserializeObject<List<Resident>>(residentsJson, settings);
                resident = residents
             .FirstOrDefault(x => x.Id == id);
                resident.Invoices = resident.Invoices.Where(inv => inv.DueDate >= DateFrom && inv.DueDate <= DateTo).ToList();

                // Iterate through each invoice of the resident
                foreach (var invoice in resident.Invoices)
                {
                    // Set the Resident property of the invoice to the current resident
                    invoice.Resident = resident;
                }

            }
            else if (!string.IsNullOrEmpty(dateFrom))
            {
                DateFrom = DateTime.ParseExact(dateFrom, "dd.MM.yyyy", null);
                ViewBag.DateFrom = DateFrom.ToString("dd.MM.yyyy");
                residents = JsonConvert.DeserializeObject<List<Resident>>(residentsJson, settings);
                resident = residents
             .FirstOrDefault(resident => resident.Id == id && resident.Invoices.Any(inv => inv.DueDate >= DateFrom));
                resident.Invoices = resident.Invoices.Where(inv => inv.DueDate >= DateFrom).ToList();
                foreach (var invoice in resident.Invoices)
                {
                    // Set the Resident property of the invoice to the current resident
                    invoice.Resident = resident;
                }
            }
            else
            {
                residents = JsonConvert.DeserializeObject<List<Resident>>(residentsJson, settings);
                resident = residents.FirstOrDefault(x => x.Id == id);
                foreach (var invoice in resident.Invoices)
                {
                    // Set the Resident property of the invoice to the current resident
                    invoice.Resident = resident;
                }
            }


            if (resident == null)
            {
                return NotFound();
            }

            return View(resident);
        }

        // GET: Residents/Create
        public IActionResult Create()
        {
            ViewData["BuildingId"] = new SelectList(_context.Buildings, "Id", "Id");
            return View();
        }

        // POST: Residents/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,FirstName,LastName,Email,PhoneNumber,BuildingId")] Resident resident)
        {
            if (ModelState.IsValid)
            {
                _context.Add(resident);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            _cache.Remove("ResidentsList");
            ViewData["BuildingId"] = new SelectList(_context.Buildings, "Id", "Id", resident.BuildingId);
            return View(resident);
        }

        // GET: Residents/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }
            _cache.TryGetValue("ResidentsList", out List<Resident> residents);

            var resident = residents.FirstOrDefault(x => x.Id == id);
            if (resident == null)
            {
                return NotFound();
            }
            ViewData["BuildingId"] = new SelectList(_context.Buildings, "Id", "Id", resident.BuildingId);
            return View(resident);
        }

        // POST: Residents/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,FirstName,LastName,Email,PhoneNumber,BuildingId")] Resident resident)
        {
            if (id != resident.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(resident);
                    await _context.SaveChangesAsync();
                    _cache.Remove("ResidentsList");
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ResidentExists(resident.Id))
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
            ViewData["BuildingId"] = new SelectList(_context.Buildings, "Id", "Id", resident.BuildingId);
            return View(resident);
        }

        // GET: Residents/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }
            _cache.TryGetValue("ResidentsList", out List<Resident> residents);

            var resident = residents.FirstOrDefault(m => m.Id == id);
            if (resident == null)
            {
                return NotFound();
            }

            return View(resident);
        }

        public async Task<IActionResult> ExportInvoices(DateTime datum, int residentId)
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
            var resident = _context.Residents.FirstOrDefault(x => x.Id == residentId);
            return RedirectToAction("Details", resident);
        }

        // POST: Residents/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (!_cache.TryGetValue("ResidentsList", out List<Resident> residents))
            {
                // Data not in cache, retrieve from database
                residents = await _context.Residents.Include(r => r.Building).Include(i => i.Invoices).ToListAsync();

                // Cache the data
                _cache.Set("ResidentsList", residents, _cacheExpiration);
            }
            var resident = residents.FirstOrDefault(x => x.Id == id);
            if (resident != null)
            {
                _context.Residents.Remove(resident);
            }

            await _context.SaveChangesAsync();
            _cache.Remove("ResidentsList");
            return RedirectToAction(nameof(Index));
        }

        private bool ResidentExists(int id)
        {
            return _context.Residents.Any(e => e.Id == id);
        }
    }
}
