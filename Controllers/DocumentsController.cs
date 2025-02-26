using CleanHub.Attribute;
using CleanHub.Config;
using CleanHub.Entities;
using CleanHub.Entities.Enums;
using CleanHub.Extensions;
using CleanHub.Helpers;
using CleanHub.Infrastructure.Data;
using CleanHub.Services;
using CleanHub.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SelectPdf;
using System.Net.Mail;
using System.Net;

namespace CleanHub.Controllers
{
    [RequireLogin]
    public class DocumentsController(IOptions<CompanyConfig> _config, IUnitOfWork _unitOfWork, ICompositeViewEngine _viewEngine, IOptions<SMTPConfig> _smtpConfig, IHttpContextAccessor _httpContextAccessor) : Controller
    {

        private static DateOnly DateFrom = DateOnly.FromDateTime(DateTime.Now);
        private static DateOnly DateTo = DateOnly.FromDateTime(DateTime.Now);
        public List<Building> Buildings { get; set; } = _unitOfWork.Buildings.GetAll().ToList();
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

        public async Task CreateAndSend(DocumentViewModel document)
        {
            using (SmtpClient smtpClient = new SmtpClient(_smtpConfig.Value.Server))
            {
                    smtpClient.Credentials = new NetworkCredential(_smtpConfig.Value.Email, _smtpConfig.Value.Passwort);
                    smtpClient.EnableSsl = true;

                    document.Company = _config.Value;
                    document.IsForPdf = true;

                    string htmlContent =
                        await RenderPartialViewToStringAsync("~/Views/Shared/_DocumentDetailPartial.cshtml", document);
                    var request = _httpContextAccessor?.HttpContext?.Request;
                    string baseUrl = $"{request?.Scheme}://{request?.Host.Value}/";
                    HtmlToPdf converter = new HtmlToPdf();
                    PdfDocument doc = converter.ConvertHtmlString(htmlContent, baseUrl);
                    // create memory stream to save PDF
                    MemoryStream pdfStream = new MemoryStream();

                    // save pdf document into a MemoryStream
                    doc.Save(pdfStream);

                    // reset stream position
                    pdfStream.Position = 0;
                    string emailBody =
                        @$"Почитувани,

                            Во прилог ви ја праќаме сметката за {document.Date.Value.Month}/{document.Date.Value.Year}. Ве молиме, проверете ја прикачената сметка и извршете ги потребните активности за плаќање.

                            Во случај на било какви прашања или недоразбирања можете слободно да не контактирате. Ви благодариме за Вашето внимание и соработка.
                            Со почит,

                            {_config.Value.Name}
                            {_config.Value.PhoneNumber}
                            {_config.Value.Email}
                            {_config.Value.Address}";
                    // create email message
                    MailMessage message = new MailMessage();
                    message.From = new MailAddress(_smtpConfig.Value.Email);
                    message.To.Add(document.Customer.Email);
                    message.Subject = string.Concat("Сметка Марти Хигиена ", document.Customer.CustomerInfo, " за ", document.Date.Value.Month, "/",
                        document.Date.Value.Year);
                    message.Body = emailBody;
                    message.Attachments.Add(new Attachment(pdfStream,
                        string.Concat("МартиХигиена", document.Date.Value.Month, "/", document.Date.Value.Year, ".pdf")));
                    smtpClient.UseDefaultCredentials = false;
                    // send email
                    smtpClient.Send(message);
                    // close pdf document
                    doc.Close();
                }
        }

        //[Route("Unpayed")]
        //[Route("Неплатени")]
        //public async Task<IActionResult> Unpayed()
        //{
        //    List<DocumentViewModel> documents = new List<DocumentViewModel>();

        //    // Retrieve document entities with relevant statuses
        //    var documentEntities = await _unitOfWork.Documents.GetAllAsync(x=> x
        //        .Where(x => x.PaymentStatus == PaymentStatus.Неплатено || x.PaymentStatus == PaymentStatus.Задоцнето)
        //        .Select(c => new Entities.Document
        //        {
        //            Id = c.Id,
        //            Number = c.Number,
        //            PaymentStatus = c.PaymentStatus,
        //            ToDocument = c.ToDocument,
        //            TotalOutput = c.TotalOutput,
        //            CreatedTime = c.CreatedTime,
        //            Customer = c.Customer,
        //            DueDate = c.DueDate.Value,
        //            DateReceived = c.DateReceived,
        //        }));

