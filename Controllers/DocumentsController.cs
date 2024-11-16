using CleanHub.Config;
using CleanHub.Entities;
using CleanHub.Helpers;
using CleanHub.Infrastructure.Data;
using CleanHub.Services;
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
        private SMTPConfig _smtpConfig;

        public DocumentsController(ApplicationDbContext context, IOptions<SMTPConfig> config, IOptions<CompanyConfig> companyConfig)
        {
            _context = context;
            _smtpConfig = config.Value;
            _config = companyConfig.Value;
        }

        [Route("Unpayed")]
        [Route("Неплатени")]
        public async Task<IActionResult> Unpayed()
        {
            List<DocumentViewModel> documents = new List<DocumentViewModel>();
            var documentEntities = await _context.Documents.Where(x => x.PaymentStatus == PaymentStatus.Неплатено || x.PaymentStatus == PaymentStatus.Задоцнето).AsNoTracking().Select(c => new Entities.Document
            {
                Id = c.Id,
                Number = c.Number,
                PaymentStatus = c.PaymentStatus,
                ToDocument = c.ToDocument,
                TotalOutput = c.TotalOutput,
                CreatedTime = c.CreatedTime,
                Customer = c.Customer,
                DueDate = c.DueDate.Value,
                DateReceived = c.DateReceived,
            }).ToListAsync();
            var documentsWithStatusLate = documentEntities.Where(x => x.PaymentStatus == PaymentStatus.Задоцнето).ToList();
            if (documentsWithStatusLate != null && documentsWithStatusLate.Any())
            {
                foreach (var documentEntity in documentsWithStatusLate)
                {
                    // Get today's date as DateOnly
                    DateOnly currentDate = DateOnly.FromDateTime(DateTime.Now);

                    // Calculate days overdue
                    int daysOverdue = (currentDate.ToDateTime(TimeOnly.MinValue) - documentEntity.DateReceived.Value.ToDateTime(TimeOnly.MinValue)).Days;
                    float additionalFeePercentage = 0f;

                    // Determine the additional fee percentage based on the number of days overdue
                    if (daysOverdue < 30)
                    {
                        additionalFeePercentage = 2f;
                    }
                    else if (daysOverdue >= 30 && daysOverdue < 60)
                    {
                        additionalFeePercentage = 4f;
                    }
                    else if (daysOverdue >= 60 && daysOverdue < 90)
                    {
                        additionalFeePercentage = 6f;
                    }
                    else if (daysOverdue >= 90 && daysOverdue < 180)
                    {
                        additionalFeePercentage = 8f;
                    }
                    else if (daysOverdue >= 180 && daysOverdue < 360)
                    {
                        additionalFeePercentage = 10f;
                    }
                    else if (daysOverdue >= 360 && daysOverdue < 730)
                    {
                        additionalFeePercentage = 13f;
                    }
                    else if (daysOverdue >= 730)
                    {
                        additionalFeePercentage = 16f;
                    }

                    // Calculate the additional fee and apply it to the document's amount due
                    float additionalFee = documentEntity.TotalOutput.Value * (additionalFeePercentage / 100);
                    documentEntity.TotalOutput += additionalFee;
                    _context.Documents.Update(documentEntity);

                }
                _context.SaveChanges();
            }
            documents = App.ReaderMapper.Map<List<DocumentViewModel>>(documentEntities);
            documents.ForEach(x => x.Company = _config);

            return View(nameof(Index), documents);
        }
        [Route("Partially")]
        [Route("Делумни")]
        public async Task<IActionResult> Partially()
        {
            List<DocumentViewModel> documents = new List<DocumentViewModel>();

            var documentEntity = await _context.Documents.Include(x => x.Customer).Where(x => x.PaymentStatus == PaymentStatus.Задоцнето).AsNoTracking().Select(c => new Entities.Document
            {
                Id = c.Id,
                Number = c.Number,
                PaymentStatus = c.PaymentStatus,
                ToDocument = c.ToDocument,
                Customer = c.Customer,
                TotalOutput = c.TotalOutput,
                CreatedTime = c.CreatedTime,
                DueDate = c.DueDate.Value,
                DateReceived = c.DateReceived,
            }).ToListAsync();

            documents = App.ReaderMapper.Map<List<DocumentViewModel>>(documentEntity);
            documents.ForEach(x => x.Company = _config);
            return View(nameof(Index), documents);
        }

        // GET: Documents

        [Route("Сметки")]
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
                var documentEntity = await _context.Documents.Include(x => x.Customer).AsNoTracking().Select(c => new Entities.Document
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

        /// <summary>
        /// 0 Zbirna 1 Poedinecna
        /// </summary>
        /// <param name="id"></param>
        /// <param name="buildingId"></param>
        /// <returns></returns>
        [HttpGet]
        public IActionResult Create(int? id, int? buildingId)
        {
            var documentViewModel = new DocumentViewModel();
            ViewBag.RouteId = id;
            documentViewModel.Date = DateOnly.FromDateTime(DateTime.UtcNow);
            documentViewModel.Company = _config;
            var buildings = _context.Buildings.Include(x => x.BuildingProducts)
                .Select(b => new Building()
                {
                    Id = b.Id,
                    Name = b.Name,
                    Customers = b.Customers,
                    BuildingProducts = b.BuildingProducts
                })
                .ToList();
            documentViewModel.Buildings = App.FullMapper.Map<List<BuildingViewModel>>(buildings);
            if (buildingId.HasValue)
            {
                documentViewModel.Building = documentViewModel.Buildings.FirstOrDefault(x => x.Id == buildingId);
            }
            else
            {
                documentViewModel.Building = documentViewModel.Buildings.FirstOrDefault();
            }

            var filteredProducts = (id == 0)
                ? documentViewModel.Building.BuildingProducts
                : documentViewModel.Building.BuildingProducts.Where(x => x.ArticleNotes != null && x.ArticleNotes.Contains("влез")).ToList();

            if (filteredProducts.Any())
            {
                documentViewModel.Building.BuildingProducts = filteredProducts;
            }
            else
            {
                var basicProducts = (id == 0)
                    ? _context.Products.ToList()
                    : _context.Products.ToList().Where(x => x.ArticleNotes != null && x.ArticleNotes.Contains("влез")).ToList();

                documentViewModel.Building.BuildingProducts = App.FullMapper.Map<List<BuildingProductViewModel>>(basicProducts);
            }

            //ViewData["CustomerId"] = new SelectList(_context.Customers, "Id", "CustomerInfo");
            return View(documentViewModel);
        }

        // GET: Invoices/Create
        public IActionResult CreatePartially()
        {
            var documentViewModel = new DocumentViewModel();
            documentViewModel.Company = _config;
            var buildings = _context.Buildings
                .Include(b => b.BuildingProducts)
                .Select(b => new Building()
                {
                    Id = b.Id,
                    Name = b.Name,
                    BuildingProducts = (ICollection<BuildingProduct>)b.BuildingProducts.Where(x => x.ArticleNotes == "Чистење на влез за")
                })
                .ToList();
            HttpContext.Session.Remove("Buildings");

            documentViewModel.Buildings = App.FullMapper.Map<List<BuildingViewModel>>(buildings);
            documentViewModel.Building = documentViewModel.Buildings.FirstOrDefault();
            return View(nameof(Create), documentViewModel);
        }



        // POST: Invoices/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(DocumentViewModel document)
        {
            if (ModelState.IsValid)
            {
                var building = _context.Buildings.Include(x => x.Customers)
                    .FirstOrDefault(x => x.Id == document.BuildingId);
                if (building != null)
                {
                    foreach (var customer in building.Customers.ToList())
                    {
                        var docEntity = await CreateCustomerDocument(customer.Id, document, building);

                        foreach (var buildingProduct in document.Building.BuildingProducts)
                        {
                            try
                            {
                               CreateBook(buildingProduct, docEntity);
                            }
                         
                            catch (Exception e)
                            {
                                Console.WriteLine(e);
                                throw;
                            }
                        }
                        var bookFinancialViewModel = new BookFinancialViewModel
                        {
                            SmetkaId = 1200,
                            DocumentId = docEntity.Id,
                            Demands = docEntity!.TotalOutput!.Value!,
                            Owes = 0,
                            CustomerId = customer.Id,
                            Time = DateTime.Now,
                            DatumF = document.Date,
                            Description = string.Empty,
                        };

                        var bookFinancialViewModelReserve = new BookFinancialViewModel
                        {
                            SmetkaId = 1201,
                            DocumentId = docEntity.Id,
                            Demands = docEntity!.TotalOutput!.Value!,
                            Owes = 0,
                            DatumF = document.Date,
                            CustomerId = customer.Id,
                            Time = DateTime.Now,
                            Description = string.Empty,
                        };
                        var bookFinancial = App.FullMapper.Map<BookFinancial>(bookFinancialViewModel);
                        var bookFinancialReserve = App.FullMapper.Map<BookFinancial>(bookFinancialViewModelReserve);
                        _context.BookFinancials.Add(bookFinancial);
                        _context.BookFinancials.Add(bookFinancialReserve);
                    }
                }

                //var entity = App.FullMapper.Map<Document>(document);
                //_context.Documents.Add(entity);
                await _context.SaveChangesAsync();
                HttpContext.Session.Remove("Documents");

                return RedirectToAction(nameof(Index), document);
            }
            ViewData["ResidentId"] = new SelectList(_context.Customers, "Id", "CustomerInfo", document.CustomerId);
            return View(document);
        }

        private void CreateBook(BuildingProductViewModel book, Document docEntity)
        {
            float price = 0;
            float priceWithTax = 0;
            if (int.TryParse(book.Price.ToString(), out int priceInt))
            {
                price = priceInt / 100f;
            }
            if (int.TryParse(book.PriceWithTax.Value.ToString(), out int priceWithTaxInt))
            {
                priceWithTax = priceWithTaxInt / 100f;
            }

            var bookEntity = new BookViewModel
            {
                DocId = docEntity.Id,
                Output = 1,
                Quantity = book.Quantity,
                PriceWithTax = priceWithTax,
                Tax = book.Tax,
                ArticleId = book.Id,
                Total = priceWithTax,
                ArticleNotes = book.ArticleNotes,
                UnitOfMeasurement = book.UnitOfMeasurement,
            };
            var entityBook = App.FullMapper.Map<Book>(bookEntity);
            _context.Books.Add(entityBook);
            _context.SaveChanges();
        }

        private async Task<Document> CreateCustomerDocument(int customerId, DocumentViewModel document, Building building)
        {
            var documentCustomer = new DocumentViewModel();
            documentCustomer.CustomerId = customerId;
            documentCustomer.Number = _context!.Documents.OrderBy(x => x.Number).LastOrDefault()?.Number + 1;
            documentCustomer.Date = document.Date;
            documentCustomer.ToDocument = DocumentService.GetMonthAsString(document.Date.Value.Month) + " " +
                                          document.Date.Value.Year;
            documentCustomer.Description = building.Name;
            documentCustomer.CreatedTime = DateTime.UtcNow;
            documentCustomer.DateReceived = DateOnly.FromDateTime(DateTime.UtcNow);
            var calculator = new PriceCalculator(building.Customers.Count());

            // Calculate prices for each product
            calculator.CalculatePrices(document.Building.BuildingProducts);

            // Calculate the total PriceWithTax sum
            float totalPriceWithTax = calculator.CalculateTotalPriceWithTaxSum(document.Building.BuildingProducts);
            documentCustomer.TotalOutput = totalPriceWithTax;
            documentCustomer.PaymentStatus = PaymentStatus.Неплатено;
            var docEntity = App.FullMapper.Map<Document>(documentCustomer);

            _context.Documents.Add(docEntity);
            await _context.SaveChangesAsync();
            return docEntity;
        }

        // GET: Invoices/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var documentEntity = await _context.Documents.Include(x => x.Books).Include(d => d.Customer).FirstOrDefaultAsync(xd => xd.Id == id);
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

                    throw;
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
