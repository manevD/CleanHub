using CleanHub.Infrastructure.Data;
using CleanHub.Entities;
using CleanHub.Entities.Enums;
using CleanHub.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CleanHub.Controllers
{
    public class InvoicesController(ApplicationDbContext context) : Controller
    {
        private static DateOnly DateFrom = DateOnly.FromDateTime(DateTime.Now);
        private static DateOnly DateTo = DateOnly.FromDateTime(DateTime.Now);
        public List<Building> Buildings { get; set; } = context.Buildings.ToList();
        public async Task<IActionResult> InvoiceFiltered(int? invoiceId, int? buildingId, string dateFrom,
            string dateTo)
        {
            ViewBag.InvoiceId = invoiceId ?? (int)InvoiceTyp.Reserve;

            Buildings.Insert(0, new Building
            {
                Name = "Сите",
                Id = 0
            });
            if (!Buildings.Any())
            {
                throw new Exception("No buildings found in the database.");
            }

            ViewBag.Buildings = new SelectList(Buildings, "Id", "Name", buildingId);
            List<Document> documentEntity;
            if (!string.IsNullOrEmpty(dateFrom) && !string.IsNullOrEmpty(dateTo))
            {
                DateFrom = DateOnly.ParseExact(dateFrom, "dd.MM.yyyy", null);
                DateTo = DateOnly.ParseExact(dateTo, "dd.MM.yyyy", null);
                ViewBag.DateFrom = DateFrom.ToString("dd.MM.yyyy");
                ViewBag.DateTo = DateTo.ToString("dd.MM.yyyy");
                documentEntity = await context.Buildings
                    .Where(b => buildingId != null && b.Id == buildingId.Value)
                    .SelectMany(b => b.Customers)
                    .SelectMany(c => c.Documents)
                    .Select(c => new Entities.Document
                    {
                        Id = c.Id,
                        Number = c.Number,
                        ToDocument = c.ToDocument,
                        Date = c.Date,
                        Description = c.Description,
                        DateReceived = c.DateReceived,
                        PaymentStatus = c.PaymentStatus,
                        TotalOutput = c.TotalOutput,
                        Customer = new Customer
                        {
                            CustomerInfo = c.Customer.CustomerInfo,
                        } 
                    }).Where(x => x.Date >= DateFrom && x.Date <= DateTo)
                    .ToListAsync();
            }
            else if (!string.IsNullOrEmpty(dateFrom))
            {
                DateFrom = DateOnly.ParseExact(dateFrom, "dd.MM.yyyy", null);
                ViewBag.DateFrom = DateFrom.ToString("dd.MM.yyyy");
                documentEntity = await context.Buildings
                    .Where(b => buildingId != null && b.Id == buildingId.Value)
                    .SelectMany(b => b.Customers)
                    .SelectMany(c => c.Documents)
                    .Select(c => new Entities.Document
                    {
                        Id = c.Id,
                        Number = c.Number,
                        ToDocument = c.ToDocument,
                        Date = c.Date,
                        Description = c.Description,
                        DateReceived = c.DateReceived,
                        PaymentStatus = c.PaymentStatus,
                        TotalOutput = c.TotalOutput,
                        Customer = new Entities.Customer
                        {
                            CustomerInfo = c.Customer.CustomerInfo,
                        }
                    }).Where(x => x.Date >= DateFrom)
                    .ToListAsync();
            }
            else
            {
                documentEntity = await context.Buildings
                    .Where(b => buildingId != null && b.Id == buildingId.Value)
                    .SelectMany(b => b.Customers)
                    .SelectMany(c => c.Documents) // Entferne die `?? new List<Document>()`
                    .Select(c => new Document
                    {
                        Id = c.Id,
                        Number = c.Number,
                        ToDocument = c.ToDocument,
                        Date = c.Date,
                        Description = c.Description,
                        DateReceived = c.DateReceived,
                        PaymentStatus = c.PaymentStatus,
                        TotalOutput = c.TotalOutput,
                        Customer = new Customer
                        {
                            CustomerInfo = c.Customer.CustomerInfo,
                        }
                    })
                    .ToListAsync();

            }

            var documents = App.FullMapper.Map<List<DocumentViewModel>>(documentEntity);
            var today = DateOnly.FromDateTime(DateTime.Now);

            foreach (var doc in documents.Where(x =>
                         x.PaymentStatus == PaymentStatus.Неплатено || x.PaymentStatus == PaymentStatus.Задоцнето))
            {
                // Null-Check für DueDate
                if (!doc.DateReceived.HasValue)
                {
                    continue; // Überspringe dieses Dokument, wenn kein Fälligkeitsdatum vorhanden ist
                }

                int overdueDays = today.DayNumber - doc.DateReceived.Value.DayNumber; // Berechne überfällige Tage
                doc.Delay = overdueDays;

                // Zahlungsstatus aktualisieren
                doc.PaymentStatus = overdueDays > 30 ? PaymentStatus.Задоцнето : PaymentStatus.Неплатено;

                // Prozentsatz basierend auf überfälligen Tagen bestimmen
                double percentage = overdueDays switch
                {
                    < 0 => 0, // Noch nicht fällig
                    < 30 => 0.02, // 2%
                    >= 30 and <= 60 => 0.04, // 4%
                    >= 61 and <= 90 => 0.06, // 6%
                    >= 91 and <= 180 => 0.08, // 8%
                    >= 181 and <= 360 => 0.10, // 10%
                    >= 361 and <= 730 => 0.13, // 13%
                    _ => 0.16 // 16% für 730+ Tage
                };

                // Null-Check für TotalOutput
                double totalOutput = doc.TotalOutput ?? 0;
                doc.NewTotal = (int)Math.Round(totalOutput * (1 + percentage), MidpointRounding.AwayFromZero);
            }

            return View("Index", documents);
        }

        public IActionResult Index()
        {
            if (!Buildings.Any())
            {
                throw new Exception("No buildings found in the database.");
            }
            Buildings.Insert(0, new Building()
            {
                Name = "Сите",
                Id = 0
            });
            ViewBag.Buildings = new SelectList(Buildings, "Id", "Name");

            return View("SpecialIndex");
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var invoiceEntity = await context.SpecialInvoices.Include(x => x.Building)
                .FirstOrDefaultAsync(xd => xd.Id == id);
            var speicalInvoiceViewModel = App.FullMapper.Map<SpecialInvoiceViewModel>(invoiceEntity);
            if (invoiceEntity == null)
            {
                return NotFound();
            }

            return View(speicalInvoiceViewModel);
        }

        // POST: ProductsController/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, SpecialInvoiceViewModel specialInvoice)
        {
            if (id != specialInvoice.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var specialInvoiceEntity = App.FullMapper.Map<SpecialInvoice>(specialInvoice);
                    context.SpecialInvoices.Update(specialInvoiceEntity);
                    await context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!SpecialInvoceExist(id))
                    {
                        return NotFound();
                    }

                    throw;
                }

                return RedirectToAction(nameof(Index));
            }
            return View(specialInvoice);
        }

        private bool SpecialInvoceExist(int id)
        {
            return context.SpecialInvoices.Any(x => x.Id == id);
        }

        // POST: InvoicesController/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(int id, IFormCollection collection)
        {
            try
            {
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return View();
            }
        }

        public async Task<IActionResult> Filtered(int? buildingId, string dateFrom, string dateTo)
        {
            List<SpecialInvoiceViewModel> specialInvoiceViewModel = new List<SpecialInvoiceViewModel>();
            specialInvoiceViewModel = await GetSpecialInvoice(dateFrom, dateTo, buildingId.Value);
            if (!Buildings.Any())
            {
                throw new Exception("No buildings found in the database.");
            }
            Buildings.Insert(0, new Building()
            {
                Name = "Сите",
                Id = 0
            });

            ViewBag.Buildings = new SelectList(Buildings, "Id", "Name", buildingId);

            return View("SpecialIndex", model: specialInvoiceViewModel);
        }

        public async Task<IActionResult> DetailsFiltered(int? buildingId,
            string dateFrom, string dateTo)
        {
            List<SpecialInvoiceViewModel> specialInvoiceViewModel = await GetSpecialInvoice(dateFrom, dateTo, buildingId ?? 0);

            if (!Buildings.Any())
            {
                throw new Exception("No buildings found in the database.");
            }
            Buildings.Insert(0, new Building()
            {
                Name = "Сите",
                Id = 0
            });
            ViewBag.Buildings = new SelectList(Buildings, "Id", "Name", buildingId);

            specialInvoiceViewModel.ForEach(x => x.InvoiceId = (int)InvoiceTyp.Energy);
            return View("SpecialIndex", model: specialInvoiceViewModel);
        }

        private async Task<List<SpecialInvoiceViewModel>> GetSpecialInvoice(string dateFrom, string dateTo,
             int buildingId)
        {
            var specialInvoiceViewModel = new List<SpecialInvoiceViewModel>();

            if (!string.IsNullOrEmpty(dateFrom) && !string.IsNullOrEmpty(dateTo))
            {
                DateFrom = DateOnly.ParseExact(dateFrom, "dd.MM.yyyy", null);
                DateTo = DateOnly.ParseExact(dateTo, "dd.MM.yyyy", null);
                ViewBag.DateFrom = DateFrom.ToString("dd.MM.yyyy");
                ViewBag.DateTo = DateTo.ToString("dd.MM.yyyy");

                specialInvoiceViewModel = specialInvoiceViewModel.Where(inv =>
                    inv.ForDate >= DateFrom && inv.ForDate <= DateTo && inv.BuildingId == buildingId &&
                    inv.InvoiceId == (int)InvoiceTyp.Energy).ToList();
            }
            else if (!string.IsNullOrEmpty(dateFrom))
            {
                DateFrom = DateOnly.ParseExact(dateFrom, "dd.MM.yyyy", null);
                ViewBag.DateFrom = DateFrom.ToString("dd.MM.yyyy");
                specialInvoiceViewModel = App.FullMapper.Map<List<SpecialInvoiceViewModel>>(await context
                    .SpecialInvoices.Include(x => x.Building)
                    .Where(inv => inv.ForDate >= DateFrom && inv.InvoiceId == (int)InvoiceTyp.Energy && inv.BuildingId == buildingId)
                    .ToListAsync());
            }
            else
            {
                specialInvoiceViewModel = App.FullMapper.Map<List<SpecialInvoiceViewModel>>(context
                    .SpecialInvoices.Include(x => x.Building)
                    .Where(x => x.InvoiceId == (int)InvoiceTyp.Energy && x.BuildingId == buildingId).ToList());
            }
            if (buildingId == 0)
            {
                specialInvoiceViewModel = App.FullMapper.Map<List<SpecialInvoiceViewModel>>(await context.SpecialInvoices.Include(x => x.Building).Where(x => x.InvoiceId == (int)InvoiceTyp.Energy).ToListAsync());
            }
            return specialInvoiceViewModel;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetStatusPayment(int? id, string dateFrom, string dateTo, int? buildingId)
        {
            if (!id.HasValue || id == 0)
            {
                return NotFound();
            }

            var invoiceToUpdate = await context.SpecialInvoices.FirstOrDefaultAsync(x => x.Id == id);
            try
            {
                if (invoiceToUpdate == null)
                {
                    return NotFound();
                }

                invoiceToUpdate.Status = PaymentStatus.Платено;
                context.Update(invoiceToUpdate);
                await context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!context.SpecialInvoices.Any(x => x.Id == id))
                {
                    return NotFound();
                }

                // Log error or handle gracefully
                throw;
            }

            return RedirectToAction(nameof(DetailsFiltered), new { buildingId, dateFrom, dateTo });
        }
        // GET: InvoicesController/Delete/5
        public ActionResult Delete(int id)
        {
            return View();
        }

        // POST: InvoicesController/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id, IFormCollection collection)
        {
            try
            {
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return View();
            }
        }
    }
}