        //    // Process documents with overdue status
        //    foreach (var documentEntity in documentEntities.Where(x => x.PaymentStatus == PaymentStatus.Задоцнето))
        //    {
        //        DateOnly currentDate = DateOnly.FromDateTime(DateTime.Now);
        //        int overdueDays = (currentDate.ToDateTime(TimeOnly.MinValue) - documentEntity.DateReceived.Value.ToDateTime(TimeOnly.MinValue)).Days;

        //        // Calculate the additional fee
        //        float additionalFeePercentage = GetOverdueFeePercentage(overdueDays);
        //        float additionalFee = documentEntity.TotalOutput.Value * (additionalFeePercentage / 100);

        //        // Update document total
        //        documentEntity.TotalOutput += additionalFee;
        //        _unitOfWork.Documents.Update(documentEntity);
        //    }

        //    // Save changes to the context
        //     _unitOfWork.SaveChangesAsync();

        //    documents = App.ReaderMapper.Map<List<DocumentViewModel>>(documentEntities);
        //    documents.ForEach(x => x.Company = _config.Value);

        //    return View(nameof(Index), documents);
        //}
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
        //[Route("Partially")]
        //[Route("Делумни")]
        //public async Task<IActionResult> Partially()
        //{
        //    var documentEntities = await _unitOfWork.Documents.GetAllAsync(x=>x
        //        .Where(x => x.PaymentStatus == PaymentStatus.Задоцнето)
        //        .Select(c => new Entities.Document
        //        {
        //            Id = c.Id,
        //            Number = c.Number,
        //            PaymentStatus = c.PaymentStatus,
        //            ToDocument = c.ToDocument,
        //            Customer = c.Customer,
        //            TotalOutput = c.TotalOutput,
        //            CreatedTime = c.CreatedTime,
        //            DueDate = c.DueDate.Value,
        //            DateReceived = c.DateReceived,
        //        }));

        //    var documents = App.ReaderMapper.Map<List<DocumentViewModel>>(documentEntities);
        //    documents.ForEach(x => x.Company = _config.Value);
        //    return View(nameof(Index), documents);
        //}


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

        //private async Task<List<BookFinancialViewModel>> GetInvoices(int? invoiceId, DateOnly dateFrom, DateOnly dateTo, int? buildingId)
        //{
        //    // Abfrage für BookFinancials mit Filterbedingungen und Includes
        //    var invoicesQuery = await _unitOfWork.BookFinancials.GetAllWithIncludeAsync(
        //        x => x.Include(xd => xd.Customer)
        //            .ThenInclude(xd => xd.Documents)
        //            .Include(xd => xd.Customer)
        //            .ThenInclude(xd => xd.Building)
        //            .Where(doc => doc.DatumF >= dateFrom && doc.DatumF <= dateTo &&
        //                          (!invoiceId.HasValue || doc.InvoiceId == invoiceId.Value) &&
        //                          (!buildingId.HasValue || doc.Customer.BuildingId == buildingId.Value)), // Filter by date, invoiceId, and buildingId
        //        null // Es gibt keine zusätzlichen Includes für diese Methode.
        //    );

        //    // Mapping der Ergebnisse
        //    return App.FullMapper.Map<List<BookFinancialViewModel>>(invoicesQuery);
        //}

        public async Task<IActionResult> InvoiceFiltered(int? buildingId, int? paymentStatusId, string dateFrom, string dateTo)
        {

            Buildings.Insert(0, new Building() { Name = "Сите", Id = 0 });
            if (!buildingId.HasValue)
            {
                return RedirectToAction(nameof(Index));
            }
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
                if (doc.Delay != 0)
                {
                    doc.NewTotal = CalculateNewTotal(doc);
                }
            }

            return View("Index", documents);
        }
        private int CalculateOverdueDays(DateOnly? dueDate)
        {
            var today = DateOnly.FromDateTime(DateTime.Now);
            if (dueDate != null) return (today.DayNumber - dueDate.Value.DayNumber); // Direct day difference
            return 0;
        }

        private int CalculateNewTotal(DocumentViewModel doc)
        {
            double percentage = GetOverdueFeePercentage(doc.Delay.Value);
            if (doc.TotalOutput != null)
                return (int)Math.Round(doc.TotalOutput.Value * (1 + percentage), MidpointRounding.AwayFromZero);
            return 0;
        }

