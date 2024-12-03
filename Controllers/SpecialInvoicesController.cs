using CleanHub.CleanHub.Infrastructure.Data;
using CleanHub.Entities;
using CleanHub.Extensions;
using CleanHub.Helpers;
using CleanHub.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace CleanHub.Controllers
{
    public class SpecialInvoicesController(ApplicationDbContext context) : Controller
    {
        private static DateOnly DateFrom = DateOnly.FromDateTime(DateTime.Now);
        private static DateOnly DateTo = DateOnly.FromDateTime(DateTime.Now);

        // GET: SpecialInvoicesController
        public async Task<IActionResult> Index()
        {
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
            //var specialInvoicesViewModel = new List<SpecialInvoiceViewModel>();
            //if (Id == Constants.Energy)
            //{
            //    var specialInvoices = context.SpecialInvoices.Include(x=>x.Building)
            //        .Where(x => x.InvoiceId == Constants.Energy && x.InvoiceId == Id)
            //        .ToList();
            //    specialInvoicesViewModel = App.FullMapper.Map<List<SpecialInvoiceViewModel>>(specialInvoices);
            //}
            return View();
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

        // POST: SpecialInvoicesController/Edit/5
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
                Selected = b.Id == buildingId.Value // `selectedBuildingId` is the ID to pre-select
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
            return View("Index", model: specialInvoiceViewModel);
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


        // GET: SpecialInvoicesController/Delete/5
        public ActionResult Delete(int id)
        {
            return View();
        }

        // POST: SpecialInvoicesController/Delete/5
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
