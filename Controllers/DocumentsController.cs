using CleanHub.CleanHub.Infrastructure.Data;
using CleanHub.Config;
using CleanHub.Entities;
using CleanHub.Extensions;
using CleanHub.Helpers;
using CleanHub.Services;
using CleanHub.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CleanHub.Controllers
{
    public class DocumentsController(ApplicationDbContext context, IOptions<SMTPConfig> config, IOptions<CompanyConfig> companyConfig) : Controller
    {
        private readonly CompanyConfig _config;
        private SMTPConfig _smtpConfig;
        private static DateOnly DateFrom = DateOnly.FromDateTime(DateTime.Now);
        private static DateOnly DateTo = DateOnly.FromDateTime(DateTime.Now);
        public List<Building> Buildings { get; set; } = context.Buildings.ToList();


        [Route("Unpayed")]
        [Route("Неплатени")]
        public async Task<IActionResult> Unpayed()
        {
            List<DocumentViewModel> documents = new List<DocumentViewModel>();
            var documentEntities = await context.Documents.Where(x => x.PaymentStatus == PaymentStatus.Неплатено || x.PaymentStatus == PaymentStatus.Задоцнето).AsNoTracking().Select(c => new Entities.Document
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
                    context.Documents.Update(documentEntity);

                }
                context.SaveChanges();
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

            var documentEntity = await context.Documents.Include(x => x.Customer).Where(x => x.PaymentStatus == PaymentStatus.Задоцнето).AsNoTracking().Select(c => new Entities.Document
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
            ViewBag.PaymentStatusList = Enum.GetValues(typeof(PaymentStatus))
                .Cast<PaymentStatus>()
                .Select(e => new SelectListItem
                {
                    Text = e.GetEnumDescription(),
                    Value = ((int)e).ToString()
                })
                .ToList();
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

            return View("Index");
        }

        private List<BookFinancialViewModel> GetInvoices(int? invoiceId, DateOnly dateFrom, DateOnly dateTo, int? buildingId)
        {
            var invoices = context.BookFinancials.Include(x => x.Customer).ThenInclude(x => x.Documents).Include(x => x.Customer).ThenInclude(x => x.Building).Where(x => x.Customer.BuildingId == buildingId) // Filter by BuildingId if provided
                .Where(doc => doc.DatumF >= dateFrom && doc.DatumF <= dateTo && doc.InvoiceId == invoiceId.Value)
                .ToListAsync().Result;
            return App.FullMapper.Map<List<BookFinancialViewModel>>(invoices);
        }

        public async Task<IActionResult> InvoiceFiltered(int? buildingId, int? paymentStatusId, string dateFrom, string dateTo)
        {
            Buildings.Insert(0, new Building()
            {
                Name = "Сите",
                Id = 0
            });
            if (!Buildings.Any())
            {
                throw new Exception("No buildings found in the database.");
            }
            ViewBag.PaymentStatusList = Enum.GetValues(typeof(PaymentStatus))
                .Cast<PaymentStatus>()
                .Select(e => new SelectListItem
                {
                    Text = e.GetEnumDescription(),
                    Value = ((int)e).ToString(),
                    Selected = (int)e == paymentStatusId // Markiere den ausgewählten Status
                })
                .ToList();
            ViewBag.Buildings = new SelectList(Buildings, "Id", "Name", buildingId);
            var documentEntity = new List<Document>();
            if (buildingId == 0)
            {
                documentEntity = await context.Documents
                    .Where(d => d.Customer != null && d.Customer.Building != null)
                    .Select(d => new Entities.Document
                    {
                        Id = d.Id,
                        Number = d.Number,
                        ToDocument = d.ToDocument,
                        Date = d.Date,
                        DueDate = d.DueDate,
                        Description = d.Description,
                        DateReceived = d.DateReceived,
                        PaymentStatus = d.PaymentStatus,
                        TotalOutput = d.TotalOutput,
                        Customer = new Entities.Customer
                        {
                            CustomerInfo = d.Customer.CustomerInfo
                        }
                    }).ToListAsync();
            }
            else
            {

                documentEntity = await context.Buildings
                    .Where(b => b.Id == buildingId.Value)
                    .SelectMany(b => b.Customers)
                    .SelectMany(c => c.Documents)
                    .Select(c => new Entities.Document
                    {
                        Id = c.Id,
                        Number = c.Number,
                        ToDocument = c.ToDocument,
                        Date = c.Date,
                        DueDate = c.DueDate,
                        Description = c.Description,
                        DateReceived = c.DateReceived,
                        PaymentStatus = c.PaymentStatus,
                        TotalOutput = c.TotalOutput,
                        Customer = c.Customer != null
                            ? new Entities.Customer
                            {
                                CustomerInfo = c.Customer.CustomerInfo,
                            }
                            : null // Setze Customer auf null, falls es nicht existiert
                    }).ToListAsync();
            }
            if (!string.IsNullOrEmpty(dateFrom) && !string.IsNullOrEmpty(dateTo))
            {
                DateFrom = DateOnly.ParseExact(dateFrom, "dd.MM.yyyy", null);
                DateTo = DateOnly.ParseExact(dateTo, "dd.MM.yyyy", null);
                ViewBag.DateFrom = DateFrom.ToString("dd.MM.yyyy");
                ViewBag.DateTo = DateTo.ToString("dd.MM.yyyy");
                documentEntity = documentEntity.Where(x =>
                    x.Date >= DateFrom && x.Date <= DateTo && (int)x.PaymentStatus == paymentStatusId.Value).ToList();
            }
            else if (!string.IsNullOrEmpty(dateFrom))
            {
                DateFrom = DateOnly.ParseExact(dateFrom, "dd.MM.yyyy", null);
                ViewBag.DateFrom = DateFrom.ToString("dd.MM.yyyy");
                documentEntity = documentEntity.Where(x => x.Date >= DateFrom && (int)x.PaymentStatus == paymentStatusId.Value)
                    .ToList();
            }
            else
            {
                documentEntity = documentEntity.Where(x => (int)x.PaymentStatus == paymentStatusId.Value)
                    .ToList();
            }

            var documents = App.FullMapper.Map<List<DocumentViewModel>>(documentEntity);
            foreach (var doc in documents.Where(x => x.PaymentStatus == PaymentStatus.Неплатено || x.PaymentStatus == PaymentStatus.Задоцнето))
            {
                var today = DateOnly.FromDateTime(DateTime.Now);
                var overdueDays = (today.DayNumber - doc.DueDate.Value.DayNumber); // Calculate overdue days
                doc.Delay = (today.DayNumber - doc.DueDate.Value.DayNumber); // DayNumber gives day difference directly
                if (doc.Delay > 30)
                {
                    doc.PaymentStatus = PaymentStatus.Задоцнето;
                }
                else
                {
                    doc.PaymentStatus = PaymentStatus.Неплатено;
                }
                // Determine the percentage based on overdue days
                double percentage = overdueDays switch
                {
                    < 0 => 0, // Not overdue
                    < 30 => 0.02, // 2%
                    >= 30 and <= 60 => 0.04, // 4%
                    >= 61 and <= 90 => 0.06, // 6%
                    >= 91 and <= 180 => 0.08, // 8%
                    >= 181 and <= 360 => 0.10, // 10%
                    >= 361 and <= 730 => 0.13, // 13%
                    _ => 0.16 // 16% for 730+ days
                };

                doc.NewTotal = (int)Math.Round(doc.TotalOutput.Value * (1 + percentage), MidpointRounding.AwayFromZero);
            }
            return View("Index", documents);
        }
        // GET: Invoices/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var documentEntity = await context.Documents.Include(x => x.Books).Include(d => d.Customer).FirstOrDefaultAsync(xd => xd.Id == id);
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
            var buildings = context.Buildings.Include(x => x.BuildingProducts)
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
                    ? context.Products.ToList()
                    : context.Products.ToList().Where(x => x.ArticleNotes != null && x.ArticleNotes.Contains("влез")).ToList();

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
            var buildings = context.Buildings
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
                var building = context.Buildings.Include(x => x.Customers)
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
                        CreateBookFinancialAndReserve(docEntity, document.Date, customer.Id, building.ReserveFund.Value);
                    }
                }

                CreateSpecialInvoice(document);
                await context.SaveChangesAsync();
                HttpContext.Session.Remove("Documents");

                return RedirectToAction(nameof(Create));
            }
            //ViewData["ResidentId"] = new SelectList(_context.Customers, "Id", "CustomerInfo", document.CustomerId);
            return View(document);
        }

        private void CreateSpecialInvoice(DocumentViewModel document)
        {
            int sum = 0;
            var energyValues = document.Building.BuildingProducts
                .Where(x => x.ArticleNotes.ToLower().Contains("енер.")
                            || x.ArticleNotes.ToLower().Contains("осветлување"))
                .ToList();
            if (energyValues != null && energyValues.Any())
            {
                sum = (int)Math.Round((decimal)energyValues.Sum(x => x.PriceWithTax), MidpointRounding.AwayFromZero);
            }
            float price = 0;
            float priceWithTax = 0;
            foreach (var energy in energyValues)
            {
                if (int.TryParse(energy.PriceWithTax.ToString(), out int priceInt))
                {
                    price = priceInt / 100f;
                    sum += (int)(price);
                }
            }

            var specialInvoiceViewModel = new SpecialInvoiceViewModel
            {
                ForDate = document.Date.Value,
                Total = sum,
                InvoiceId = Constants.Energy,
                BuildingId = document.BuildingId,
                Status = PaymentStatus.Неплатено
            };
            var specialInvoice = App.FullMapper.Map<SpecialInvoice>(specialInvoiceViewModel);
            // Ensure the BuildingId is treated as a foreign key reference
            context.Entry(specialInvoice).Property(s => s.BuildingId).IsModified = true;

            // Alternatively, attach the entity and set the BuildingId explicitly
            context.Attach(specialInvoice);
            specialInvoice.BuildingId = document.BuildingId;

            // Add the SpecialInvoice to the context
            context.SpecialInvoices.Add(specialInvoice);
        }

        private void CreateBookFinancialAndReserve(Document docEntity, DateOnly? date, int customerId, int reserve)
        {
            var bookFinancialViewModel = new BookFinancialViewModel
            {
                InvoiceId = Constants.Recieve,
                DocumentId = docEntity.Id,
                Demands = docEntity!.TotalOutput!.Value!,
                Owes = 0,
                CustomerId = customerId,
                Time = DateTime.Now,
                DatumF = date,
                Description = string.Empty,
            };

            var bookFinancialViewModelReserve = new BookFinancialViewModel
            {
                InvoiceId = Constants.Reserve,
                DocumentId = docEntity.Id,
                Demands = reserve,
                Owes = 0,
                DatumF = date,
                CustomerId = customerId,
                Time = DateTime.Now,
                Description = string.Empty,
            };
            var bookFinancial = App.FullMapper.Map<BookFinancial>(bookFinancialViewModel);
            var bookFinancialReserve = App.FullMapper.Map<BookFinancial>(bookFinancialViewModelReserve);
            context.BookFinancials.Add(bookFinancial);
            context.BookFinancials.Add(bookFinancialReserve);
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
            context.Books.Add(entityBook);
        }

        private async Task<Document> CreateCustomerDocument(int customerId, DocumentViewModel document, Building building)
        {
            var documentCustomer = new DocumentViewModel();
            documentCustomer.CustomerId = customerId;
            documentCustomer.Number = context!.Documents.OrderBy(x => x.Number).LastOrDefault()?.Number + 1;
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

            context.Documents.Add(docEntity);
            await context.SaveChangesAsync();
            return docEntity;
        }

        // GET: Invoices/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var documentEntity = await context.Documents.Include(x => x.Books).Include(d => d.Customer).FirstOrDefaultAsync(xd => xd.Id == id);
            var document = App.FullMapper.Map<DocumentViewModel>(documentEntity);
            if (document == null)
            {
                return NotFound();
            }

            document.Company = _config;
            ViewData["CustomerId"] = new SelectList(context.Customers, "Id", "Id", document.CustomerId);
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
                    context.Update(documentEntity);
                    await context.SaveChangesAsync();
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
            ViewData["CustomerId"] = new SelectList(context.Customers, "Id", "Name", document.CustomerId);
            return View(document);
        }

        // GET: Invoices/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var invoice = await context.Documents
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
            var invoice = await context.Documents.FindAsync(id);
            if (invoice != null)
            {
                context.Documents.Remove(invoice);
            }

            await context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool InvoiceExists(int id)
        {
            return context.Documents.Any(e => e.Id == id);
        }
    }
}
