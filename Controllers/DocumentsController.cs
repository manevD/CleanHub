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
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;

namespace CleanHub.Controllers
{
    [RequireLogin]
    public class DocumentsController(IOptions<CompanyConfig> _config, IUnitOfWork _unitOfWork, ICompositeViewEngine _viewEngine, IOptions<SMTPConfig> _smtpConfig, IHttpContextAccessor _httpContextAccessor) : Controller
    {
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

        private int GetOverdueFeePercentage(int overdueDays)
        {
            if (overdueDays == 0)
                return 0;
            if (overdueDays < 30)
                return 2;
            else if (overdueDays >= 31 && overdueDays <= 60)
                return 4;
            else if (overdueDays >= 61 && overdueDays <= 90)
                return 6;
            else if (overdueDays >= 91 && overdueDays <= 180)
                return 8;
            else if (overdueDays >= 181 && overdueDays <= 360)
                return 10;
            else if (overdueDays >= 361 && overdueDays <= 730)
                return 13;
            else
                return 16;
        }


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


        [HttpGet]
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
            foreach (var doc in documents.Where(x => x.PaymentStatus != (int)PaymentStatus.Платено))
            {
                doc.Delay = CalculateOverdueDays(doc.DateReceived);
                if (doc.Delay != 0)
                {
                    doc.NewTotal = (int?)(doc.TotalOutput + CalculateNewTotal(doc));
                }
            }

            return View("Index", documents);
        }


        private int CalculateOverdueDays(DateOnly? dateReceived)
        {
            var today = DateOnly.FromDateTime(DateTime.Now);
            if (dateReceived != null)
            {
                if (dateReceived >= today)
                {
                    return 0;
                }
                return today.DayNumber - dateReceived.Value.DayNumber;
            }

            return 0;
        }

        private int CalculateNewTotal(DocumentViewModel doc)
        {
            doc.ChargesInPercent = GetOverdueFeePercentage(doc.Delay.Value);
            if (doc.TotalOutput != null)
                return (int)Math.Round(doc.TotalOutput.Value * (doc.ChargesInPercent.Value / 100f), MidpointRounding.AwayFromZero);
            return 0;
        }