        private async Task<List<Document>> GetDocuments(int? buildingId, int? paymentStatusId, string dateFrom, string dateTo)
        {
            var query = await _unitOfWork.Documents.GetAllWithIncludeAsync(
                query => query.Include(d => d.Customer).ThenInclude(c => c.Building),
                d => !buildingId.HasValue || d.Customer.BuildingId == buildingId.Value
            );
            if (paymentStatusId.HasValue)
            {
                query = query.Where(d => (int)d.PaymentStatus == paymentStatusId.Value).ToList();
            }

            if (!string.IsNullOrEmpty(dateFrom) && !string.IsNullOrEmpty(dateTo))
            {
                var startDate = DateOnly.ParseExact(dateFrom, "dd.MM.yyyy", null);
                var endDate = DateOnly.ParseExact(dateTo, "dd.MM.yyyy", null);
                query = query.Where(d => d.Date >= startDate && d.Date <= endDate).ToList();
            }

            return query;
        }
        // GET: Invoices/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }
            var documentEntity = await _unitOfWork.Documents.GetByIdAsync(xd => xd.Id == id, d => d.Include(x => x.Books).Include(d => d.Customer));
            var documentViewModel = App.FullMapper.Map<DocumentViewModel>(documentEntity);

            if (documentViewModel == null)
            {
                return NotFound();
            }

