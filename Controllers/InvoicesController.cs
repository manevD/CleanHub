using CleanHub.CleanHub.Infrastructure.Data;
using CleanHub.Entities;
using CleanHub.Extensions;
using CleanHub.Helpers;
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

        public async Task<IActionResult> Invoice(int? id)
        {
            ViewBag.InvoiceId = id.Value;

            var buildings = await context.Buildings.ToListAsync();
            if (!buildings.Any())
            {
                throw new Exception("No buildings found in the database.");
            }
            ViewBag.Buildings = new SelectList(buildings, "Id", "Name");
      
            return View("Index");
        }

        public async Task<IActionResult> InvoiceFiltered(int? invoiceId, int? buildingId, string dateFrom, string dateTo)
        {
            ViewBag.InvoiceId = invoiceId.Value;

            var buildings = await context.Buildings.ToListAsync();
            if (!buildings.Any())
            {
                throw new Exception("No buildings found in the database.");
            }
            ViewBag.Buildings = new SelectList(buildings, "Id", "Name");
            //var documentEntity = await context.Buildings
            //        .Where(b => b.Id == buildingId.Value)
            //        .SelectMany(b => b.Customers)
            //        .SelectMany(c => c.Documents)
            //        .Select(c => new Entities.Document
            //        {
            //            Id = c.Id,
            //            Number = c.Number,
            //            ToDocument = c.ToDocument,
            //            DateReceived = c.DateReceived,
            //            PaymentStatus = c.PaymentStatus,
            //            TotalOutput = c.TotalOutput,
            //            Customer = c.Customer != null ? new Entities.Customer
            //            {
            //                CustomerInfo = c.Customer.CustomerInfo,
            //                Building = c.Customer.Building != null ? new Entities.Building
            //                {
            //                    Id = c.Customer.Building.Id,
            //                    Name = c.Customer.Building.Name,
            //                } : null // Setze Building auf null, falls es nicht existiert
            //            } : null // Setze Customer auf null, falls es nicht existiert
            //        })
            //        .ToListAsync();
            var result = await (from b in context.Buildings
                join c in context.Customers on b.Id equals c.BuildingId into customerJoin
                from c in customerJoin.DefaultIfEmpty()
                join bf in context.BookFinancials on c.Id equals bf.CustomerId into financialJoin
                from bf in financialJoin.DefaultIfEmpty()
                where bf.InvoiceId == invoiceId.Value && b.Id ==buildingId.Value
                select new BookFinancialInfoViewModel
                {
                    Id = bf.Id,
                    BuildingName = b.Name,
                    CustomerInfo = c.CustomerInfo,
                    Status = bf.Status,
                    InvoiceId = bf.InvoiceId.Value,
                    Description = bf.Description,
                    DatumF = bf.DatumF.Value,
                    Owes = bf.Owes,
                    Demands = bf.Demands
                }).ToListAsync();
            ViewBag.InvoiceId = invoiceId.Value;

            return View("Index", result);    
        }

        [Route("/Сметки/{id?}")]
        public async Task<IActionResult> Index(int? id)
        {
            ViewBag.InvoiceId = id.Value;
            if (id.Value !=  (int)InvoiceTyp.Energy)
            {
                return RedirectToAction(nameof(Invoice), new { id = id.Value });
            }
            var buildings = await context.Buildings.ToListAsync();
            if (!buildings.Any())
            {
                throw new Exception("No buildings found in the database.");
            }
            ViewBag.Buildings = new SelectList(buildings, "Id", "Name");
            ViewBag.InvoiceTypList = Enum.GetValues(typeof(InvoiceTyp))
                .Cast<InvoiceTyp>()
                .Select(e => new SelectListItem
                {
                    Text = e.GetEnumDescription(),
                    Value = ((int)e).ToString()
                })
                .ToList();         
            return View("SpecialIndex");
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }
            var invoiceEntity = await context.SpecialInvoices.Include(x=>x.Building).FirstOrDefaultAsync(xd => xd.Id == id);
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
            return context.SpecialInvoices.Any(x=>x.Id == id); 
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

        public async Task<IActionResult> Filtered(int? buildingId, int? invoiceId, string dateFrom, string dateTo)
        {
            if (!invoiceId.HasValue || !buildingId.HasValue)
            {
                return RedirectToAction(nameof(Index));
            }

            List<SpecialInvoiceViewModel> specialInvoiceViewModel = new List<SpecialInvoiceViewModel>();
            if (invoiceId == (int)InvoiceTyp.Energy)
            {
                specialInvoiceViewModel = await GetSpecialInvoice(dateFrom, dateTo, invoiceId.Value, buildingId.Value);
            }

            var buildings = await context.Buildings.ToListAsync();
            if (!buildings.Any())
            {
                throw new Exception("No buildings found in the database.");
            }
            ViewBag.Buildings = new SelectList(buildings.Select(b => new SelectListItem
            {
                Text = b.Name,
                Value = b.Id.ToString(),
                Selected = b.Id == buildingId.Value
            }).ToList(), "Value", "Text");
            ViewBag.InvoiceTypList = Enum.GetValues(typeof(InvoiceTyp))
                .Cast<InvoiceTyp>()
                .Select(e => new SelectListItem
                {
                    Text = e.GetEnumDescription(),
                    Value = ((int)e).ToString(),
                    Selected = (int)e == invoiceId.Value // `selectedInvoiceId` is the ID to pre-select
                })
                .ToList();
            specialInvoiceViewModel.ForEach(x => x.InvoiceId = invoiceId.Value);
            return View("SpecialIndex", model: specialInvoiceViewModel);
        }

        public async Task<IActionResult> DetailsFiltered(int? buildingId, int? invoiceId, string dateFrom, string dateTo)
        {
            if (!invoiceId.HasValue || !buildingId.HasValue)
            {
                return RedirectToAction(nameof(Index));
            }

            List<SpecialInvoiceViewModel> specialInvoiceViewModel = new List<SpecialInvoiceViewModel>();
            if (invoiceId == (int)InvoiceTyp.Energy)
            {
                specialInvoiceViewModel = await GetSpecialInvoice(dateFrom,dateTo,invoiceId.Value,buildingId.Value);
            }
           
            var buildings = await context.Buildings.ToListAsync();
            if (!buildings.Any())
            {
                throw new Exception("No buildings found in the database.");
            }
            ViewBag.Buildings = new SelectList(buildings.Select(b => new SelectListItem
            {
                Text = b.Name,
                Value = b.Id.ToString(),
                Selected = b.Id == buildingId.Value
            }).ToList(), "Value", "Text");
            ViewBag.InvoiceTypList = Enum.GetValues(typeof(InvoiceTyp))
                .Cast<InvoiceTyp>()
                .Select(e => new SelectListItem
                {
                    Text = e.GetEnumDescription(),
                    Value = ((int)e).ToString(),
                    Selected = (int)e == invoiceId.Value // `selectedInvoiceId` is the ID to pre-select
                })
                .ToList();
            specialInvoiceViewModel.ForEach(x => x.InvoiceId = invoiceId.Value);
            return View("SpecialIndex", model: specialInvoiceViewModel);
        }

        private async Task<List<SpecialInvoiceViewModel>> GetSpecialInvoice(string dateFrom, string dateTo,int invoiceId,int buildingId)
        {
            var specialInvoiceViewModel = new List<SpecialInvoiceViewModel>();
            if (!string.IsNullOrEmpty(dateFrom) && !string.IsNullOrEmpty(dateTo))
            {
                DateFrom = DateOnly.ParseExact(dateFrom, "dd.MM.yyyy", null);
                DateTo = DateOnly.ParseExact(dateTo, "dd.MM.yyyy", null);
                ViewBag.DateFrom = DateFrom.ToString("dd.MM.yyyy");
                ViewBag.DateTo = DateTo.ToString("dd.MM.yyyy");
                if (invoiceId == Constants.Energy)
                {
                    specialInvoiceViewModel = App.FullMapper.Map<List<SpecialInvoiceViewModel>>(await context.SpecialInvoices.Include(x => x.Building)
                        .Where(inv => inv.ForDate >= DateFrom && inv.ForDate <= DateTo && inv.BuildingId == buildingId && inv.InvoiceId == invoiceId).ToListAsync());
                }
                else
                {
                    specialInvoiceViewModel = GetInvoices(invoiceId, DateFrom, DateTo, buildingId);
                }
            }
            else if (!string.IsNullOrEmpty(dateFrom))
            {
                DateFrom = DateOnly.ParseExact(dateFrom, "dd.MM.yyyy", null);
                ViewBag.DateFrom = DateFrom.ToString("dd.MM.yyyy");
                specialInvoiceViewModel = App.FullMapper.Map<List<SpecialInvoiceViewModel>>(await context.SpecialInvoices.Include(x => x.Building)
                    .Where(inv => inv.ForDate >= DateFrom && inv.InvoiceId == invoiceId && inv.BuildingId == buildingId).ToListAsync());
            }
            else
            {
                specialInvoiceViewModel = App.FullMapper.Map<List<SpecialInvoiceViewModel>>(await context.SpecialInvoices.Include(x => x.Building).Where(x => x.InvoiceId == invoiceId && x.BuildingId == buildingId).ToListAsync());
            }

            return specialInvoiceViewModel;
        }

        private List<SpecialInvoiceViewModel> GetInvoices(int? invoiceId, DateOnly dateFrom, DateOnly dateTo, int? buildingId)
        {
            var invoices = context.BookFinancials.Include(x => x.Customer).ThenInclude(x=>x.Documents).Include(x => x.Customer).ThenInclude(x=>x.Building).Where(x =>  x.Customer.BuildingId == buildingId) // Filter by BuildingId if provided
                .Where(doc => doc.DatumF >= dateFrom && doc.DatumF <= dateTo && doc.InvoiceId == invoiceId.Value)
                .ToListAsync().Result;
            return App.FullMapper.Map<List<SpecialInvoiceViewModel>>(invoices);
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