        private async Task<List<Document>> GetDocuments(int? buildingId, int? paymentStatusId, string dateFrom, string dateTo)
        {
            var query = await _unitOfWork.Documents.GetAllWithIncludeAsync(
                query => query.Include(d => d.Customer).ThenInclude(c => c.Building),
                d =>
                    (buildingId.GetValueOrDefault() == 0 || d.Customer.BuildingId == buildingId.Value) &&
                    (paymentStatusId == (int)PaymentStatus.Сите || (int)d.PaymentStatus == paymentStatusId)
            );

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
                    invoiceId: (int)InvoiceTyp.Reserve, status: null, documentViewModel);
            documentViewModel.Delay = CalculateOverdueDays(documentViewModel.DateReceived);
            if (documentViewModel.PaymentStatus == PaymentStatus.Платено)
            {
                documentViewModel.NewTotal = (int?)documentViewModel.TotalOutput;
            }
            else
            {
                documentViewModel.NewTotal = (int?)(documentViewModel.TotalOutput + CalculateNewTotal(documentViewModel));
            }
            documentViewModel.Company = _config.Value;
            if (documentViewModel.Books != null && documentViewModel.Books.Any(x => x.Hide))
            {
                foreach (var book in documentViewModel.Books.Where(x => x.Hide))
                {
                    var sb = new StringBuilder();
                    if (!string.IsNullOrWhiteSpace(documentViewModel.Company?.InvoiceNotice))
                        sb.AppendLine(documentViewModel.Company.InvoiceNotice);

                    if (!string.IsNullOrWhiteSpace(book.ArticleNotes))
                        sb.AppendLine(book.ArticleNotes);
                }

                documentViewModel.Books = documentViewModel.Books.Where(x => !x.Hide).ToList();
            }
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
            _unitOfWork.BookFinancials.SetOwesAndDemandsToDocument(buildingId.HasValue ? buildingId.Value : 1, invoiceId: (int)InvoiceTyp.Reserve, status: null, documentViewModel);
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
                    : await _unitOfWork.Products.GetAllAsync(x => x.Where(x => x.ArticleNotes != null && x.ArticleNotes.Contains("влез")));

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
        public async Task<IActionResult> Create(DocumentViewModel document, bool send)
        {
            if (ModelState.IsValid)
            {
                var buildingProductsFromBuilding = _unitOfWork.Buildings.GetAllBuildingProducts(document.Building.Id).ToList();
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

                if (buildingProductsFromBuilding != null && buildingProductsFromBuilding.Any())
                {
                    if (buildingProductsFromBuilding.Count != document.Building?.BuildingProducts.Count)
                    {
                        var existingNotes = buildingProductsFromBuilding
                            .Select(bp => bp.ArticleNotes?.Trim())
                            .Where(note => !string.IsNullOrEmpty(note))
                            .ToList();

                        var productsToAdd = document.Building?.BuildingProducts
                            .Where(x =>
                                !string.IsNullOrWhiteSpace(x.ArticleNotes) &&
                                !existingNotes.Any(existing =>
                                    x.ArticleNotes.Trim().StartsWith(existing, StringComparison.OrdinalIgnoreCase)))
                            .ToList();
                        foreach (var product in productsToAdd)
                        {
                            var bookFinancialViewModelReserve = new BookFinancialViewModel
                            {
                                InvoiceId = Constants.Reserve,
                                Demands = 0,
                                DocumentTypId = 5,
                                Owes = product.PriceWithTax ?? 0,
                                DatumF = DateOnly.FromDateTime(document.Date.Value.ToDateTime(TimeOnly.MinValue).AddDays(10)),
                                CustomerId = document.Building?.Id,
                                Status = PaymentStatus.Неплатено,
                                Time = DateTime.Now,
                                Description = product.ArticleNotes,
                            };
                            var bookFinancialReserve = App.FullMapper.Map<BookFinancial>(bookFinancialViewModelReserve);
                            _unitOfWork.BookFinancials.Add(bookFinancialReserve);
                            document.Building.BuildingProducts.Remove(product);
                        }
                    }
                }

                var building = await _unitOfWork.Buildings.GetByIdAsync(x => x.Id == document.Building.Id,
                    inc => inc.Include(x => x.Customers));
                var documents = new List<Document>();

                if (building != null)
                {
                    foreach (var customer in building.Customers.Where(x => x.Inactive == false).ToList())
                    {
                        var docEntity = await CreateCustomerDocument(customer, document, building);

                        if (document?.Building?.BuildingProducts != null)
                            foreach (var buildingProduct in document.Building.BuildingProducts.Where(x => x.PriceWithTax != 0))
                            {
                                try
                                {
                                    // Continue creating the book if the article exists
                                    if (buildingProduct.ArticleNotes.Contains("гаража") && !customer.Garage)
                                    {
                                        continue;
                                    }
                                    CreateBook(buildingProduct, docEntity);
                                }

                                catch (Exception e)
                                {
                                    Console.WriteLine(e);
                                    throw;
                                }
                            }
                        CreateBookFinancialAndReserve(docEntity, customer.Id, building.ReserveFund ?? 0, document.PaymentDate, document.PaymentType, document.PaymentNumber);
                        documents.Add(docEntity);
                    }
                }

                CreateSpecialInvoice(document);
                await _unitOfWork.SaveChangesAsync();
                HttpContext.Session.Remove("Documents");
                
                return await PrintDocuments(documents, building,send);
            }

            return View(document);
            //ViewData["ResidentId"] = new SelectList(_context.Customers, "Id", "CustomerInfo", document.CustomerId);
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
                sum = (int)Math.Round((decimal)energyValues.Sum(x => x.Total), MidpointRounding.AwayFromZero);
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
                Demands = 0,
                Owes = docEntity!.TotalOutput!.Value!,
                DocumentTypId = 4,
                CustomerId = customerId,
                Time = DateTime.Now,
                Status = PaymentStatus.Неплатено,
                DatumF = docEntity.DateReceived,
                Description = string.Empty,
            };

