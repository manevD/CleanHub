using CleanHub.CleanHub.Infrastructure.Data;
using CleanHub.Entities;
using CleanHub.Helpers;
using CleanHub.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CleanHub.Controllers
{
    public class SpecialInvoicesController(ApplicationDbContext context) : Controller
    {
        // GET: SpecialInvoicesController
        public ActionResult Index(int Id)
        {
            var specialInvoicesViewModel = new List<SpecialInvoiceViewModel>();
            if (Id == Constants.Energy)
            {
                var specialInvoices = context.SpecialInvoices.Include(x=>x.Building)
                    .Where(x => x.InvoiceId == Constants.Energy && x.InvoiceId == Id)
                    .ToList();
                specialInvoicesViewModel = App.FullMapper.Map<List<SpecialInvoiceViewModel>>(specialInvoices);
            }
            return View(specialInvoicesViewModel);
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
