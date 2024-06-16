using CleanHub.Config;
using CleanHub.Entities;
using CleanHub.Infrastructure.Data;
using CleanHub.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace CleanHub.Controllers
{
    public class DocumentsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly CompanyConfig _config;
        public DocumentsController(ApplicationDbContext context, IOptions<CompanyConfig> config)
        {
            _context = context;
            _config = config.Value;
        }

        // GET: Documents
        public async Task<IActionResult> Index()
        {
            string documentsJson = HttpContext.Session.GetString("Documents");
            var settings = new JsonSerializerSettings
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            };
            List<DocumentViewModel> documents;
            if (!string.IsNullOrEmpty(documentsJson))
            {
                documents = JsonConvert.DeserializeObject<List<DocumentViewModel>>(documentsJson, settings);
            }
            else
            {
                var documentEntity = await _context.Documents.AsNoTracking().Select(c => new Document
                {
                    Id = c.Id,
                    Number = c.Number,
                    ToDocument = c.ToDocument,
                    DateReceived = c.DateReceived,
                }).ToListAsync();
                documents = App.ReaderMapper.Map<List<DocumentViewModel>>(documentEntity);

                HttpContext.Session.SetString("Documents", JsonConvert.SerializeObject(documents, settings));
            }
            return View(documents);
        }

        // GET: Invoices/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var documentEntity = await _context.Documents.Include(x => x.Books).Include(d => d.Customer).FirstOrDefaultAsync(xd => xd.Id == id);
            var documentViewModel = App.FullMapper.Map<DocumentViewModel>(documentEntity);

            if (documentViewModel == null)
            {
                return NotFound();
            }
            documentViewModel.Company = _config;

            return PartialView("_DocumentDetailPartial", documentViewModel);
        }

        // GET: Invoices/Create
        public IActionResult Create()
        {
            ViewData["CustomerId"] = new SelectList(_context.Customers, "Id", "CustomerInfo");
            return View();
        }

        // POST: Invoices/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create( DocumentViewModel document)
        {
            if (ModelState.IsValid)
            {
                var entity = App.FullMapper.Map<Document>(document);
                _context.Add(document);
                await _context.SaveChangesAsync();
                HttpContext.Session.Remove("Buildings");

                return RedirectToAction(nameof(Index));
            }
            ViewData["ResidentId"] = new SelectList(_context.Customers, "Id", "CustomerInfo", document.CustomerId);
            return View(document);
        }

        // GET: Invoices/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var documentEntity= await _context.Documents.Include(x => x.Books).Include(d => d.Customer).FirstOrDefaultAsync(xd => xd.Id == id);
            var document = App.FullMapper.Map<DocumentViewModel>(documentEntity);
            if (document == null)
            {
                return NotFound();
            }

            document.Company = _config;
            ViewData["CustomerId"] = new SelectList(_context.Customers, "Id", "Id", document.CustomerId);
            return PartialView("_DocumentDetailPartial", document);
        }

        // POST: Invoices/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, DocumentViewModel document)
        {
            if (id != document.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var documentEntity = App.FullMapper.Map<DocumentViewModel>(document);
                    _context.Update(documentEntity);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!InvoiceExists(document.Id))
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
            ViewData["CustomerId"] = new SelectList(_context.Customers, "Id", "Name", document.CustomerId);
            return View(document);
        }

        // GET: Invoices/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var invoice = await _context.Documents
                .Include(i => i.Books)
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
            var invoice = await _context.Documents.FindAsync(id);
            if (invoice != null)
            {
                _context.Documents.Remove(invoice);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool InvoiceExists(int id)
        {
            return _context.Documents.Any(e => e.Id == id);
        }
    }
}