            CreateReserve(docEntity, customerId, reserve, paymentDate, paymentType, paymentNumber);
            var bookFinancial = App.FullMapper.Map<BookFinancial>(bookFinancialViewModel);
            _unitOfWork.BookFinancials.Add(bookFinancial);
        }

        public void CreateReserve(Document docEntity, int customerId, int reserve, DateOnly? paymentDate,
            PaymentType paymentType, string? paymentNumber)
        {
            var bookFinancialViewModelReserve = new BookFinancialViewModel
            {
                InvoiceId = Constants.Reserve,
                DocumentId = docEntity.Id,
                Demands = 0,
                DocumentTypId = 4,
                Owes = docEntity.Books.FirstOrDefault(x => x.ArticleNotes.Contains("Резервен фонд"))?.Total ?? 0.0,
                DatumF = docEntity.DateReceived,
                CustomerId = customerId,
                Status = PaymentStatus.Неплатено,
                Time = DateTime.Now,
                Description = string.Empty,
            };
            var bookFinancialReserve = App.FullMapper.Map<BookFinancial>(bookFinancialViewModelReserve);
            _unitOfWork.BookFinancials.Add(bookFinancialReserve);
        }

        private void CreateBook(BuildingProductViewModel book, Document docEntity)
        {
            var bookEntity = new BookViewModel
            {
                DocId = docEntity.Id,
                Output = 1,
                Input = 0,
                Hide = book.Hide,
                Quantity = book.Quantity,
                PriceWithTax = book.PriceWithTax,
                Tax = book.Tax,
                Total = book.PriceWithTax,
                ArticleNotes = book.ArticleNotes,
                UnitOfMeasurement = book.UnitOfMeasurement,
            };
            var entityBook = App.FullMapper.Map<Book>(bookEntity);
            docEntity.Books.Add(entityBook);
            if (!bookEntity.Hide)
            {
                _unitOfWork.Books.Add(entityBook);
            }
        }

        private async Task<Document> CreateCustomerDocument(Customer customer, DocumentViewModel document, Building building)
        {
            var documentCustomer = new DocumentViewModel();
            documentCustomer.CustomerId = customer.Id;
            documentCustomer.Number =
                (await _unitOfWork.Documents.GetMaxAsync(x => x.Number) ?? 0) + 1; documentCustomer.Date = document.Date;
            if (document.Date != null)
                documentCustomer.ToDocument = DocumentService.GetMonthAsString(document.Date.Value.Month) + " " +
                                              document.Date.Value.Year;
            documentCustomer.Description = building.Name;
            documentCustomer.Date = document.Date;
            documentCustomer.CreatedTime = DateTime.UtcNow;
            if (document.Date != null)
                documentCustomer.DateReceived =
                    DateOnly.FromDateTime(document.Date.Value.ToDateTime(TimeOnly.MinValue).AddDays(10));
            documentCustomer.TotalInput = 0;
            var calculator = new PriceCalculator(building.Customers.Count(x => x.Inactive != null && !x.Inactive.Value), building.Customers.Count(x => x.Garage));

            var tempBuildingProducts = document.Building?.BuildingProducts.ToList();
            if (!customer.Garage)
            {
                if (tempBuildingProducts != null)
                    tempBuildingProducts = tempBuildingProducts
                        .Where(x => !x.ArticleNotes!.Contains("гаража"))
                        .ToList();
            }

            calculator.CalculatePrices(tempBuildingProducts, customer);
            // Calculate the total PriceWithTax sum
            if (tempBuildingProducts != null)
            {
                float totalPriceWithTax = calculator.CalculateTotalPriceWithTaxSum(tempBuildingProducts);
                documentCustomer.TotalOutput = totalPriceWithTax;
            }
            if (customer.Subscription.HasValue && customer.Subscription != 0 && customer.Subscription >= documentCustomer.TotalOutput)
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
                if (model.NewTotal.HasValue && model.NewTotal != 0)
                {
                    documentToUpdate.NewTotal = model.NewTotal.Value;
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
                        item.Description = model.Description;
                        item.Demands = item.Owes;
                        item.Owes = 0;
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
                .Include(x => x.Books.Where(x => !x.Hide))
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
            (owes, demands) = _unitOfWork.BookFinancials.GetBuildingReserve(buildingId.Value, invoiceId: (int)InvoiceTyp.Reserve, status: null);

            PdfDocument endDoc = new PdfDocument();  // Initialize the final document to append pages to.
            MemoryStream pdfStream = new MemoryStream();
            foreach (var item in customers.Where(x => x.Inactive == false))
            {
                var documentEntity = await _unitOfWork.Documents.GetByIdAsync(
                    da => da.CustomerId.HasValue && da.CustomerId == item.Id &&
                          da.Date.HasValue && da.Date.Value >= startDate &&
                          da.Date.Value <= endDate,
                    d => d.Include(x => x.Customer).Include(x => x.Books)
                );



                var document = App.FullMapper.Map<DocumentViewModel>(documentEntity);

                if (document == null)
                {
                    continue;
                }
                document.TotalBuildingDemands = demands;
                document.TotalBuildingOwes = owes;
                document.Company = _config.Value;
                document.IsForPdf = true;
                if (document.Books != null && document.Books.Any(x => x.Hide))
                {
                    foreach (var book in document.Books.Where(x => x.Hide))
                    {
                        var sb = new StringBuilder();
                        if (!string.IsNullOrWhiteSpace(document.Company?.InvoiceNotice))
                            sb.AppendLine(document.Company.InvoiceNotice);

                        if (!string.IsNullOrWhiteSpace(book.ArticleNotes))
                            sb.AppendLine(book.ArticleNotes);
                    }

                    document.Books = document.Books.Where(x => !x.Hide).ToList();
                }
                string htmlContent = await RenderPartialViewToStringAsync("~/Views/Shared/_DocumentDetailPartialPrint.cshtml", document);

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

        public async Task<IActionResult> PrintDocuments(List<Document> documents, Building building,bool send)
        {
            if (documents != null && documents.Any())
            {
                var owes = 0;
                var demands = 0;
                (owes, demands) = _unitOfWork.BookFinancials.GetBuildingReserve(building.Id, invoiceId: (int)InvoiceTyp.Reserve, status: null);
                var total = owes - demands;
                PdfDocument endDoc = new PdfDocument(); 
                MemoryStream pdfStream = new MemoryStream();
                foreach (var item in documents)
                {
                    var document = App.FullMapper.Map<DocumentViewModel>(item);

                    if (document == null)
                    {
                        continue;
                    }
                    document.TotalBuildingDemands = demands;
                    document.TotalBuildingOwes = owes;
                    document.Company = _config.Value;
                    document.IsForPdf = true;
                    if (document.Books != null && document.Books.Any(x => x.Hide))
                    {
                        var sb = new StringBuilder();
                        foreach (var book in document.Books.Where(x => x.Hide))
                        {
                            if (!string.IsNullOrWhiteSpace(document.Company?.InvoiceNotice))
                                sb.AppendLine(document.Company.InvoiceNotice);

                            if (!string.IsNullOrWhiteSpace(book.ArticleNotes))
                                sb.AppendLine($"за {document.ToDocument} трошок за {book.ArticleNotes} имате {book.Total} мкд");
                            sb.AppendLine(Environment.NewLine);
                        }
                        document.Company.InvoiceNotice = sb.ToString();
                        //Check if work?
                        if (send)
                        {
                            await CreateAndSend(App.FullMapper.Map<DocumentViewModel>(document));
                        }
                        document.Books = document.Books.Where(x => !x.Hide).ToList();
                    }
                    string htmlContent = await RenderPartialViewToStringAsync("~/Views/Shared/_DocumentDetailPartialPrint.cshtml", document);

                    var request = _httpContextAccessor?.HttpContext?.Request;
                    string baseUrl = $"{request?.Scheme}://{request?.Host.Value}/";

                    HtmlToPdf converter = new HtmlToPdf();
                    PdfDocument doc = converter.ConvertHtmlString(htmlContent, baseUrl);
                    endDoc.Append(doc);
                    
                }

                byte[] pdf = endDoc.Save();

                endDoc.Close();

                FileResult fileResult = new FileContentResult(pdf, "application/pdf");
                var dateOnly = documents.FirstOrDefault().Date;
                if (dateOnly != null)
                    fileResult.FileDownloadName =
                        $"{building?.Name}_{dateOnly.Value.Month}_{dateOnly.Value.Year}.pdf";
                return fileResult;
            }

            return null;
        }
    }
}