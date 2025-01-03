using CleanHub.Attribute;
using CleanHub.CleanHub.Infrastructure.Data;
using CleanHub.Config;
using CleanHub.Entities;
using CleanHub.Entities.Enums;
using CleanHub.Extensions;
using CleanHub.Helpers;
using CleanHub.Services;
using CleanHub.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SelectPdf;
using System.Reflection.PortableExecutable;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace CleanHub.Controllers
{
    [RequireLogin]
    public class DocumentsController(ApplicationDbContext _context, IOptions<SMTPConfig> _smtpConfig, IOptions<CompanyConfig> _config, IUnitOfWork _unitOfWork, ICompositeViewEngine _viewEngine, IHttpContextAccessor _httpContextAccessor) : Controller
    {

        private static DateOnly DateFrom = DateOnly.FromDateTime(DateTime.Now);
        private static DateOnly DateTo = DateOnly.FromDateTime(DateTime.Now);
        public List<Building> Buildings { get; set; } = _context.Buildings.ToList();


        [Route("Unpayed")]
        [Route("Неплатени")]
        public async Task<IActionResult> Unpayed()
        {
            List<DocumentViewModel> documents = new List<DocumentViewModel>();

            // Retrieve document entities with relevant statuses
            var documentEntities = await _context.Documents
                .Where(x => x.PaymentStatus == PaymentStatus.Неплатено || x.PaymentStatus == PaymentStatus.Задоцнето)
                .AsNoTracking()
                .Select(c => new Entities.Document
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

            // Process documents with overdue status
            foreach (var documentEntity in documentEntities.Where(x => x.PaymentStatus == PaymentStatus.Задоцнето))
            {
                DateOnly currentDate = DateOnly.FromDateTime(DateTime.Now);
                int overdueDays = (currentDate.ToDateTime(TimeOnly.MinValue) - documentEntity.DateReceived.Value.ToDateTime(TimeOnly.MinValue)).Days;

                // Calculate the additional fee
                float additionalFeePercentage = GetOverdueFeePercentage(overdueDays);
                float additionalFee = documentEntity.TotalOutput.Value * (additionalFeePercentage / 100);

                // Update document total
                documentEntity.TotalOutput += additionalFee;
                _context.Documents.Update(documentEntity);
            }

            // Save changes to the context
            await _context.SaveChangesAsync();

            documents = App.ReaderMapper.Map<List<DocumentViewModel>>(documentEntities);
            documents.ForEach(x => x.Company = _config.Value);

            return View(nameof(Index), documents);
        }
        private float GetOverdueFeePercentage(int overdueDays)
        {
            if (overdueDays < 30)
                return 2f;
            else if (overdueDays < 60)
                return 4f;
            else if (overdueDays < 90)
                return 6f;
            else if (overdueDays < 180)
                return 8f;
            else if (overdueDays < 360)
                return 10f;
            else if (overdueDays < 730)
                return 13f;
            else
                return 16f;
        }
        [Route("Partially")]
        [Route("Делумни")]
        public async Task<IActionResult> Partially()
        {
            var documentEntities = await _context.Documents
                .Where(x => x.PaymentStatus == PaymentStatus.Задоцнето)
                .AsNoTracking()
                .Select(c => new Entities.Document
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
                })
                .ToListAsync();

            var documents = App.ReaderMapper.Map<List<DocumentViewModel>>(documentEntities);
            documents.ForEach(x => x.Company = _config.Value);
            return View(nameof(Index), documents);
        }


        // GET: Documents

        [Route("Сметки")]
        public IActionResult Index()
        {
            // Populate dropdown for PaymentStatus and Buildings
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

            Buildings.Insert(0, new Building { Name = "Сите", Id = 0 });
            ViewBag.Buildings = new SelectList(Buildings, "Id", "Name");

            return View("Index");
        }

        private List<BookFinancialViewModel> GetInvoices(int? invoiceId, DateOnly dateFrom, DateOnly dateTo, int? buildingId)
        {
            var invoices = _context.BookFinancials.Include(x => x.Customer).ThenInclude(x => x.Documents).Include(x => x.Customer).ThenInclude(x => x.Building).Where(x => x.Customer.BuildingId == buildingId) // Filter by BuildingId if provided
                .Where(doc => doc.DatumF >= dateFrom && doc.DatumF <= dateTo && doc.InvoiceId == invoiceId.Value)
                .ToListAsync().Result;
            return App.FullMapper.Map<List<BookFinancialViewModel>>(invoices);
        }

        public async Task<IActionResult> InvoiceFiltered(int? buildingId, int? paymentStatusId, string dateFrom, string dateTo)
        {
            // Add default building and payment status list
            Buildings.Insert(0, new Building() { Name = "Сите", Id = 0 });
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
                    Selected = (int)e == paymentStatusId
                })
                .ToList();

            ViewBag.Buildings = new SelectList(Buildings, "Id", "Name", buildingId);

            var documentEntities = await GetDocuments(buildingId, paymentStatusId, dateFrom, dateTo);

            var documents = App.FullMapper.Map<List<DocumentViewModel>>(documentEntities);
            foreach (var doc in documents)
            {
                doc.Delay = CalculateOverdueDays(doc.DueDate);
                doc.NewTotal = CalculateNewTotal(doc);
            }

            return View("Index", documents);
        }
        private int CalculateOverdueDays(DateOnly? dueDate)
        {
            var today = DateOnly.FromDateTime(DateTime.Now);
            return (today.DayNumber - dueDate.Value.DayNumber); // Direct day difference
        }

        private int CalculateNewTotal(DocumentViewModel doc)
        {
            double percentage = GetOverdueFeePercentage(doc.Delay.Value);
            return (int)Math.Round(doc.TotalOutput.Value * (1 + percentage), MidpointRounding.AwayFromZero);
        }

        private async Task<List<Entities.Document>> GetDocuments(int? buildingId, int? paymentStatusId, string dateFrom, string dateTo)
        {
            var query = _context.Documents.Include(d => d.Customer).ThenInclude(c => c.Building)
                .Where(d => !buildingId.HasValue || d.Customer.BuildingId == buildingId.Value)
                .AsQueryable();

            if (paymentStatusId.HasValue)
            {
                query = query.Where(d => (int)d.PaymentStatus == paymentStatusId.Value);
            }

            if (!string.IsNullOrEmpty(dateFrom) && !string.IsNullOrEmpty(dateTo))
            {
                var startDate = DateOnly.ParseExact(dateFrom, "dd.MM.yyyy", null);
                var endDate = DateOnly.ParseExact(dateTo, "dd.MM.yyyy", null);
                query = query.Where(d => d.Date >= startDate && d.Date <= endDate);
            }

            return await query.ToListAsync();
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
            _unitOfWork.BookFinancials.SetOwesAndDemandsToDocument(documentViewModel.Customer.BuildingId, invoiceId: 1201, status: null, documentViewModel);

            documentViewModel.Company = _config.Value;

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
            documentViewModel.Company = _config.Value;

            var buildings = _unitOfWork.Buildings.GetAll(query => query.Include(x => x.BuildingProducts).Include(x => x.Customers))
                .Select(b => new Building()
                {
                    Id = b.Id,
                    Name = b.Name,
                    ReserveFund = b.ReserveFund,
                    Customers = b.Customers,
                    BuildingProducts = b.BuildingProducts
                })
                .ToList();
            _unitOfWork.BookFinancials.SetOwesAndDemandsToDocument(buildingId.HasValue ? buildingId.Value : 1, invoiceId: 1201, status: null, documentViewModel);
            documentViewModel.Buildings = App.FullMapper.Map<List<BuildingViewModel>>(buildings);
            documentViewModel.Building = buildingId.HasValue ? documentViewModel.Buildings.FirstOrDefault(x => x.Id == buildingId) : documentViewModel.Buildings.FirstOrDefault();
            ViewBag.Buildings = new SelectList(buildings, "Id", "Name", buildingId.HasValue ? buildingId.Value : 1);
            var selectedBuildingName = buildings?.FirstOrDefault(x => x.Id == (buildingId ?? 1))?.Name;
            if (selectedBuildingName != null)
                ViewBag.SelectedBuildingName = selectedBuildingName;
            var filteredProducts = (id == 0)
                ? documentViewModel.Building?.BuildingProducts
                : documentViewModel.Building?.BuildingProducts.Where(x => x.ArticleNotes != null && x.ArticleNotes.Contains("влез")).ToList();

            if (filteredProducts != null && filteredProducts.Any())
            {
                if (documentViewModel.Building != null) documentViewModel.Building.BuildingProducts = filteredProducts;
            }
            else
            {
                var basicProducts = (id == 0)
                    ? _context.Products.ToList()
                    : _context.Products.ToList().Where(x => x.ArticleNotes != null && x.ArticleNotes.Contains("влез")).ToList();

                if (documentViewModel.Building != null)
                {
                    documentViewModel.Building.BuildingProducts =
                        App.FullMapper.Map<List<BuildingProductViewModel>>(basicProducts);
                    foreach (var product in documentViewModel.Building.BuildingProducts.Where(p =>
                                 p.ArticleNotes != null && p.ArticleNotes.Contains("Резервен")))
                    {
                        if (documentViewModel.Building.ReserveFund != null)
                            product.Price = documentViewModel.Building.ReserveFund.Value;
                    }
                }
                for (int i = 0; i < 3; i++)
                {
                    documentViewModel.Building?.BuildingProducts.Add(new BuildingProductViewModel
                    {
                        ArticleNotes = "",
                        UnitOfMeasurement = "",
                        Quantity = 1,
                        Price = 0,
                        Tax = 0,
                        PriceWithTax = 0,
                        Total = 0,
                        GetFromReserve = true
                    });
                }
            }

            //ViewData["CustomerId"] = new SelectList(_context.Customers, "Id", "CustomerInfo");
            return View(documentViewModel);
        }

        // GET: Invoices/Create
        public IActionResult CreatePartially()
        {
            var documentViewModel = new DocumentViewModel();
            documentViewModel.Company = _config.Value;
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
                var buildingProdutsToRemove = document.Building?.BuildingProducts
                    .Where(x => string.IsNullOrWhiteSpace(x.ArticleNotes)).ToList();
                if (buildingProdutsToRemove != null && buildingProdutsToRemove.Any())
                {
                    foreach (var buildingProduct in buildingProdutsToRemove)
                    {
                        document.Building?.BuildingProducts.Remove(buildingProduct);
                    }
                }

                var buildingProductsFromReserve =
                    document.Building?.BuildingProducts.Where(x => x.GetFromReserve).ToList();
                if (buildingProductsFromReserve != null && buildingProductsFromReserve.Any())
                {
                    var priceTotal = buildingProductsFromReserve.Sum(x => x.Total);
                    /// Get the reserve and minus the priceTotal
                }

                var building = _context.Buildings.Include(x => x.Customers)
                    .FirstOrDefault(x => x.Id == document.BuildingId);
                if (building != null)
                {
                    foreach (var customer in building.Customers.ToList())
                    {
                        var docEntity = await CreateCustomerDocument(customer.Id, document, building);

                        foreach (var buildingProduct in document?.Building?.BuildingProducts)
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
                        CreateBookFinancialAndReserve(docEntity, customer.Id, building.ReserveFund ?? 0, document.PaymentDate, document.PaymentType, document.PaymentNumber);
                    }
                }

                CreateSpecialInvoice(document);
                await _context.SaveChangesAsync();
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
            _context.Entry(specialInvoice).Property(s => s.BuildingId).IsModified = true;

            // Alternatively, attach the entity and set the BuildingId explicitly
            _context.Attach(specialInvoice);
            specialInvoice.BuildingId = document.BuildingId;

            // Add the SpecialInvoice to the context
            _context.SpecialInvoices.Add(specialInvoice);
        }

        private void CreateBookFinancialAndReserve(Document docEntity, int customerId, int reserve, DateOnly? paymentDate, PaymentType paymentType, string? paymentNumber)
        {
            var bookFinancialViewModel = new BookFinancialViewModel
            {
                InvoiceId = Constants.Recieve,
                DocumentId = docEntity.Id,
                Demands = docEntity!.TotalOutput!.Value!,
                Owes = 0,
                CustomerId = customerId,
                Time = DateTime.Now,
                Status = PaymentStatus.Неплатено,
                DatumF = docEntity.DueDate,
                Description = string.Empty,
            };

            var bookFinancialViewModelReserve = new BookFinancialViewModel
            {
                InvoiceId = Constants.Reserve,
                DocumentId = docEntity.Id,
                Demands = reserve,
                Owes = 0,
                DatumF = docEntity.DueDate,
                CustomerId = customerId,
                Status = PaymentStatus.Неплатено,
                Time = DateTime.Now,
                Description = string.Empty,
            };
            var bookFinancial = App.FullMapper.Map<BookFinancial>(bookFinancialViewModel);
            var bookFinancialReserve = App.FullMapper.Map<BookFinancial>(bookFinancialViewModelReserve);
            _context.BookFinancials.Add(bookFinancial);
            _context.BookFinancials.Add(bookFinancialReserve);
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
            documentCustomer.DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10));
            var calculator = new PriceCalculator(building.Customers.Count());

            // Calculate prices for each product
            calculator.CalculatePrices(document.Building.BuildingProducts);

            // Calculate the total PriceWithTax sum
            float totalPriceWithTax = calculator.CalculateTotalPriceWithTaxSum(document.Building.BuildingProducts);
            documentCustomer.TotalOutput = totalPriceWithTax;
            var customer = _context.Customers.FirstOrDefault(x => x.Id == customerId);
            if (customer != null && customer.Subscription.HasValue && customer.Subscription != 0 && customer.Subscription >= documentCustomer.TotalOutput)
            {
                float price = 0;
                float priceWithTax = 0;
                if (int.TryParse(documentCustomer.TotalOutput.ToString(), out int priceInt))
                {
                    price = priceInt / 100f;
                }
                if (int.TryParse(documentCustomer.TotalOutput.Value.ToString(), out int priceWithTaxInt))
                {
                    priceWithTax = priceWithTaxInt / 100f;
                }
                //customer.Subscription = customer.Subscription - documentCustomer.TotalOutput.Value;
                documentCustomer.PaymentStatus = PaymentStatus.Неплатено;
            }
            else
            {
                documentCustomer.PaymentStatus = PaymentStatus.Неплатено;
            }
            var docEntity = App.FullMapper.Map<Document>(documentCustomer);

            _context.Documents.Add(docEntity);
            await _context.SaveChangesAsync();
            return docEntity;
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetStatusPayment(DocumentViewModel model)
        {
            if (model.Id == 0)
            {
                return NotFound();
            }
            try
            {
                var documentToUpdate = await _context.Documents.FirstOrDefaultAsync(x => x.Id == model.Id);
                if (documentToUpdate == null)
                {
                    return NotFound();
                }
                documentToUpdate.PaymentStatus = PaymentStatus.Платено;
                documentToUpdate.PaymentDate = model.PaymentDate;
                documentToUpdate.PaymentType = model.PaymentType;
                documentToUpdate.PaymentNumber = model.PaymentNumber;

                // Update related BookFinancials
                var bookfinancialToUpdate = await _context.BookFinancials.Where(x => x.DocumentId == model.Id).ToListAsync();
                if (bookfinancialToUpdate == null) throw new ArgumentNullException(nameof(bookfinancialToUpdate));
                foreach (var item in bookfinancialToUpdate)
                {
                    if (model.PaymentDate != null) item.PaymentDate = model.PaymentDate.Value;
                    item.PaymentType = model.PaymentType;
                    item.PaymentNumber = model.PaymentNumber;
                    item.Status = PaymentStatus.Платено;
                }

                _context.Update(documentToUpdate);
                _context.UpdateRange(bookfinancialToUpdate);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Documents.Any(x => x.Id == model.Id))
                {
                    return NotFound();
                }

                // Optionally log error here for better debugging
                throw;
            }

            return RedirectToAction(nameof(Index));
        }
        // GET: Invoices/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var documentEntity = await _context.Documents
                .Include(x => x.Books)
                .Include(d => d.Customer)
                .FirstOrDefaultAsync(xd => xd.Id == id);

            if (documentEntity == null)
            {
                return NotFound();
            }

            var document = App.FullMapper.Map<DocumentViewModel>(documentEntity);
            document.Company = _config.Value;

            ViewData["CustomerId"] = new SelectList(_context.Customers, "Id", "Name", document.CustomerId);
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
                    var documentEntity = App.FullMapper.Map<Document>(document);  // Map to Document entity, not ViewModel
                    _context.Update(documentEntity);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!InvoiceExists(document.Id))
                    {
                        return NotFound();
                    }

                    // Optionally log error here for better debugging
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

        public async Task<IActionResult> CombineAndDownloadPdfs(int? buildingId, int? paymentStatusId, string dateFrom, string dateTo)
        {
            // Hole die Kunden, die dem Gebäude zugeordnet sind
            var customers = _context.Buildings
                .Where(x => x.Id == buildingId)
                .Include(x => x.Customers)
                .SelectMany(d => d.Customers)
                .ToList();

            var startDate = new DateOnly();
            var endDate = new DateOnly();

            if (!string.IsNullOrEmpty(dateFrom) && !string.IsNullOrEmpty(dateTo))
            {
                startDate = DateOnly.ParseExact(dateFrom, "dd.MM.yyyy", null);
                endDate = DateOnly.ParseExact(dateTo, "dd.MM.yyyy", null);
            }
            var owes = 0;
            var demands = 0;
            (owes, demands) = _unitOfWork.BookFinancials.GetBuildingReserve(buildingId.Value, invoiceId: 1201, status: null);

            PdfDocument endDoc = new PdfDocument();  // Initialize the final document to append pages to.
            MemoryStream pdfStream = new MemoryStream();
            foreach (var item in customers)
            {
                var document = App.FullMapper.Map<DocumentViewModel>(_context.Documents
                    .Include(x => x.Customer)
                    .Include(x => x.Books)
                    .FirstOrDefault(x =>
                        x.CustomerId == item.Id && x.Date!.Value.Year == startDate.Year && x.Date!.Value.Month == endDate.Month));

                if (document == null)
                {
                    continue;  
                }
                document.TotalBuildingDemands = demands;
                document.TotalBuildingOwes = owes;
                document.Company = _config.Value;
                document.IsForPdf = true;

                string htmlContent = await RenderPartialViewToStringAsync("~/Views/Shared/_DocumentDetailPartial.cshtml", document);

                var request = _httpContextAccessor?.HttpContext?.Request;
                string baseUrl = $"{request?.Scheme}://{request?.Host.Value}/";

                HtmlToPdf converter = new HtmlToPdf();
                PdfDocument doc = converter.ConvertHtmlString(htmlContent, baseUrl);
                endDoc.Append(doc);
            }

            byte[] pdf = endDoc.Save();

            endDoc.Close();

            FileResult fileResult = new FileContentResult(pdf, "application/pdf");
            fileResult.FileDownloadName = $"{Buildings.FirstOrDefault(x => x.Id == buildingId.Value)?.Name}_{dateFrom}_{dateTo}";
            return fileResult;
        }

        private async Task<string> RenderPartialViewToStringAsync(string viewPath, object model)
        {
            ViewData.Model = model;
            using (var writer = new StringWriter())
            {
                var viewResult = _viewEngine.GetView("", viewPath, false);

                if (viewResult.View == null)
                {
                    throw new ArgumentNullException($"The view '{viewPath}' was not found.");
                }

                var viewContext = new ViewContext(
                    ControllerContext,
                    viewResult.View,
                    ViewData,
                    TempData,
                    writer,
                    new HtmlHelperOptions()
                );

                await viewResult.View.RenderAsync(viewContext);
                return writer.GetStringBuilder().ToString();
            }
        }
    }
}
