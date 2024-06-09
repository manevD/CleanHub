using CleanHub.Attribute;
using CleanHub.Config;
using CleanHub.Entities;
using CleanHub.Infrastructure.Data;
using CleanHub.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Text;

namespace CleanHub.Controllers
{
    [RequireLogin]
    public class BuildingsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private SMTPConfig _smtpConfig;

        public BuildingsController(ApplicationDbContext context, IOptions<SMTPConfig> smtpConfig)
        {
            _context = context;
            _smtpConfig = smtpConfig.Value;
        }

        // GET: Buildings
        public async Task<IActionResult> Index()
        {
            string buildingsJson = HttpContext.Session.GetString("Buildings");
            var settings = new JsonSerializerSettings
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            };
            List<BuildingViewModel> buildings;
            if (!string.IsNullOrEmpty(buildingsJson))
            {
                buildings = JsonConvert.DeserializeObject<List<BuildingViewModel>>(buildingsJson, settings);
            }
            else
            {
                var buildingssEntity = await _context.Buildings.AsNoTracking().ToListAsync();
                buildings = App.FullMapper.Map<List<BuildingViewModel>>(buildingssEntity);

                HttpContext.Session.SetString("Buildings", JsonConvert.SerializeObject(buildings, settings));
            }
            return View(buildings);
        }

        // GET: Buildings/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }
            var building = _context.Buildings.FirstOrDefault(c => c.Id == id);
            var buildingViewModel = App.FullMapper.Map<BuildingViewModel>(building);

            return View(buildingViewModel);
        }

        // GET: Buildings/Create
        public IActionResult Create()
        {
            BuildingViewModel buildingViewModel = new BuildingViewModel();
            return View(buildingViewModel);
        }

        // POST: Buildings/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(BuildingViewModel building)
        {
            if (ModelState.IsValid)
            {
                var entity = App.FullMapper.Map<Building>(building);
                _context.Add(entity);
                await _context.SaveChangesAsync();
                HttpContext.Session.Remove("Buildings");

                return RedirectToAction(nameof(Index));
            }
            return View(building);
        }

        // GET: Buildings/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }
            var buildingEntity = _context.Buildings.FirstOrDefault(c => c.Id == id);
            var building = App.FullMapper.Map<BuildingViewModel>(buildingEntity);
            HttpContext.Session.Remove("Buildings");

            return View(building);
        }

        // POST: Buildings/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, BuildingViewModel building)
        {
            if (id != building.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var buildingToUpdate = App.FullMapper.Map<Building>(building);
                    _context.Update(buildingToUpdate);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!BuildingExists(building.Id))
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
            return View(building);
        }

        // GET: Buildings/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }
            var building = _context.Buildings.FirstOrDefault(m => m.Id == id);
            if (building == null)
            {
                return NotFound();
            }

            return View(building);
        }

        // POST: Buildings/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {

            var building = _context.Buildings.FirstOrDefault(x => x.Id == id);
            if (building != null)
            {
                _context.Buildings.Remove(building);
            }
            await _context.SaveChangesAsync();
            HttpContext.Session.Remove("Buildings");

            return RedirectToAction(nameof(Index));
        }

        private bool BuildingExists(int id)
        {
            return _context.Buildings.Any(e => e.Id == id);
        }

        //[HttpPost, ActionName("SendInvoiceEmail")]
        //public IActionResult SendInvoiceEmail(int id)
        //{
        //    var residents = _context.Buildings.Where(x => x.Id == id).Include(x => x.Residents).SelectMany(d => d.Residents!).ToList();
        //    StringBuilder sb = new StringBuilder();
        //    foreach (var item in residents)
        //    {
        //        using (SmtpClient smtpClient = new SmtpClient(_smtpConfig.Server))
        //        {
        //            smtpClient.Credentials = new NetworkCredential(_smtpConfig.Email, _smtpConfig.Passwort);
        //            smtpClient.EnableSsl = true;
        //            var invoices = _context.Invoices.Where(x => x.ResidentId == item.Id && (x.PaymentStatus == PaymentStatus.Неплатено || x.PaymentStatus == PaymentStatus.Задоцнето)).ToList();
        //            foreach (var invoice in invoices)
        //            {
        //                sb.Append(ControllerExtensions.RenderPartialViewToString(this, "_InvoiceDetail", invoice));
        //            }

        //            MailMessage mailMessage = new MailMessage
        //            {
        //                Subject = string.Concat("Сметка Марти Хигиена за ", item.FirstName, item.LastName, " за ден"),
        //                Body = sb.ToString(),
        //                IsBodyHtml = true
        //            };
        //            mailMessage.From = new MailAddress(_smtpConfig.Email);
        //            mailMessage.To.Add(_smtpConfig.Recipient);

        //            // Send the email
        //            smtpClient.Send(mailMessage);
        //        }
        //    }
        //    return View(nameof(Details));
        //}
    }
}