            if (documentViewModel.Customer != null)
                _unitOfWork.BookFinancials.SetOwesAndDemandsToDocument(documentViewModel.Customer.BuildingId,
                    invoiceId: 1201, status: null, documentViewModel);

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
        public async Task<IActionResult> Create(int? id, int? buildingId)
        {
            var documentViewModel = new DocumentViewModel();
            ViewBag.RouteId = id;
            documentViewModel.Date = DateOnly.FromDateTime(DateTime.UtcNow);
            documentViewModel.Company = _config.Value;

            var buildings = await _unitOfWork.Buildings.GetAllAsync(query => query.Include(x => x.BuildingProducts)
                .Include(x => x.Customers)
                .Select(b => new Building()
                {
                    Id = b.Id,
                    Name = b.Name,
                    ReserveFund = b.ReserveFund,
                    Customers = b.Customers.Where(x => x.Inactive == false).ToList(),
                    BuildingProducts = b.BuildingProducts
                }));
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
                    ? await _unitOfWork.Products.GetAllAsync()
                    : await _unitOfWork.Products.GetAllAsync(x => x.Where((x => x.ArticleNotes != null && x.ArticleNotes.Contains("влез"))));

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

        //// GET: Invoices/Create
        //public async Task<IActionResult> CreatePartially()
        //{
        //    var documentViewModel = new DocumentViewModel();
        //    documentViewModel.Company = _config.Value;
        //    var buildings = await _unitOfWork.Buildings.GetAllAsync(x => x
        //        .Include(b => b.BuildingProducts)
        //        .Select(b => new Building()
        //        {
        //            Id = b.Id,
        //            Name = b.Name,
        //            BuildingProducts =
        //                (ICollection<BuildingProduct>)b.BuildingProducts.Where(x =>
        //                    x.ArticleNotes == "Чистење на влез за")
        //        }));
        //    HttpContext.Session.Remove("Documents");

        //    documentViewModel.Buildings = App.FullMapper.Map<List<BuildingViewModel>>(buildings);
        //    documentViewModel.Building = documentViewModel.Buildings.FirstOrDefault();
        //    return View(nameof(Create), documentViewModel);
        //}



        // POST: Invoices/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(DocumentViewModel document,bool send)
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
                /// da se napravie site drugo so ne spagaat u normalni BuildingProducts, da se knizat na trosok na 1201 i sumata na Owes
                var buildingProductsFromReserve =
                    document.Building?.BuildingProducts.Where(x => x.GetFromReserve).ToList();
                if (buildingProductsFromReserve != null && buildingProductsFromReserve.Any())
                {
                    foreach (var invoice in buildingProductsFromReserve)
                    {
                        var bookFinancial = new BookFinancialViewModel
                        {
                            DatumF = document.Date,
                            InvoiceId = (int)InvoiceTyp.Reserve,
                            Demands = 0,
                            Owes = (double)invoice.Total,
                            CustomerId = document.Building?.Id,
                            DocumentTypId = 5,
                            Description = invoice.ArticleNotes,
                            Status = PaymentStatus.Неплатено
                        };
                        _unitOfWork.BookFinancials.Add(App.FullMapper.Map<BookFinancial>(bookFinancial));
                    }
                }

                var building = await _unitOfWork.Buildings.GetByIdAsync(x => x.Id == document.Building.Id,
                    inc => inc.Include(x => x.Customers));
                if (building != null)
                {
                    foreach (var customer in building.Customers.Where(x => x.Inactive == false).ToList())
                    {
                        var docEntity = await CreateCustomerDocument(customer.Id, document, building);

                        if (document?.Building?.BuildingProducts != null)
                            foreach (var buildingProduct in document.Building.BuildingProducts.Where(x => x.PriceWithTax != 0))
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

                        if (send)
                        {
                           await CreateAndSend(App.FullMapper.Map<DocumentViewModel>(docEntity));
                        }
                        //CreateBookFinancialAndReserve(docEntity, customer.Id, building.ReserveFund ?? 0, document.PaymentDate, document.PaymentType, document.PaymentNumber);
                    }
                }

                CreateSpecialInvoice(document);
                await _unitOfWork.SaveChangesAsync();
                HttpContext.Session.Remove("Documents");

                return RedirectToAction(nameof(Create), new { id = 0, buildingId = document.Building.Id });
            }
            //ViewData["ResidentId"] = new SelectList(_context.Customers, "Id", "CustomerInfo", document.CustomerId);
            return View(document);
        }

        private void CreateSpecialInvoice(DocumentViewModel document)
        {
            int sum = 0;
            var energyValues = document.Building?.BuildingProducts
                .Where(x => x.ArticleNotes != null && x.Total != 0 && (x.ArticleNotes.ToLower().Contains("енер.")
                                                                        || x.ArticleNotes.ToLower().Contains("осветлување")))
                .ToList();
            if (energyValues != null && energyValues.Any())
            {
                sum = (int)Math.Round((decimal)energyValues.Sum(x => x.PriceWithTax), MidpointRounding.AwayFromZero);
            }
            if (sum == 0)
            {
                return;
            }

            var specialInvoiceViewModel = new SpecialInvoiceViewModel
            {
                ForDate = document.Date ?? DateOnly.MinValue.AddMonths(-1),
                Total = sum,
                InvoiceId = Constants.Energy,
                BuildingId = document.Building.Id,
                Status = PaymentStatus.Неплатено
            };
            specialInvoiceViewModel.Building = null;
            var specialInvoice = App.FullMapper.Map<SpecialInvoice>(specialInvoiceViewModel);
            _unitOfWork.SpecialInvoices.UpdateSpecialInvoices(document, specialInvoice);
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
            _unitOfWork.BookFinancials.Add(bookFinancial);
            _unitOfWork.BookFinancials.Add(bookFinancialReserve);
        }

        private void CreateBook(BuildingProductViewModel book, Document docEntity)
        {

            var bookEntity = new BookViewModel
            {
                DocId = docEntity.Id,
                Output = 1,
                Quantity = book.Quantity,
                PriceWithTax = book.PriceWithTax,
                Tax = book.Tax,
                ArticleId = book.Id,
                Total = book.PriceWithTax,
                ArticleNotes = book.ArticleNotes,
                UnitOfMeasurement = book.UnitOfMeasurement,
            };
            var entityBook = App.FullMapper.Map<Book>(bookEntity);
            _unitOfWork.Books.Add(entityBook);
        }

        private async Task<Document> CreateCustomerDocument(int customerId, DocumentViewModel document, Building building)
        {
            var documentCustomer = new DocumentViewModel();
            documentCustomer.CustomerId = customerId;
            documentCustomer.Number =
                (await _unitOfWork.Documents.GetMaxAsync(x => x.Number) ?? 0) + 1; documentCustomer.Date = document.Date;
            if (document.Date != null)
                documentCustomer.ToDocument = DocumentService.GetMonthAsString(document.Date.Value.Month) + " " +
                                              document.Date.Value.Year;
            documentCustomer.Description = building.Name;
            documentCustomer.Date = DateOnly.FromDateTime(DateTime.UtcNow);
            documentCustomer.CreatedTime = DateTime.UtcNow;
            documentCustomer.DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(15));
            documentCustomer.TotalInput = 0;
            var calculator = new PriceCalculator(building.Customers.Where(x => x.Inactive == false).Count());

            // Calculate prices for each product
            if (document?.Building?.BuildingProducts != null)
            {
                calculator.CalculatePrices(document.Building.BuildingProducts);
            }
            // Calculate the total PriceWithTax sum
            if (document?.Building?.BuildingProducts != null)
            {
                float totalPriceWithTax = calculator.CalculateTotalPriceWithTaxSum(document?.Building?.BuildingProducts);
                documentCustomer.TotalOutput = totalPriceWithTax;
            }

            var customer = await _unitOfWork.Customers.GetByIdAsync(x => x.Id == customerId);
            if (customer != null && customer.Subscription.HasValue && customer.Subscription != 0 && customer.Subscription >= documentCustomer.TotalOutput)
            {
                customer.Subscription = (int?)(customer.Subscription - documentCustomer.TotalOutput.Value);
                documentCustomer.PaymentStatus = PaymentStatus.Платено;
                _unitOfWork.Customers.Update(customer);
            }
            else
            {
                documentCustomer.PaymentStatus = PaymentStatus.Неплатено;
            }
            var docEntity = App.FullMapper.Map<Document>(documentCustomer);

            _unitOfWork.Documents.Add(docEntity);
            await _unitOfWork.SaveChangesAsync();
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
            var bookfinancialToUpdate = await _unitOfWork.BookFinancials.GetAllAsync(wh => wh.Where(x => x.DocumentId == model.Id));
            try
            {
                var documentToUpdate = await _unitOfWork.Documents.GetByIdAsync(x => x.Id == model.Id);
                if (documentToUpdate == null)
                {
                    return NotFound();
                }
                documentToUpdate.PaymentStatus = PaymentStatus.Платено;
                documentToUpdate.PaymentDate = model.PaymentDate;
                documentToUpdate.PaymentType = model.PaymentType;
                documentToUpdate.PaymentNumber = model.PaymentNumber;

                // Update related BookFinancials
                if (bookfinancialToUpdate == null) throw new ArgumentNullException(nameof(bookfinancialToUpdate));
                if (bookfinancialToUpdate.Any())
                {
                    foreach (var item in bookfinancialToUpdate)
                    {
                        if (model.PaymentDate != null) item.PaymentDate = model.PaymentDate.Value;
                        item.PaymentType = model.PaymentType;
                        item.PaymentNumber = model.PaymentNumber;
                        item.Status = PaymentStatus.Платено;
                    }
                    _unitOfWork.BookFinancials.UpdateRange(bookfinancialToUpdate);
                }
                _unitOfWork.Documents.Update(documentToUpdate);
                await _unitOfWork.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (_unitOfWork.Documents.GetAllAsync().Result.All(x => x.Id != model.Id))
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

            var documentEntity = await _unitOfWork.Documents.GetByIdAsync(x => x.Id == id.Value, d => d
                .Include(x => x.Books)
                .Include(d => d.Customer));

            if (documentEntity == null)
            {
                return NotFound();
            }

            var document = App.FullMapper.Map<DocumentViewModel>(documentEntity);
            document.Company = _config.Value;

            ViewData["CustomerId"] = new SelectList(await _unitOfWork.Customers.GetAllAsync(), "Id", "Name", document.CustomerId);
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
                    _unitOfWork.Documents.Update(documentEntity);
                    await _unitOfWork.SaveChangesAsync();
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

            ViewData["CustomerId"] = new SelectList(await _unitOfWork.Customers.GetAllAsync(), "Id", "Name", document.CustomerId);
            return View(document);
        }

        // GET: Invoices/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var invoice = await _unitOfWork.Documents.GetByIdAsync(x => x.Id == id.Value, x => x
                .Include(i => i.Books));

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
            var invoice = await _unitOfWork.Documents.GetByIdAsync(x => x.Id == id);
            if (invoice != null)
            {
                _unitOfWork.Documents.Delete(invoice);
            }

            await _unitOfWork.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool InvoiceExists(int id)
        {
            return _unitOfWork.Documents.GetAllAsync().Result.Any(e => e.Id == id);
        }

        public async Task<IActionResult> CombineAndDownloadPdfs(int? buildingId, int? paymentStatusId, string dateFrom, string dateTo)
        {
            // Hole die Kunden, die dem Gebäude zugeordnet sind
            var customers = await _unitOfWork.Customers.GetCustomersByBuildingIdAsync(buildingId.Value);

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
            foreach (var item in customers.Where(x=>x.Inactive == false))
            {
                var documentEntity = await  _unitOfWork.Documents.GetByIdAsync(da =>
                    da.CustomerId == item.Id && da.Date!.Value.Year == startDate.Year &&
                    da.Date!.Value.Month == endDate.Month, d => d
                    .Include(x => x.Customer)
                    .Include(x => x.Books));
                var document = App.FullMapper.Map<DocumentViewModel>(documentEntity);


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
            fileResult.FileDownloadName = $"{Buildings.FirstOrDefault(x => x.Id == buildingId.Value)?.Name}_{startDate.Month}_{startDate.Year}.pdf";
            return fileResult;
        }
    }
}
