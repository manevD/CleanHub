//using CleanHub.Attribute;
//using CleanHub.Config;
//using CleanHub.Infrastructure.Data;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.EntityFrameworkCore;
//using Microsoft.Extensions.Caching.Memory;
//using System.Net.Mail;
//using System.Net;
//using Microsoft.Extensions.Options;
//using System.Text;
//using CleanHub.Extensions;

//namespace CleanHub.Controllers
//{
//    [RequireLogin]
//    public class BuildingsController : Controller
//    {
//        private readonly ApplicationDbContext _context;
//        private SMTPConfig _smtpConfig;
//        private readonly IMemoryCache _cache;
//        private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(30); // Adjust expiration time as needed

//        public BuildingsController(ApplicationDbContext context, IMemoryCache cache, IOptions<SMTPConfig> smtpConfig)
//        {
//            _context = context;
//            _cache = cache;
//            _smtpConfig = smtpConfig.Value;
//        }

//        // GET: Buildings
//        public async Task<IActionResult> Index()
//        {
//            if (!_cache.TryGetValue("BuildingsList", out List<Building> buildings))
//            {
//                // Data not in cache, retrieve from database
//                buildings = await _context.Buildings.Include(x=>x.Residents).ToListAsync();
//                // Cache the data
//                _cache.Set("BuildingsList", buildings, _cacheExpiration);
//            }
//            return View(await _context.Buildings.ToListAsync());
//        }

//        // GET: Buildings/Details/5
//        public async Task<IActionResult> Details(int? id)
//        {
//            if (id == null)
//            {
//                return NotFound();
//            }
//            if (!_cache.TryGetValue("BuildingsList", out List<Building> buildings))
//            {
//                // Data not in cache, retrieve from database
//                buildings = await _context.Buildings.Include(x=>x.Residents).ToListAsync();
//                // Cache the data
//                _cache.Set("BuildingsList", buildings, _cacheExpiration);
//            }
//            var building =  buildings.FirstOrDefault(m => m.Id == id);
//            if (building == null)
//            {
//                return NotFound();
//            }

//            return View(building);
//        }

//        // GET: Buildings/Create
//        public IActionResult Create()
//        {
//            return View();
//        }

//        // POST: Buildings/Create
//        // To protect from overposting attacks, enable the specific properties you want to bind to.
//        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
//        [HttpPost]
//        [ValidateAntiForgeryToken]
//        public async Task<IActionResult> Create([Bind("Id,Name,NumberOfResidence")] Building building)
//        {
//            if (ModelState.IsValid)
//            {
//                _context.Add(building);
//                await _context.SaveChangesAsync();
//                _cache.Remove("BuildingsList");
//                return RedirectToAction(nameof(Index));
//            }
//            return View(building);
//        }

//        // GET: Buildings/Edit/5
//        public async Task<IActionResult> Edit(int? id)
//        {
//            if (id == null)
//            {
//                return NotFound();
//            }
//            _cache.TryGetValue("BuildingsList", out List<Building> buildings);
//            var building = buildings.FirstOrDefault(x=>x.Id==id);
//            if (building == null)
//            {
//                return NotFound();
//            }
//            return View(building);
//        }

//        // POST: Buildings/Edit/5
//        // To protect from overposting attacks, enable the specific properties you want to bind to.
//        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
//        [HttpPost]
//        [ValidateAntiForgeryToken]
//        public async Task<IActionResult> Edit(int id, Building building)
//        {
//            if (id != building.Id)
//            {
//                return NotFound();
//            }

//            if (ModelState.IsValid)
//            {
//                try
//                {
//                    _cache.TryGetValue("BuildingsList", out List<Building> buildings);
//                    building.NumberOfResidence = buildings.FirstOrDefault(x=>x.Id == id).Residents.Count();
//                    _context.Update(building);

//                    await _context.SaveChangesAsync();
//                    _cache.Remove("BuildingsList");
//                }
//                catch (DbUpdateConcurrencyException)
//                {
//                    if (!BuildingExists(building.Id))
//                    {
//                        return NotFound();
//                    }
//                    else
//                    {
//                        throw;
//                    }
//                }
//                return RedirectToAction(nameof(Index));
//            }
//            return View(building);
//        }

//        // GET: Buildings/Delete/5
//        public async Task<IActionResult> Delete(int? id)
//        {
//            if (id == null)
//            {
//                return NotFound();
//            }
//            _cache.TryGetValue("BuildingsList", out List<Building> buildings);
//            var building = buildings.FirstOrDefault(m => m.Id == id);
//            if (building == null)
//            {
//                return NotFound();
//            }

//            return View(building);
//        }

//        // POST: Buildings/Delete/5
//        [HttpPost, ActionName("Delete")]
//        [ValidateAntiForgeryToken]
//        public async Task<IActionResult> DeleteConfirmed(int id)
//        {
//            _cache.TryGetValue("BuildingsList", out List<Building> buildings);

//            var building = buildings.FirstOrDefault(x=>x.Id==id);
//            if (building != null)
//            {
//                _context.Buildings.Remove(building);
//            }

//            await _context.SaveChangesAsync();
//            return RedirectToAction(nameof(Index));
//        }

//        private bool BuildingExists(int id)
//        {
//            _cache.TryGetValue("BuildingsList", out List<Building> buildings);

//            return buildings.Any(e => e.Id == id);
//        }

//        [HttpPost, ActionName("SendInvoiceEmail")]
//        public IActionResult SendInvoiceEmail(int id)
//        {
//            var residents = _context.Buildings.Where(x=>x.Id == id).Include(x => x.Residents).SelectMany(d => d.Residents!).ToList();
//            StringBuilder sb = new StringBuilder();
//            foreach (var item in residents)
//            {
//                using (SmtpClient smtpClient = new SmtpClient(_smtpConfig.Server))
//                {
//                    smtpClient.Credentials = new NetworkCredential(_smtpConfig.Email, _smtpConfig.Passwort);
//                    smtpClient.EnableSsl = true;
//                    var invoices = _context.Invoices.Where(x => x.ResidentId == item.Id && (x.PaymentStatus == PaymentStatus.Неплатено || x.PaymentStatus == PaymentStatus.Задоцнето)).ToList();
//                    foreach (var invoice in invoices)
//                    {
//                        sb.Append(ControllerExtensions.RenderPartialViewToString(this,"_InvoiceDetail", invoice));
//                    }

//                    MailMessage mailMessage = new MailMessage
//                    {
//                        Subject = string.Concat("Сметка Марти Хигиена за ", item.FirstName , item.LastName , " за ден"),
//                        Body = sb.ToString(),
//                        IsBodyHtml = true
//                    };
//                    mailMessage.From = new MailAddress(_smtpConfig.Email);
//                    mailMessage.To.Add(_smtpConfig.Recipient);

//                    // Send the email
//                    smtpClient.Send(mailMessage);
//                }
//            }
//            return View(nameof(Details));
//        }
//    }
//}
