using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using CleanHub.Data;
using CleanHub.Models;
using CleanHub.Config;
using Microsoft.Extensions.Options;

namespace CleanHub.Controllers
{
    public class InvoicesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly CompanyConfig _config;
        public InvoicesController(ApplicationDbContext context, IOptions<CompanyConfig> config)
        {
            _context = context;
            _config = config.Value;
        }

        // GET: Invoices
        public async Task<IActionResult> Index()
        {
            var applicationDbContext = _context.Invoices.Include(i => i.Resident).Where(x=>x.PaymentStatus == PaymentStatus.Задоцнето || x.PaymentStatus == PaymentStatus.Неплатено);
            return View(await applicationDbContext.ToListAsync());
        }

        // GET: Invoices/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var invoice = await _context.Invoices.Include(x => x.Resident).ThenInclude(d => d.Building).FirstOrDefaultAsync(xd => xd.Id == id);

            if (invoice == null)
            {
                return NotFound();
            }
            invoice.Company = _config;

            return PartialView("_InvoiceDetail", invoice);
        }

        // GET: Invoices/Create
        public IActionResult Create()
        {
            ViewData["ResidentId"] = new SelectList(_context.Residents, "Id", "Id");
            return View();
        }

        // POST: Invoices/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,InvoiceNumber,AmountDue,DueDate,CreatedDate,PaymentStatus,Discount,ResidentId")] Invoice invoice)
        {
            if (ModelState.IsValid)
            {
                _context.Add(invoice);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["ResidentId"] = new SelectList(_context.Residents, "Id", "Id", invoice.ResidentId);
            return View(invoice);
        }

        // GET: Invoices/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var invoice = await _context.Invoices.Include(x => x.Resident).ThenInclude(d => d.Building).FirstOrDefaultAsync(xd => xd.Id == id);
            if (invoice == null)
            {
                return NotFound();
            }
            invoice.Company = _config;
            ViewData["ResidentId"] = new SelectList(_context.Residents, "Id", "Id", invoice.ResidentId);
            return PartialView("_InvoiceDetail",invoice);
        }

        // POST: Invoices/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,InvoiceNumber,AmountDue,DueDate,CreatedDate,PaymentStatus,Discount,ResidentId")] Invoice invoice)
        {
            if (id != invoice.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(invoice);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!InvoiceExists(invoice.Id))
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
            ViewData["ResidentId"] = new SelectList(_context.Residents, "Id", "Id", invoice.ResidentId);
            return View(invoice);
        }

        // GET: Invoices/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var invoice = await _context.Invoices
                .Include(i => i.Resident)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (invoice == null)
            {
                return NotFound();
            }

            return View(invoice);
        }

        // POST: Invoices/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var invoice = await _context.Invoices.FindAsync(id);
            if (invoice != null)
            {
                _context.Invoices.Remove(invoice);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool InvoiceExists(int id)
        {
            return _context.Invoices.Any(e => e.Id == id);
        }
    }
}
