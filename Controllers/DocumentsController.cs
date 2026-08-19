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
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;
using PaymentStatus = CleanHub.Entities.Enums.PaymentStatus;
using SpecialInvoice = CleanHub.Entities.SpecialInvoice;

namespace CleanHub.Controllers
{
    [RequireLogin]
    public class DocumentsController(IOptions<CompanyConfig> _config, IUnitOfWork _unitOfWork, ICompositeViewEngine _viewEngine, IOptions<SMTPConfig> _smtpConfig, IHttpContextAccessor _httpContextAccessor, ApplicationDbMartiContext _context) : Controller
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

        private async void SendNotificationMail(Customer customer, string description)
        {
            using (SmtpClient smtpClient = new SmtpClient(_smtpConfig.Value.Server))
            {
                smtpClient.Credentials = new NetworkCredential(_smtpConfig.Value.Email, _smtpConfig.Value.Passwort);
                smtpClient.EnableSsl = true;
                string emailBody =
                    @$"Почитувани,
                            Сакаме да ве известиме дека за сметката {description} немате доволно средства да се покрие од вашата претплата.
                            Во случај на било какви прашања или недоразбирања можете слободно да не контактирате. Ви благодариме за Вашето внимание и соработка.
                            Со почит,
                            {_config.Value.Name}
                            {_config.Value.PhoneNumber}
                            {_config.Value.Email}
                            {_config.Value.Address}";
                MailMessage message = new MailMessage();
                message.From = new MailAddress(_smtpConfig.Value.Email);
                message.To.Add(customer.Email);
                message.Subject = "Известување Марти Хигиена";
                message.Body = emailBody;
                smtpClient.UseDefaultCredentials = false;
                await smtpClient.SendMailAsync(message);
            }
        }

        [HttpPost]
        public async Task<IActionResult> Pay(int invoiceId, PaymentType PaymentType, DateTime PaymentDate, string PaymentNumber, string PaymentDescription)
        {
            var document = await _unitOfWork.Documents.Query()
                .FirstOrDefaultAsync(x => x.Id == invoiceId);

            if (document == null)
            {
                return BadRequest("Сметката не е пронајдена.");
            }

            document.PaymentStatus = PaymentStatus.Платено;
            document.PaymentDate = DateOnly.FromDateTime(PaymentDate);
            document.PaymentDescription = PaymentDescription;
            document.PaymentType = PaymentType;
            document.PaymentNumber = PaymentNumber;

            var viewModel = App.FullMapper.Map<DocumentViewModel>(document);
            SetStatusPayment(viewModel);

            await _unitOfWork.SaveChangesAsync();

            return Ok(new { success = true });
        }

        [HttpGet]
        public async Task<IActionResult> GetUnpaidInvoices(int customerId)
        {
            var unpaidInvoices = await _unitOfWork.Documents.Query()
                .Where(x => x.CustomerId == customerId && x.PaymentStatus == PaymentStatus.Неплатено)
                .Select(x => new {
                    id = x.Id,
                    documentText = x.ToDocument + " (" + x.TotalOutput + " МКД)",
                })
                .ToListAsync();

            return Json(unpaidInvoices);
        }

        public async Task CreateAndSend(DocumentViewModel document)
        {
            using (SmtpClient smtpClient = new SmtpClient(_smtpConfig.Value.Server))
            {
                smtpClient.Credentials = new NetworkCredential(_smtpConfig.Value.Email, _smtpConfig.Value.Passwort);
                smtpClient.EnableSsl = true;

                document.IsForPdf = true;

                string htmlContent =
                    await RenderPartialViewToStringAsync("~/Views/Shared/_DocumentDetailPartial.cshtml", document);
                var request = _httpContextAccessor?.HttpContext?.Request;
                string baseUrl = $"{request?.Scheme}://{request?.Host.Value}/";
                HtmlToPdf converter = new HtmlToPdf();
                PdfDocument doc = converter.ConvertHtmlString(htmlContent, baseUrl);
                MemoryStream pdfStream = new MemoryStream();

                doc.Save(pdfStream);
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
                MailMessage message = new MailMessage();
                message.From = new MailAddress(_smtpConfig.Value.Email);
                message.To.Add(document?.Customer?.Email);
                message.Subject = string.Concat("Сметка Марти Хигиена ", document.Customer?.CustomerInfo, " за ", document.Date.Value.Month, "/",
                    document.Date.Value.Year);
                message.Body = emailBody;
                message.Attachments.Add(new Attachment(pdfStream,
                    string.Concat("МартиХигиена", document.Date.Value.Month, "/", document.Date.Value.Year, ".pdf")));
                smtpClient.UseDefaultCredentials = false;
                await smtpClient.SendMailAsync(message);
                doc.Close();
            }
        }

        private int GetOverdueFeePercentage(int overdueDays)
        {
            if (overdueDays == 0) return 0;
            if (overdueDays < 30) return 2;
            else if (overdueDays >= 31 && overdueDays <= 60) return 4;
            else if (overdueDays >= 61 && overdueDays <= 90) return 6;
            else if (overdueDays >= 91 && overdueDays <= 180) return 8;
            else if (overdueDays >= 181 && overdueDays <= 360) return 10;
            else if (overdueDays >= 361 && overdueDays <= 730) return 13;
            else return 16;
        }

        [Route("Сметки")]
        public IActionResult Index(bool? fromPaymentStatus)
        {
            if (!fromPaymentStatus.HasValue || !fromPaymentStatus.Value)
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

                Buildings.Insert(0, new Building { Name = "Сите", Id = 0 });
                ViewBag.Buildings = new SelectList(Buildings, "Id", "Name");
            }
            else
            {
                var selectedId = TempData["SelectedBuildingId"] as int?;
                var selectedName = TempData["SelectedBuildingName"] as string;

                ViewBag.Buildings = new SelectList(Buildings, "Id", "Name", selectedId);
                ViewBag.SelectedBuildingName = selectedName;
                ViewBag.BuildingId = selectedId;

                ViewBag.PaymentStatusList = Enum.GetValues(typeof(PaymentStatus))
              .Cast<PaymentStatus>()
              .Select(e => new SelectListItem
              {
                  Text = e.GetEnumDescription(),
                  Value = ((int)e).ToString(),
                  Selected = (int)e == (int)Entities.Enums.PaymentStatus.Неплатено
              })
              .ToList();
            }
            return View("Index");
        }

        [HttpGet]
        public async Task<IActionResult> InvoiceFiltered(int? buildingId, int? paymentStatusId, int? year)
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

            var building = await _unitOfWork.Buildings.GetByIdAsync(x => x.Id == buildingId.Value, inc => inc.Include(c => c.Customers));
            if (building != null)
            {
                ViewBag.Buildings = new SelectList(Buildings, "Id", "Name", building.Id);
                ViewBag.SelectedBuildingName = building.Name;
                ViewBag.BuildingId = building.Id;
                ViewBag.Customers = building.Customers.ToList();

                var bookFinancials = _unitOfWork.BookFinancials
                    .GetAllNoTrakcing(query => query
                        .Include(bf => bf.Customer)
                        .Where(bf =>
                            bf.Customer.BuildingId == buildingId.Value &&
                            (bf.InvoiceId == (int)InvoiceTyp.Recieve || bf.DocumentTypId == 11) &&
                            bf.InvoiceId != 1201
                        )
                    )
                    .ToList();

                var dataDocument = _unitOfWork.Documents.GetAllNoTrakcing(query => query
                            .Include(bf => bf.Customer)
                            .Where(bf => bf.Customer.BuildingId == buildingId.Value)).ToList();
                ViewBag.Documents = dataDocument;
                ViewBag.BookFinancials = bookFinancials;

                var customerBalances = building.Customers.Select(c =>
                {
                    var customerDocs = dataDocument.Where(x => x.CustomerId == c.Id).ToList();
                    var dataBookFinancial = bookFinancials.Where(x => x.CustomerId == c.Id).ToList();

                    var pobaruva = dataBookFinancial.Sum(x => x.Demands);

                    double dolzi = customerDocs
                        .Where(x => x.Date.HasValue && x.Date.Value > new DateOnly(2021, 1, 1))
                        .Sum(x => (double)(x.TotalOutput ?? 0));

                    if (dataBookFinancial.Any(x => x.Owes != 0 && x.DatumF.HasValue && x.DatumF.Value >= new DateOnly(2021, 1, 1)))
                    {
                        dolzi += dataBookFinancial
                            .Where(x => x.Owes != 0 && x.DatumF.HasValue && x.DatumF.Value >= new DateOnly(2021, 1, 1))
                            .Sum(x => x.Owes);
                    }

                    var saldo = pobaruva - dolzi;

                    return new
                    {
                        Customer = c,
                        Pobaruva = pobaruva,
                        Dolzi = dolzi,
                        Saldo = saldo
                    };

                }).ToList();

                ViewBag.CustomerBalances = customerBalances;
            }

            int selectedYear = year ?? DateTime.Now.Year;
            ViewBag.Year = selectedYear;

            var documentEntities = await GetDocumentsByYear(buildingId, paymentStatusId, selectedYear);

            var documents = App.FullMapper.Map<List<DocumentViewModel>>(documentEntities);
            if (paymentStatusId == (int)PaymentStatus.Неплатено)
            {
                ViewBag.Docs = App.FullMapper.Map<List<DocumentViewModel>>(await GetDocumentsByYear(buildingId, (int)PaymentStatus.Платено, selectedYear));
            }
            else
            {
                ViewBag.Docs = documents;
            }
            return View("Index", documents
                .OrderBy(x => x.Date.HasValue ? x.Date.Value : DateOnly.MinValue)
                .ToList());
        }

        private async Task<List<Document>> GetDocumentsByYear(int? buildingId, int? paymentStatusId, int? year)
        {
            var query = await _unitOfWork.Documents.GetAllWithIncludeAsync(
                q => q.Include(d => d.Customer)
                      .ThenInclude(c => c.Building),

                d =>
                    (buildingId.GetValueOrDefault() == 0 || d.Customer.BuildingId == buildingId.Value) &&
                    (paymentStatusId == (int)PaymentStatus.Сите || (int)d.PaymentStatus == paymentStatusId)
            );

            if (year.HasValue)
            {
                query = query
                    .Where(d => d.Date.HasValue && d.Date.Value.Year == year.Value)
                    .ToList();
            }

            return query;
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

        public async Task<IActionResult> JustSetDocumentPayedStatus(int id, int? year, int? buildingId, int? paymentStatusId, string buildingName)
        {
            if (id == 0) return NotFound();

            var documentEntity = await _unitOfWork.Documents.GetByIdAsync(xd => xd.Id == id);
            if (documentEntity == null) return NotFound();

            documentEntity.PaymentStatus = PaymentStatus.Платено;
            _unitOfWork.Documents.Update(documentEntity);
            await _unitOfWork.SaveChangesAsync();

            return RedirectToAction("InvoiceFiltered", new
            {
                year = year,
                buildingId = buildingId,
                paymentStatusId = paymentStatusId,
                buildingName = buildingName
            });
        }

        public async Task<IActionResult> JustSetDocumentNotPayedStatus(int id, int? year, int? buildingId, int? paymentStatusId, string buildingName)
        {
            if (id == 0) return NotFound();

            var documentEntity = await _unitOfWork.Documents.GetByIdAsync(xd => xd.Id == id);
            if (documentEntity == null) return NotFound();

            documentEntity.PaymentStatus = PaymentStatus.Неплатено;
            _unitOfWork.Documents.Update(documentEntity);
            await _unitOfWork.SaveChangesAsync();

            return RedirectToAction("InvoiceFiltered", new
            {
                year = year,
                buildingId = buildingId,
                paymentStatusId = paymentStatusId,
                buildingName = buildingName
            });
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var documentEntity = await _unitOfWork.Documents.GetByIdAsync(xd => xd.Id == id, d => d.Include(x => x.Books).Include(d => d.Customer));
            var documentViewModel = App.FullMapper.Map<DocumentViewModel>(documentEntity);

            if (documentViewModel == null) return NotFound();

            documentViewModel.Company = App.FullMapper.Map<CompanyConfig>(_config.Value);
            if (documentViewModel.Books != null && documentViewModel.Books.Any(x => x.Hide))
            {
                var sb = new StringBuilder();
                if (!string.IsNullOrWhiteSpace(documentViewModel.Company?.InvoiceNotice))
                    sb.AppendLine(documentViewModel.Company.InvoiceNotice);

                foreach (var book in documentViewModel.Books.Where(x => x.Hide))
                {
                    if (!string.IsNullOrWhiteSpace(book.ArticleNotes))
                        sb.AppendLine($"<b style='color: red;'> за {documentViewModel.ToDocument} трошок за {book.ArticleNotes} имате {book.Total} мкд </b><br/>");
                }
                documentViewModel.Company.InvoiceNotice = sb.ToString();

                documentViewModel.Books = documentViewModel.Books.Where(x => !x.Hide).ToList();
            }

            var debt = _unitOfWork.Documents.GetAll().Where(x => x.CustomerId == documentViewModel.CustomerId && x.PaymentStatus != 0);
            if (debt != null && debt.Any())
            {
                var allParts = debt.OrderBy(x => x.Id).SelectMany(x => (x.ToDocument ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries)).ToList();

                var distinctExceptLast = allParts
                    .Reverse<string>()
                    .Skip(1)
                    .Reverse()
                    .Distinct()
                    .ToList();

                if (distinctExceptLast != null && distinctExceptLast.Any() && distinctExceptLast.Count() >= 8)
                {
                    documentViewModel.Debt = " : " + debt.Sum(x => x.TotalOutput ?? 0);
                }
                else
                {
                    documentViewModel.Debt = string.Join(",", distinctExceptLast) + " : " + debt.Sum(x => x.TotalOutput ?? 0);
                }
            }

            var results = GetFilteredBookFinancials(1201, documentViewModel.Customer.BuildingId).ToList();
            var demands = results.Where(x => !x.DontSum).Sum(su => su.Demands);
            var owes = results.Where(x => !x.DontSum).Sum(su => su.Owes);
            documentViewModel.TotalBuildingOwes = owes;
            documentViewModel.TotalBuildingDemands = demands;

            return PartialView("_DocumentDetailPartial", documentViewModel);
        }

        [Route("креирајФактураЗаСтанар")]
        public async Task<IActionResult> CreateForCustomer(int? customerId)
        {
            var documentViewModel = new DocumentViewModel();

            var customers = _unitOfWork.Customers.GetAll()
                .Select(x => new Customer
                {
                    Id = x.Id,
                    CustomerInfo = x.CustomerInfo
                })
                .ToList();

            var selectedCustomer = customers.FirstOrDefault(x => x.Id == customerId) ?? customers.FirstOrDefault();
            ViewBag.SelectedCustomerName = selectedCustomer?.CustomerInfo;
            documentViewModel.Company = _config.Value;

            ViewBag.Customers = new SelectList(customers, "Id", "CustomerInfo", selectedCustomer?.Id);

            if (!customerId.HasValue)
            {
                return View(documentViewModel);
            }

            var customer = _unitOfWork.Customers
                .GetAll(include: inc => inc
                    .Include(x => x.Building)
                    .ThenInclude(x => x.BuildingProducts))
                .FirstOrDefault(x => x.Id == customerId);

            if (customer == null)
            {
                return View(documentViewModel);
            }

            documentViewModel.CustomerId = customerId;

            if (customer.Building != null)
            {
                documentViewModel.Building = App.FullMapper.Map<BuildingViewModel>(customer.Building);
            }

            documentViewModel.Building ??= new BuildingViewModel();

            if (!(documentViewModel.Building.BuildingProducts?.Any() ?? false))
            {
                var products = await _unitOfWork.Products.GetAllAsync();
                documentViewModel.Building.BuildingProducts = App.FullMapper.Map<List<BuildingProductViewModel>>(products);
            }

            var reserveFund = documentViewModel.Building.ReserveFund;

            if (reserveFund != null)
            {
                foreach (var product in documentViewModel.Building.BuildingProducts.Where(p => p.ArticleNotes?.Contains("Резервен") == true))
                {
                    product.Price = reserveFund.Value;
                }
            }

            documentViewModel.BuildingId = documentViewModel.Building.Id;

            return View(documentViewModel);
        }

        [HttpPost]
        [Route("креирајФактураЗаСтанар")]
        public async Task<IActionResult> CreateForCustomer(DocumentViewModel documentViewModel, string actionType)
        {
            var customer = await _unitOfWork.Customers.GetByIdAsync(x => x.Id == documentViewModel.CustomerId);

            documentViewModel.BuildingId = customer.BuildingId;
            var buildingProductsFromBuilding = _unitOfWork.Buildings.GetAllBuildingProducts(documentViewModel.BuildingId.Value).ToList();
            var buildingProdutsToRemove = documentViewModel.Building?.BuildingProducts.Where(x => string.IsNullOrWhiteSpace(x.ArticleNotes)).ToList();

            if (buildingProdutsToRemove != null && buildingProdutsToRemove.Any())
            {
                foreach (var buildingProduct in buildingProdutsToRemove)
                {
                    documentViewModel.Building?.BuildingProducts.Remove(buildingProduct);
                }
            }

            if (buildingProductsFromBuilding != null && buildingProductsFromBuilding.Any())
            {
                if (buildingProductsFromBuilding.Count != documentViewModel.Building?.BuildingProducts.Count)
                {
                    var existingNotes = buildingProductsFromBuilding
                        .Select(bp => bp.ArticleNotes?.Trim())
                        .Where(note => !string.IsNullOrEmpty(note))
                        .ToList();

                    var productsToAdd = documentViewModel.Building?.BuildingProducts
                        .Where(x => !string.IsNullOrWhiteSpace(x.ArticleNotes) && !existingNotes.Any(existing => x.ArticleNotes.Trim().StartsWith(existing, StringComparison.OrdinalIgnoreCase)))
                        .ToList();

                    foreach (var product in productsToAdd)
                    {
                        var bookFinancialViewModelReserve = new BookFinancialViewModel
                        {
                            InvoiceId = 1201, // Заменет Constants.Reserve со 1201
                            Demands = 0,
                            DocumentTypId = 5,
                            Owes = PriceHelper.CalculatePriceWithTax(product.Price, product.Tax),
                            DatumF = DateOnly.FromDateTime(documentViewModel.Date.Value.ToDateTime(TimeOnly.MinValue).AddDays(10)),
                            CustomerId = documentViewModel.Building.CustomerRefId,
                            Status = PaymentStatus.Неплатено,
                            Time = DateTime.Now,
                            Description = product.ArticleNotes,
                        };
                        var bookFinancialReserve = App.FullMapper.Map<BookFinancial>(bookFinancialViewModelReserve);
                        _unitOfWork.BookFinancials.Add(bookFinancialReserve);
                    }
                }
            }

            var building = await _unitOfWork.Buildings.GetByIdAsync(x => x.Id == customer.BuildingId);
            var docEntity = await CreateCustomerDocument(customer, documentViewModel, building);

            if (documentViewModel?.Building?.BuildingProducts != null)
            {
                foreach (var buildingProduct in documentViewModel.Building.BuildingProducts.Where(x => x.PriceWithTax != 0))
                {
                    if (buildingProduct.ArticleNotes.Contains("гаража") && !customer.Garage)
                    {
                        continue;
                    }
                    CreateBook(buildingProduct, docEntity);
                }
            }

            var docViewModel = App.FullMapper.Map<DocumentViewModel>(docEntity);
            if (docViewModel.PaymentStatus != PaymentStatus.Платено && customer.Subscription.HasValue && customer.Subscription != 0 && customer.Subscription < docViewModel.TotalOutput && !string.IsNullOrEmpty(customer.Email))
            {
                SendNotificationMail(customer, docViewModel.ToDocument);
            }

            CreateBookFinancialAndReserve(docEntity, customer.Id, building.ReserveFund ?? 0, documentViewModel.PaymentDate, documentViewModel.PaymentType, documentViewModel.PaymentNumber);

            await _unitOfWork.SaveChangesAsync();

            if (docEntity != null)
            {
                DokumentiTest mappedDocuments = new DokumentiTest()
                {
                    Dokid = docEntity.Id.ToString(),
                    Datum = docEntity.Date.Value.ToString("yyyy-MM-dd"),
                    Broj = docEntity.Number.ToString(),
                    PartnerID = docEntity.CustomerId.ToString(),
                    Godina = docEntity.Date.Value.Year.ToString(),
                    VkupnoIz = docEntity.TotalOutput.ToString()
                };

                _context.DokumentiTest.AddRange(mappedDocuments);
                _context.SaveChanges();
            }

            HttpContext.Session.Remove("Documents");
            bool send = actionType == "send";
            return await PrintDocuments(new List<Document> { docEntity }, building, send);
        }

        [HttpGet]
        public async Task<IActionResult> CreateWithDate(int? id, int? buildingId, DateTime? date)
        {
            var formattedDate = date?.ToString("yyyy-MM-dd");
            return RedirectToAction("Create", new { id = id, buildingId = buildingId, date = formattedDate });
        }

        [HttpGet]
        public async Task<IActionResult> CreateCostForBuilding(int? buildingId, DateTime? date)
        {
            var documentViewModel = new DocumentViewModel();
            var now = DateTime.UtcNow;

            if (!date.HasValue)
            {
                var lastDayOfLastMonth = new DateTime(now.Year, now.Month, 1).AddDays(-1);
                documentViewModel.Date = DateOnly.FromDateTime(lastDayOfLastMonth);
            }
            else
            {
                documentViewModel.Date = DateOnly.FromDateTime(date.Value);
            }

            documentViewModel.Company = _config.Value;

            var buildings = (List<Building>)await _unitOfWork.Buildings.GetAllAsync(
                query => query.Select(b => new Building()
                {
                    Id = b.Id,
                    Name = b.Name,
                    CustomerRefId = b.CustomerRefId
                }));

            documentViewModel.Buildings = App.FullMapper.Map<List<BuildingViewModel>>(buildings);

            int targetBuildingId = buildingId ?? buildings.FirstOrDefault()?.Id ?? 0;
            documentViewModel.BuildingId = targetBuildingId;
            documentViewModel.Building = documentViewModel.Buildings.FirstOrDefault(x => x.Id == targetBuildingId) ?? new BuildingViewModel();

            if (documentViewModel.Building.BuildingProducts == null)
            {
                documentViewModel.Building.BuildingProducts = new List<BuildingProductViewModel>();
            }

            foreach (var product in documentViewModel.Building.BuildingProducts)
            {
                if (product.PriceWithTax == null) product.PriceWithTax = 0;
                if (product.Total == null) product.Total = 0;
                if (product.Tax == null) product.Tax = 0;
            }

            ViewBag.Buildings = new SelectList(buildings, "Id", "Name", documentViewModel.BuildingId);
            ViewBag.SelectedBuildingName = documentViewModel.Building?.Name;
            ViewBag.BuildingId = documentViewModel.BuildingId;

            if (documentViewModel.BuildingId.HasValue && documentViewModel.BuildingId.Value > 0)
            {
                var results = GetFilteredBookFinancials(1201, documentViewModel.BuildingId.Value).ToList();
                documentViewModel.TotalBuildingDemands = results.Where(x => !x.DontSum).Sum(x => x.Demands);
                documentViewModel.TotalBuildingOwes = results.Where(x => !x.DontSum).Sum(x => x.Owes);
            }

            return View(documentViewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(DocumentViewModel document, string actionType)
        {
            try
            {
                bool send = actionType == "send";
                ModelState.Remove(nameof(actionType));

                if (!ModelState.IsValid)
                    return View(document);

                var building = await _unitOfWork.Buildings.GetByIdAsync(
                    x => x.Id == document.BuildingId,
                    inc => inc.Include(x => x.Customers)
                              .Include(x => x.BuildingProducts));

                if (building == null)
                    return NotFound("Зградата не е пронајдена.");

                var toDocument = DocumentService.GetMonthAsString(document.Date.Value.Month) + " " + document.Date.Value.Year;
                    var activeCustomers = building.Customers
                .Where(x => x.Inactive == false && x.Hide == false)
                .ToList();
                var activeCustomerIds = activeCustomers.Select(x => x.Id).ToList();

                var exists = await _unitOfWork.Documents.AnyAsync(
                    x => activeCustomerIds.Contains(x.CustomerId.Value) && x.ToDocument == toDocument);

                if (exists)
                {
                    TempData["InvoiceExists"] = "За овој месец веќе постои фактура за оваа зграда.";
                    return RedirectToAction(nameof(Create), new { id = 0, buildingId = building.Id });
                }

                int currentMaxNumber = await _unitOfWork.Documents.GetMaxAsync(x => x.Number) ?? 0;

                var allDebts = await _unitOfWork.Documents.Query()
                    .Where(x => activeCustomerIds.Contains(x.CustomerId.Value) && x.PaymentStatus != PaymentStatus.Платено)
                    .ToListAsync();

                var documentsToInsert = new List<Document>();
                var bookFinancialsToInsert = new List<BookFinancial>();

                bool hasSetCost = activeCustomers.Any(x => x.SetCost);

                foreach (var customer in activeCustomers)
                {
                    currentMaxNumber++;

                    var docEntity = CreateCustomerDocumentInMemory(customer, document, building, currentMaxNumber);
                    documentsToInsert.Add(docEntity);

                    var productsForCustomer = GetProductsForCustomer(document.Building?.BuildingProducts, customer, hasSetCost);

                    foreach (var buildingProduct in productsForCustomer)
                    {
                        if (buildingProduct.ArticleNotes?.Contains("гаража") == true && !customer.Garage)
                            continue;

                       CreateBook(buildingProduct, docEntity);
                    }

                    // Креирање на финансиски записи преку BookFinancials
                    CreateBookFinancialAndReserveInMemory(
                        docEntity,
                        building.ReserveFund ?? 0,
                        document,
                        bookFinancialsToInsert);
                }

               // CreateSpecialInvoiceInMemory(document, documentsToInsert);

                // BATCH SAVE
                _unitOfWork.Documents.AddRange(documentsToInsert);
                _unitOfWork.BookFinancials.AddRange(bookFinancialsToInsert);

                await _unitOfWork.SaveChangesAsync();

                var mappedTestDocs = documentsToInsert.Select(d => new DokumentiTest
                {
                    Dokid = d.Id.ToString(),
                    Datum = d.Date.Value.ToString("yyyy-MM-dd"),
                    Broj = d.Number.ToString(),
                    PartnerID = d.CustomerId.ToString(),
                    Godina = d.Date.Value.Year.ToString(),
                    VkupnoIz = d.TotalOutput.ToString()
                }).ToList();

                _context.DokumentiTest.AddRange(mappedTestDocs);
                await _context.SaveChangesAsync();

                HttpContext.Session.Remove("Documents");
                return await ProcessPrintAndSend(documentsToInsert, building, allDebts, send);
            }
            catch (Exception ex)
            {
                return Content($"ERROR:\n\n{ex.Message}\n\n{ex.StackTrace}", "text/plain");
            }
        }

        private Document CreateCustomerDocumentInMemory(Customer customer, DocumentViewModel document, Building building, int number)
        {
            var documentCustomer = new DocumentViewModel
            {
                CustomerId = customer.Id,
                Number = number,
                Date = document.Date,
                ToDocument = DocumentService.GetMonthAsString(document.Date.Value.Month) + " " + document.Date.Value.Year,
                Description = building.Name,
                CreatedTime = DateTime.UtcNow,
                DateReceived = DateOnly.FromDateTime(document.Date.Value.ToDateTime(TimeOnly.MinValue).AddDays(10)),
                TotalInput = 0
            };

            var calculator = new PriceCalculator(building.Customers.ToList());
            var tempBuildingProducts = GetProductsForCustomer(document.Building?.BuildingProducts, customer, building.Customers.Any(x => x.SetCost));

            calculator.CalculatePrices(tempBuildingProducts, customer);

            if (tempBuildingProducts != null)
            {
                documentCustomer.TotalOutput = calculator.CalculateTotalPriceWithTaxSum(tempBuildingProducts);
            }

            var docEntity = App.FullMapper.Map<Document>(documentCustomer);

            if (customer.Subscription.HasValue && customer.Subscription != 0 && customer.Subscription >= documentCustomer.TotalOutput)
            {
                customer.Subscription -= (int)documentCustomer.TotalOutput.Value;
                docEntity.PaymentStatus = PaymentStatus.Платено;
                docEntity.PaymentType = PaymentType.Subscription;
                docEntity.Description = customer.Building.Name;
                docEntity.PaymentDate = DateOnly.FromDateTime(DateTime.UtcNow);
                _unitOfWork.Customers.Update(customer);
            }
            else
            {
                docEntity.PaymentStatus = PaymentStatus.Неплатено;
            }

            return docEntity;
        }

        private void CreateBook(BuildingProductViewModel book, Document docEntity)
        {
            var bookEntity = new BookViewModel
            {
                DocId = docEntity.Id,
                Output = 1,
                Input = 0,
                Quantity = book.Quantity,

                PriceWithTax = book.PriceWithTax,

                Price = book.Price,

                Tax = book.Tax,

                Total = book.PriceWithTax,

                ArticleNotes = book.ArticleNotes,
                UnitOfMeasurement = book.UnitOfMeasurement,
            };

            var entityBook = App.FullMapper.Map<Book>(bookEntity);
            docEntity.Books.Add(entityBook);
        }

        private void CreateBookFinancialAndReserveInMemory(
         Document docEntity,
         decimal reserveFundAmount,
         DocumentViewModel document,
         List<BookFinancial> bookFinancials)
        {
            // Главен финансиски запис за фактурата
            bookFinancials.Add(new BookFinancial
            {
                Document = docEntity,
                CustomerId = docEntity.CustomerId,
                InvoiceId = (int)InvoiceTyp.Recieve,
                Owes = docEntity.TotalOutput ?? 0,
                Demands = 0,
                PaymentType = document.PaymentType,
                PaymentNumber = document.PaymentNumber,
                PaymentDate = document.PaymentDate,
                DatumF = docEntity.DateReceived,
                Status = docEntity.PaymentStatus,
                Time = DateTime.UtcNow
            });

            // Доколку има резервен фонд, се внесува во BookFinancials со InvoiceId = 1201
            if (reserveFundAmount > 0)
            {
                bookFinancials.Add(new BookFinancial
                {
                    Document = docEntity,
                    CustomerId = docEntity.CustomerId,
                    InvoiceId = 1201,
                    Owes = (double)reserveFundAmount,
                    Demands = 0,
                    PaymentType = document.PaymentType,
                    PaymentNumber = document.PaymentNumber,
                    PaymentDate = document.PaymentDate,
                    DatumF = docEntity.DateReceived,
                    Status = docEntity.PaymentStatus,
                    Time = DateTime.UtcNow
                });
            }
        }

        //private void CreateSpecialInvoiceInMemory(DocumentViewModel document, List<Document> documents)
        //{
        //    if (document.BuildingSpecialInvoice != null && document.BuildingSpecialInvoice.Any(x => x.IsSelected))
        //    {
        //        foreach (var special in document.BuildingSpecialInvoice.Where(x => x.IsSelected))
        //        {
        //            var specialDoc = new Document
        //            {
        //                CustomerId = special.CustomerId,
        //                Date = document.Date,
        //                ToDocument = DocumentService.GetMonthAsString(document.Date.Value.Month) + " " + document.Date.Value.Year,
        //                TotalOutput = special.Amount,
        //                PaymentStatus = PaymentStatus.Неплатено,
        //                Description = "Специјална фактура - " + special.Description
        //            };
        //            documents.Add(specialDoc);
        //        }
        //    }
        //}

        private List<BuildingProductViewModel> GetProductsForCustomer(List<BuildingProductViewModel> baseProducts, Customer customer, bool hasSetCost)
        {
            if (baseProducts == null) return new List<BuildingProductViewModel>();

            var filtered = baseProducts.Where(x => x.PriceWithTax != 0).ToList();

            if (hasSetCost && !customer.SetCost)
                filtered = filtered.Where(x => !x.IsNew).ToList();

            if (!customer.PresmetajAdministrativniTrosoci)
                filtered.RemoveAll(x => x.ArticleNotes?.Contains("административни трошоци", StringComparison.OrdinalIgnoreCase) == true);

            if (!customer.PresmetajKomunalnaTaksaJavnoOsvetluvanje)
                filtered.RemoveAll(x => x.ArticleNotes?.Contains("комунална такса за јавно осветлување", StringComparison.OrdinalIgnoreCase) == true);

            if (!customer.PresmetajOdrzuvanjeLift)
                filtered.RemoveAll(x => x.ArticleNotes?.Contains("одржување на лифт", StringComparison.OrdinalIgnoreCase) == true);

            if (!customer.PresmetajOdrzuvanjeSmetki)
                filtered.RemoveAll(x => x.ArticleNotes?.Contains("одржување на сметки", StringComparison.OrdinalIgnoreCase) == true);

            if (!customer.PresmetajPotrosenaElektricnaEnergija)
                filtered.RemoveAll(x => x.ArticleNotes?.Contains("потрошена електрична енергија", StringComparison.OrdinalIgnoreCase) == true);

            if (!customer.PresmetajRezervenFond)
                filtered.RemoveAll(x => x.ArticleNotes?.Contains("резервен фонд", StringComparison.OrdinalIgnoreCase) == true);

            if (!customer.PresmetajUpravitel)
                filtered.RemoveAll(x => x.ArticleNotes?.Contains("управител", StringComparison.OrdinalIgnoreCase) == true);

            if (!customer.PresmetajCistenjeVlez)
                filtered.RemoveAll(x => x.ArticleNotes?.Contains("чистење на влез", StringComparison.OrdinalIgnoreCase) == true);

            return filtered;
        }
        public async Task<IActionResult> ProcessPrintAndSend(
      List<Document> documents,
      Building building,
      List<Document> allDebts,
      bool send)
        {
            try
            {
                if (documents == null || !documents.Any())
                {
                    throw new Exception("No documents were provided for PDF generation.");
                }

                if (building == null)
                {
                    throw new Exception("Building is null.");
                }

                double total = 0;
                double owes = 0;
                double demands = 0;

                // ============================================================
                // CALCULATE BUILDING TOTALS
                // ============================================================

                try
                {
                    if (building.CustomerRefId != null &&
                        building.Customers != null &&
                        building.Customers.Any() &&
                        building.Customers.Count() <= 1)
                    {
                        total = (double)await _unitOfWork.Documents.Query()
                            .Where(x =>
                                x.CustomerId == building.CustomerRefId &&
                                x.PaymentStatus == PaymentStatus.Неплатено)
                            .SumAsync(x => x.TotalOutput);
                    }
                    else
                    {
                        var results = GetFilteredBookFinancials(
                            1201,
                            building.Id)
                            .ToList();

                        demands = results
                            .Where(x => !x.DontSum)
                            .Sum(x => x.Demands);

                        owes = results
                            .Where(x => !x.DontSum)
                            .Sum(x => x.Owes);

                        total = owes - demands;
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception(
                        $"ERROR while calculating building totals. " +
                        $"BuildingId={building.Id}, BuildingName={building.Name}",
                        ex);
                }

                // ============================================================
                // CREATE FINAL PDF
                // ============================================================

                PdfDocument endDoc = null;

                try
                {
                    endDoc = new PdfDocument();

                    int documentIndex = 0;

                    foreach (var item in documents)
                    {
                        documentIndex++;

                        try
                        {
                            if (item == null)
                            {
                                throw new Exception(
                                    $"Document #{documentIndex} is null.");
                            }

                            // ====================================================
                            // MAP DOCUMENT
                            // ====================================================

                            var document =
                                App.FullMapper.Map<DocumentViewModel>(item);

                            if (document == null)
                            {
                                throw new Exception(
                                    $"Document mapping returned null. " +
                                    $"DocumentId={item.Id}");
                            }

                            document.TotalBuildingDemands = (int)demands;
                            document.TotalBuildingOwes = (int)owes;

                            // ====================================================
                            // GET CUSTOMER DEBTS
                            // ====================================================

                            var debtQuery =
                                allDebts != null && allDebts.Any()
                                    ? allDebts
                                        .Where(x =>
                                            x.CustomerId == item.CustomerId &&
                                            x.PaymentStatus != 0)
                                    : _unitOfWork.Documents
                                        .GetAll()
                                        .Where(x =>
                                            x.CustomerId == item.CustomerId &&
                                            x.PaymentStatus != 0);

                            if (debtQuery != null && debtQuery.Any())
                            {
                                var allParts = debtQuery
                                 .OrderBy(x => x.Id)
                                 .SelectMany(x =>
                                     (
                                         x.ToDocument ??
                                         x.DateReceived?.ToString("yyyy-MM") ??
                                         string.Empty
                                     )
                                     .Split(',', StringSplitOptions.RemoveEmptyEntries))
                                 .ToList();

                                var distinctExceptLast = allParts
                                    .AsEnumerable()
                                    .Reverse()
                                    .Skip(1)
                                    .Reverse()
                                    .Distinct()
                                    .ToList();

                                document.Debt =
                                    string.Join(",", distinctExceptLast) +
                                    " : " +
                                    debtQuery.Sum(x => x.TotalOutput ?? 0);
                            }

                            // ====================================================
                            // COMPANY
                            // ====================================================

                            document.Company =
                                App.FullMapper.Map<CompanyConfig>(
                                    _config.Value);

                            if (document.Company == null)
                            {
                                throw new Exception(
                                    $"Company configuration is null. " +
                                    $"DocumentId={item.Id}");
                            }

                            document.IsForPdf = true;

                            // ====================================================
                            // SEND EMAIL
                            // ====================================================

                            if (send &&
                                !string.IsNullOrWhiteSpace(
                                    document.Customer?.Email))
                            {
                                try
                                {
                                    await CreateAndSend(document);
                                }
                                catch (Exception mailEx)
                                {
                                    throw new Exception(
                                        $"ERROR while creating/sending PDF email. " +
                                        $"DocumentId={item.Id}, " +
                                        $"CustomerId={item.CustomerId}, " +
                                        $"Email={document.Customer.Email}",
                                        mailEx);
                                }
                            }

                            // ====================================================
                            // RENDER HTML
                            // ====================================================

                            string htmlContent;

                            try
                            {
                                htmlContent =
                                    await RenderPartialViewToStringAsync(
                                        "~/Views/Shared/_DocumentDetailPartialPrint.cshtml",
                                        document);
                            }
                            catch (Exception viewEx)
                            {
                                throw new Exception(
                                    $"ERROR rendering invoice HTML. " +
                                    $"DocumentId={item.Id}",
                                    viewEx);
                            }

                            if (string.IsNullOrWhiteSpace(htmlContent))
                            {
                                throw new Exception(
                                    $"Generated HTML is empty. " +
                                    $"DocumentId={item.Id}");
                            }

                            // ====================================================
                            // BASE URL
                            // ====================================================

                            var request =
                                _httpContextAccessor?.HttpContext?.Request;

                            string baseUrl =
                                request != null
                                    ? $"{request.Scheme}://{request.Host.Value}/"
                                    : null;

                            // ====================================================
                            // CONVERT HTML -> PDF
                            // ====================================================

                            PdfDocument singleDoc = null;

                            try
                            {
                                var converter = new HtmlToPdf();

                                singleDoc = string.IsNullOrWhiteSpace(baseUrl)
                                    ? converter.ConvertHtmlString(htmlContent)
                                    : converter.ConvertHtmlString(
                                        htmlContent,
                                        baseUrl);

                                if (singleDoc == null)
                                {
                                    throw new Exception(
                                        $"SelectPdf returned null PdfDocument. " +
                                        $"DocumentId={item.Id}");
                                }

                                if (singleDoc.Pages == null ||
                                    singleDoc.Pages.Count == 0)
                                {
                                    throw new Exception(
                                        $"SelectPdf generated 0 pages. " +
                                        $"DocumentId={item.Id}");
                                }

                                // =================================================
                                // IMPORTANT
                                // Append entire document instead of AddPage()
                                // =================================================

                                endDoc.Append(singleDoc);

                                singleDoc = null;
                            }
                            catch (Exception pdfEx)
                            {
                                throw new Exception(
                                    $"ERROR converting HTML to PDF. " +
                                    $"DocumentId={item.Id}, " +
                                    $"DocumentNumber={item.Number}, " +
                                    $"HTML length={htmlContent.Length}, " +
                                    $"BaseUrl={baseUrl}",
                                    pdfEx);
                            }
                            finally
                            {
                                try
                                {
                                    singleDoc?.Close();
                                }
                                catch
                                {
                                }
                            }
                        }
                        catch (Exception documentEx)
                        {
                            throw new Exception(
                                $"ERROR processing document " +
                                $"{documentIndex}/{documents.Count}. " +
                                $"DocumentId={item?.Id}, " +
                                $"CustomerId={item?.CustomerId}, " +
                                $"Number={item?.Number}",
                                documentEx);
                        }
                    }

                    // ============================================================
                    // CHECK FINAL PDF
                    // ============================================================

                    if (endDoc == null)
                    {
                        throw new Exception(
                            "Final PdfDocument is null.");
                    }

                    if (endDoc.Pages == null ||
                        endDoc.Pages.Count == 0)
                    {
                        throw new Exception(
                            "Final PDF contains 0 pages.");
                    }

                    // ============================================================
                    // SAVE PDF
                    // ============================================================

                    try
                    {
                        using var pdfStream = new MemoryStream();

                        endDoc.Save(pdfStream);

                        if (pdfStream.Length == 0)
                        {
                            throw new Exception(
                                "PDF stream is empty after Save().");
                        }

                        byte[] pdfBytes =
                            pdfStream.ToArray();

                        var fileResult =
                            new FileContentResult(
                                pdfBytes,
                                "application/pdf");

                        var dateOnly =
                            documents.FirstOrDefault()?.Date;

                        if (dateOnly != null)
                        {
                            fileResult.FileDownloadName =
                                $"{building.Name}_" +
                                $"{dateOnly.Value.Month}_" +
                                $"{dateOnly.Value.Year}.pdf";
                        }
                        else
                        {
                            fileResult.FileDownloadName =
                                $"{building.Name}_Invoices.pdf";
                        }

                        return fileResult;
                    }
                    catch (Exception saveEx)
                    {
                        throw new Exception(
                            $"ERROR SAVING FINAL PDF. " +
                            $"BuildingId={building.Id}, " +
                            $"BuildingName={building.Name}, " +
                            $"Documents={documents.Count}, " +
                            $"Pages={endDoc.Pages?.Count ?? 0}",
                            saveEx);
                    }
                }
                finally
                {
                    try
                    {
                        endDoc?.Close();
                    }
                    catch
                    {
                    }
                }
            }
            catch (Exception ex)
            {
                return Content(
                    "========================================\n" +
                    "CLEANHUB PDF ERROR\n" +
                    "========================================\n\n" +
                    ex.ToString(),
                    "text/plain");
            }
        }
        private string RenderInvoiceHtml(Document doc, Building building, List<Document> customerDebts)
        {
            return $"<html><body><h1>Фактура бр. {doc.Number}</h1><p>За: {doc.Customer?.CustomerInfo}</p><p>Износ: {doc.TotalOutput} ден.</p></body></html>";
        }

        private async Task SendEmailWithPdfAsync(string emailTo, string htmlContent, int docNumber)
        {
            try
            {
                var converter = new HtmlToPdf();
                PdfDocument pdfDoc = converter.ConvertHtmlString(htmlContent);

                using var ms = new MemoryStream();
                pdfDoc.Save(ms);
                pdfDoc.Close();
                ms.Position = 0;

                using var mail = new MailMessage();
                mail.From = new MailAddress("your-email@domain.com", "CleanHub System");
                mail.To.Add(emailTo);
                mail.Subject = $"Фактура бр. {docNumber}";
                mail.Body = "Почитувани, во прилог е вашата најнова фактура.";
                mail.Attachments.Add(new Attachment(ms, $"Faktura_{docNumber}.pdf", "application/pdf"));

                using var smtp = new SmtpClient("smtp.yourserver.com", 587);
                smtp.Credentials = new NetworkCredential("your-email@domain.com", "your-password");
                smtp.EnableSsl = true;

                await smtp.SendMailAsync(mail);
            }
            catch
            {
            }
        }

        [HttpGet]
        public async Task<IActionResult> Create(int? id, int? buildingId, DateTime? date)
        {
            try
            {
                var documentViewModel = new DocumentViewModel();
                ViewBag.RouteId = id;
                var now = DateTime.UtcNow;

                if (!date.HasValue)
                {
                    var lastDayOfLastMonth = new DateTime(now.Year, now.Month, 1).AddDays(-1);
                    documentViewModel.Date = DateOnly.FromDateTime(lastDayOfLastMonth);
                }
                else
                {
                    documentViewModel.Date = DateOnly.FromDateTime(date.Value);
                }

                documentViewModel.Company = _config.Value;
                var buildings = new List<Building>();

                if (id == 2)
                {
                    buildings = (List<Building>)await _unitOfWork.Buildings
                            .GetAllAsync(query => query.Select(b => new Building()
                            {
                                Id = b.Id,
                                Name = b.Name
                            }));

                    documentViewModel.Buildings = App.FullMapper.Map<List<BuildingViewModel>>(buildings);
                    documentViewModel.Building = buildingId.HasValue
                            ? documentViewModel.Buildings.FirstOrDefault(x => x.Id == buildingId.Value)
                            : documentViewModel.Buildings.FirstOrDefault();

                    if (documentViewModel.Building == null)
                    {
                        documentViewModel.Building = documentViewModel.Buildings.FirstOrDefault();
                    }

                    documentViewModel.BuildingId = documentViewModel.Building?.Id ?? 0;
                    documentViewModel.Building.BuildingProducts = new List<BuildingProductViewModel>();
                }
                else
                {
                    buildings = (List<Building>)await _unitOfWork.Buildings
                            .GetAllAsync(query => query
                                    .Include(x => x.BuildingProducts)
                                    .Include(x => x.Customers)
                                    .Select(b => new Building()
                                    {
                                        Id = b.Id,
                                        Name = b.Name,
                                        ReserveFund = b.ReserveFund,
                                        Customers = b.Customers,
                                        BuildingProducts = b.BuildingProducts
                                    }));

                    documentViewModel.Buildings = App.FullMapper.Map<List<BuildingViewModel>>(buildings);
                    documentViewModel.Building = buildingId.HasValue
                            ? documentViewModel.Buildings.FirstOrDefault(x => x.Id == buildingId.Value)
                            : documentViewModel.Buildings.FirstOrDefault();

                    if (documentViewModel.Building == null)
                    {
                        documentViewModel.Building = documentViewModel.Buildings.FirstOrDefault();
                    }

                    if (documentViewModel.Building == null)
                    {
                        throw new Exception($"Building not found. BuildingId = {buildingId}, Id = {id}");
                    }

                    documentViewModel.BuildingId = documentViewModel.Building.Id;
                    documentViewModel.Building.Customers = documentViewModel.Building.Customers
                            .Where(x => !x.Hide && x.Inactive != true && x.ActiveDatum.HasValue && x.ActiveDatum <= documentViewModel.Date)
                            .ToList();

                    var filteredProducts = (id == 0)
                            ? documentViewModel.Building.BuildingProducts
                            : documentViewModel.Building.BuildingProducts
                                .Where(x => x.ArticleNotes != null && x.ArticleNotes.ToLower().Contains("влез"))
                                .ToList();

                    if (filteredProducts != null && filteredProducts.Any())
                    {
                        documentViewModel.Building.BuildingProducts = filteredProducts;
                    }
                    else
                    {
                        var basicProducts = (id == 0)
                                ? await _unitOfWork.Products.GetAllAsync()
                                : await _unitOfWork.Products.GetAllAsync(x => x.Where(p => p.ArticleNotes != null && p.ArticleNotes.Contains("влез")));

                        documentViewModel.Building.BuildingProducts = App.FullMapper.Map<List<BuildingProductViewModel>>(basicProducts);

                        foreach (var product in documentViewModel.Building.BuildingProducts.Where(p => p.ArticleNotes != null && p.ArticleNotes.ToLower().Contains("резервен")))
                        {
                            if (documentViewModel.Building.ReserveFund != null)
                            {
                                product.Price = documentViewModel.Building.ReserveFund.Value;
                            }
                        }
                    }
                }

                if (documentViewModel.Building?.BuildingProducts == null)
                {
                    documentViewModel.Building.BuildingProducts = new List<BuildingProductViewModel>();
                }

                foreach (var product in documentViewModel.Building.BuildingProducts)
                {
                    if (product.PriceWithTax == null) product.PriceWithTax = 0;
                    if (product.Total == null) product.Total = 0;
                }

                ViewBag.Buildings = new SelectList(buildings, "Id", "Name", documentViewModel.BuildingId);
                ViewBag.SelectedBuildingName = documentViewModel.Building?.Name;
                ViewBag.BuildingId = documentViewModel.BuildingId;

                var results = GetFilteredBookFinancials(1201, documentViewModel.BuildingId.Value).ToList();

                documentViewModel.TotalBuildingDemands = results.Where(x => !x.DontSum).Sum(x => x.Demands);
                documentViewModel.TotalBuildingOwes = results.Where(x => !x.DontSum).Sum(x => x.Owes);

                return View(documentViewModel);
            }
            catch (Exception ex)
            {
                return Content(
                    "========================================\n" +
                    "CLEANHUB GET CREATE ERROR\n" +
                    "========================================\n\n" +
                    ex.ToString(),
                    "text/plain");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCostForBuilding(DocumentViewModel document, string actionType)
        {
            bool send = actionType == "send";
            ModelState.Remove(nameof(actionType));
            if (ModelState.IsValid)
            {
                var building = await _unitOfWork.Buildings.GetByIdAsync(x => x.Id == document.BuildingId);
                var toDocument = DocumentService.GetMonthAsString(document.Date.Value.Month) + " " + document.Date.Value.Year;

                if (document.BuildingId == null || document.BuildingId == 0)
                {
                    document.BuildingId = 1;
                }

                ViewBag.Buildings = new SelectList(Buildings, "Id", "Name", document.BuildingId);

                var selectedBuildingName = Buildings?.FirstOrDefault(x => x.Id == building.Id)?.Name;
                if (selectedBuildingName != null)
                    ViewBag.SelectedBuildingName = selectedBuildingName;

                foreach (var product in document.Building.BuildingProducts.Where(x => !string.IsNullOrEmpty(x.ArticleNotes)))
                {
                    var bookFinancial = new BookFinancialViewModel
                    {
                        DatumF = document.Date,
                        InvoiceId = 1201, // Заменет Constants.Reserve
                        Demands = 0,
                        Owes = PriceHelper.CalculatePriceWithTax(product.Price, product.Tax),
                        CustomerId = building.CustomerRefId ?? building.Id,
                        DocumentTypId = 5,
                        Description = product.ArticleNotes,
                        Status = PaymentStatus.Неплатено
                    };
                    _unitOfWork.BookFinancials.Add(App.FullMapper.Map<BookFinancial>(bookFinancial));
                }
                await _unitOfWork.SaveChangesAsync();
            }

            return View(document);
        }

        [HttpPost]
        public async Task<IActionResult> CreateBookFinancial(PaymentType PaymentType, DateTime PaymentDate, string PaymentNumber, string PaymentDescription, int Total, int customerId)
        {
            var bookFinancial = new BookFinancial()
            {
                CustomerId = customerId,
                Description = PaymentDescription,
                Demands = Total,
                Owes = 0,
                PaymentType = PaymentType,
                PaymentDate = DateOnly.FromDateTime(PaymentDate),
                InvoiceId = (int)InvoiceTyp.Recieve,
                DocumentTypId = 4,
                Status = (int)PaymentStatus.Платено,
                Time = DateTime.UtcNow,
                DatumF = DateOnly.FromDateTime(PaymentDate),
                DateTimeChanges = DateTime.UtcNow
            };
            _unitOfWork.BookFinancials.Add(bookFinancial);
            await _unitOfWork.SaveChangesAsync();

            return Ok(new { success = true, message = "Успешно зачувано плаќање!" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePaymentMultipleDocuments(string SelectedInvoiceIds, PaymentType PaymentType, DateTime PaymentDate, string PaymentNumber, string PaymentDescription, int Total)
        {
            if (SelectedInvoiceIds == null || !SelectedInvoiceIds.Any())
            {
                TempData["Error"] = "Изберете најмалку една фактура.";
                return RedirectToAction(nameof(Index));
            }
            var ids = SelectedInvoiceIds.Split(',').Select(int.Parse).ToList();

            var documents = await _unitOfWork.Documents
                .Query()
                .Include(x => x.Books)
                .Where(x => ids.Contains(x.Id))
                .ToListAsync();

            if (documents != null && documents.Any())
            {
                foreach (var document in documents)
                {
                    var bookFinancials = _unitOfWork.BookFinancials.GetAll().Where(x => x.DocumentId == document.Id).ToList();

                    if (PaymentDate != DateTime.MinValue)
                    {
                        document.PaymentDate = DateOnly.FromDateTime(PaymentDate);
                    }
                    if (PaymentType != PaymentType.Bank)
                    {
                        document.PaymentType = PaymentType;
                    }
                    if (!string.IsNullOrEmpty(PaymentNumber))
                    {
                        document.PaymentNumber = PaymentNumber;
                    }
                    if (!string.IsNullOrEmpty(PaymentDescription))
                    {
                        document.PaymentDescription = PaymentDescription;
                    }
                    _unitOfWork.Documents.Update(document);
                    if (bookFinancials != null && bookFinancials.Any())
                    {
                        foreach (var bookFinancial in bookFinancials)
                        {
                            if (!string.IsNullOrEmpty(PaymentNumber))
                            {
                                bookFinancial.PaymentNumber = PaymentNumber;
                            }
                            if (!string.IsNullOrEmpty(PaymentDescription))
                            {
                                bookFinancial.Description = PaymentDescription;
                            }
                            if (Total != 0 && bookFinancial.InvoiceId == 1200)
                            {
                                bookFinancial.Demands = Total;
                            }
                            bookFinancial.DateTimeChanges = DateTime.UtcNow;
                            _unitOfWork.BookFinancials.Update(bookFinancial);
                        }
                    }
                    else
                    {
                        CreateBookFinancialAndReserve(document, document.CustomerId.Value, 0, document.PaymentDate, document.PaymentType, document.PaymentNumber);
                    }
                }

                await _unitOfWork.SaveChangesAsync();
                var customerId = documents?.FirstOrDefault()?.CustomerId;
                var customer = await _unitOfWork.Customers.GetByIdAsync(x => x.Id == customerId, inc => inc.Include(bu => bu.Building));
                var building = customer?.Building;
                if (building != null)
                {
                    TempData["SelectedBuildingId"] = building.Id;
                    TempData["SelectedBuildingName"] = building.Name;
                }
            }

            TempData["Success"] = $"{ids.Count} фактури се успешно изменети.";

            return RedirectToAction(nameof(Index), new { fromPaymentStatus = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteMultipleDocuments(string SelectedInvoiceIds)
        {
            if (SelectedInvoiceIds == null || !SelectedInvoiceIds.Any())
            {
                TempData["Error"] = "Изберете најмалку една фактура.";
                return RedirectToAction(nameof(Index));
            }
            var ids = SelectedInvoiceIds.Split(',').Select(int.Parse).ToList();

            var booksToDelete = _unitOfWork.Books.GetAll().Where(x => ids.Contains(x.DocId)).ToList();
            if (booksToDelete != null && booksToDelete.Any())
            {
                _unitOfWork.Books.DeleteRange(booksToDelete);
            }
            var bookFinancialsToDelete = _unitOfWork.BookFinancials.GetAll().Where(x => x.DocumentId.HasValue && ids.Contains(x.DocumentId.Value)).ToList();
            if (bookFinancialsToDelete != null && bookFinancialsToDelete.Any())
            {
                _unitOfWork.BookFinancials.DeleteRange(bookFinancialsToDelete);
            }

            var documentsToDelete = _unitOfWork.Documents.GetAll().Where(x => ids.Contains(x.Id)).ToList();
            if (documentsToDelete != null && documentsToDelete.Any())
            {
                _unitOfWork.Documents.DeleteRange(documentsToDelete);
                await _unitOfWork.SaveChangesAsync();
                var idsDoc = documentsToDelete.Select(d => d.Id.ToString()).ToList();

                var documents = _context.DokumentiTest.Where(x => idsDoc.Contains(x.Dokid));
                if (documents != null && documents.Any())
                {
                    _context.DokumentiTest.RemoveRange(documents);
                    _context.SaveChanges();
                }
                var customerId = documentsToDelete?.FirstOrDefault()?.CustomerId;
                var customer = await _unitOfWork.Customers.GetByIdAsync(x => x.Id == customerId, inc => inc.Include(bu => bu.Building));
                var building = customer?.Building;
                if (building != null)
                {
                    TempData["SelectedBuildingId"] = building.Id;
                    TempData["SelectedBuildingName"] = building.Name;
                }
            }

            TempData["Success"] = $"{ids.Count} фактури се успешно избришани.";

            return RedirectToAction(nameof(Index), new { fromPaymentStatus = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetMultiplePayments(string SelectedInvoiceIds, PaymentType PaymentType, DateTime PaymentDate, string PaymentNumber, string PaymentDescription)
        {
            if (SelectedInvoiceIds == null || !SelectedInvoiceIds.Any())
            {
                TempData["Error"] = "Изберете најмалку една фактура.";
                return RedirectToAction("Index");
            }
            var ids = SelectedInvoiceIds.Split(',').Select(int.Parse).ToList();

            var documents = _unitOfWork.Documents
                .GetAll(query => query
                    .Where(d => ids.Contains(d.Id))
                    .Include(d => d.Customer)
                    .ThenInclude(c => c.BookFinancials))
                .ToList();

            if (documents != null && documents.Any())
            {
                foreach (var document in documents)
                {
                    document.PaymentStatus = PaymentStatus.Платено;
                    document.PaymentDate = DateOnly.FromDateTime(PaymentDate);
                    document.PaymentDescription = PaymentDescription;
                    document.PaymentType = PaymentType;
                    document.PaymentNumber = PaymentNumber;
                    SetStatusPayment(App.FullMapper.Map<DocumentViewModel>(document));
                }
                await _unitOfWork.SaveChangesAsync();
                var customerId = documents?.FirstOrDefault()?.CustomerId;
                var customer = await _unitOfWork.Customers.GetByIdAsync(x => x.Id == customerId, inc => inc.Include(bu => bu.Building));
                var building = customer?.Building;
                if (building != null)
                {
                    TempData["SelectedBuildingId"] = building.Id;
                    TempData["SelectedBuildingName"] = building.Name;
                }
            }

            TempData["Success"] = $"{ids.Count} фактури се успешно платени.";

            return RedirectToAction(nameof(Index), new { fromPaymentStatus = true });
        }

        private void CreateSpecialInvoice(DocumentViewModel document)
        {
            int sum = 0;
            var energyValues = document.Building?.BuildingProducts
                .Where(x => x.ArticleNotes != null && x.Total != 0 && (x.ArticleNotes.ToLower().Contains("енер.") || x.ArticleNotes.ToLower().Contains("осветлување")))
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
                BuildingId = document.BuildingId,
                Status = PaymentStatus.Неплатено
            };
            specialInvoiceViewModel.Building = null;
            var specialInvoice = App.FullMapper.Map<SpecialInvoice>(specialInvoiceViewModel);
            _unitOfWork.SpecialInvoices.UpdateSpecialInvoices(document, specialInvoice);
        }

        private void CreateBookFinancialAndReserve(Document docEntity, int customerId, int reserve, DateOnly? paymentDate, PaymentType? paymentType, string? paymentNumber)
        {
            if (docEntity.PaymentStatus == PaymentStatus.Платено)
            {
                var bookFinancials = _unitOfWork.BookFinancials.GetAll().Where(x => x.CustomerId == customerId).ToList();
                var existsInBookFinancials = AlreadyPayedInBookFinancials(bookFinancials, customerId, docEntity.ToDocument);

                if (!existsInBookFinancials)
                {
                    var bookFinancialViewModel = new BookFinancialViewModel
                    {
                        InvoiceId = Constants.Recieve,
                        DocumentId = docEntity.Id,
                        Demands = docEntity!.TotalOutput!.Value!,
                        Owes = 0,
                        DocumentTypId = 4,
                        CustomerId = customerId,
                        Time = DateTime.Now,
                        Status = PaymentStatus.Платено,
                        DatumF = docEntity.DateReceived,
                        PaymentDate = paymentDate.HasValue && paymentDate.Value != DateOnly.MinValue ? paymentDate.Value : DateOnly.FromDateTime(DateTime.UtcNow),
                        PaymentType = docEntity.PaymentType.Value,
                        Description = string.IsNullOrEmpty(docEntity.PaymentDescription) ? docEntity.PaymentType.GetEnumDescription() : docEntity.PaymentDescription,
                        PaymentNumber = paymentNumber
                    };
                    CreateReserve(docEntity, customerId, reserve, paymentDate, paymentType, paymentNumber);
                    var bookFinancial = App.FullMapper.Map<BookFinancial>(bookFinancialViewModel);
                    _unitOfWork.BookFinancials.Add(bookFinancial);
                }
            }
        }

        public void CreateReserve(Document docEntity, int customerId, int reserve, DateOnly? paymentDate, PaymentType? paymentType, string? paymentNumber)
        {
            var bookFinancialViewModelReserve = new BookFinancialViewModel();

            if (docEntity.PaymentStatus == PaymentStatus.Платено)
            {
                bookFinancialViewModelReserve = new BookFinancialViewModel()
                {
                    InvoiceId = 1201, // Заменет Constants.Reserve со 1201
                    DocumentId = docEntity.Id,
                    Demands = docEntity.Books.FirstOrDefault(x => x.ArticleNotes.Contains("Резервен фонд"))?.Total ?? 0.0,
                    DocumentTypId = 4,
                    Owes = 0,
                    DatumF = docEntity.DateReceived,
                    CustomerId = customerId,
                    Status = docEntity.PaymentStatus,
                    Time = DateTime.Now,
                    Description = string.IsNullOrEmpty(docEntity.PaymentDescription) ? docEntity.PaymentType.GetEnumDescription() : docEntity.PaymentDescription,
                    PaymentDate = paymentDate.HasValue && paymentDate.Value != DateOnly.MinValue ? paymentDate.Value : DateOnly.FromDateTime(DateTime.UtcNow),
                    PaymentType = docEntity.PaymentType.Value,
                    PaymentNumber = paymentNumber
                };
            }
            else
            {
                bookFinancialViewModelReserve = new BookFinancialViewModel()
                {
                    InvoiceId = 1201, // Заменет Constants.Reserve со 1201
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
            }

            var bookFinancialReserve = App.FullMapper.Map<BookFinancial>(bookFinancialViewModelReserve);
            _unitOfWork.BookFinancials.Add(bookFinancialReserve);
        }

        private async Task<Document> CreateCustomerDocument(Customer customer, DocumentViewModel document, Building building)
        {
            var documentCustomer = new DocumentViewModel
            {
                CustomerId = customer.Id,
                Number = (await _unitOfWork.Documents.GetMaxAsync(x => x.Number) ?? 0) + 1,
                Date = document.Date
            };

            if (document.Date != null)
                documentCustomer.ToDocument = DocumentService.GetMonthAsString(document.Date.Value.Month) + " " + document.Date.Value.Year;

            documentCustomer.Description = building.Name;
            documentCustomer.Date = document.Date;
            documentCustomer.CreatedTime = DateTime.UtcNow;

            if (document.Date != null)
                documentCustomer.DateReceived = DateOnly.FromDateTime(document.Date.Value.ToDateTime(TimeOnly.MinValue).AddDays(10));

            documentCustomer.TotalInput = 0;
            var calculator = new PriceCalculator(building.Customers.ToList());
            var tempBuildingProducts = document.Building?.BuildingProducts.ToList();

            var hasSetCost = building.Customers?.Any(x => x.SetCost) == true;

            if (!customer.Garage)
            {
                tempBuildingProducts = tempBuildingProducts?
                    .Where(x => x.ArticleNotes == null || !x.ArticleNotes.Contains("гаража"))
                    .ToList();
            }

            if (hasSetCost && !customer.SetCost)
            {
                tempBuildingProducts = tempBuildingProducts?
                    .Where(x => !x.IsNew)
                    .ToList();
            }

            if (!customer.PresmetajAdministrativniTrosoci)
            {
                tempBuildingProducts?.RemoveAll(x => x.ArticleNotes != null && x.ArticleNotes.Contains("административни трошоци", StringComparison.OrdinalIgnoreCase));
            }

            if (!customer.PresmetajKomunalnaTaksaJavnoOsvetluvanje)
            {
                tempBuildingProducts?.RemoveAll(x => x.ArticleNotes != null && x.ArticleNotes.Contains("комунална такса за јавно осветлување", StringComparison.OrdinalIgnoreCase));
            }

            if (!customer.PresmetajOdrzuvanjeLift)
            {
                tempBuildingProducts?.RemoveAll(x => x.ArticleNotes != null && x.ArticleNotes.Contains("одржување на лифт", StringComparison.OrdinalIgnoreCase));
            }

            if (!customer.PresmetajOdrzuvanjeSmetki)
            {
                tempBuildingProducts?.RemoveAll(x => x.ArticleNotes != null && x.ArticleNotes.Contains("одржување на сметки", StringComparison.OrdinalIgnoreCase));
            }

            if (!customer.PresmetajPotrosenaElektricnaEnergija)
            {
                tempBuildingProducts?.RemoveAll(x => x.ArticleNotes != null && x.ArticleNotes.Contains("потрошена електрична енергија", StringComparison.OrdinalIgnoreCase));
            }

            if (!customer.PresmetajRezervenFond)
            {
                tempBuildingProducts?.RemoveAll(x => x.ArticleNotes != null && x.ArticleNotes.Contains("резервен фонд", StringComparison.OrdinalIgnoreCase));
            }

            if (!customer.PresmetajUpravitel)
            {
                tempBuildingProducts?.RemoveAll(x => x.ArticleNotes != null && x.ArticleNotes.Contains("управител", StringComparison.OrdinalIgnoreCase));
            }

            if (!customer.PresmetajCistenjeVlez)
            {
                tempBuildingProducts?.RemoveAll(x => x.ArticleNotes != null && x.ArticleNotes.Contains("чистење на влез", StringComparison.OrdinalIgnoreCase));
            }

            calculator.CalculatePrices(tempBuildingProducts, customer);

            if (tempBuildingProducts != null)
            {
                float totalPriceWithTax = calculator.CalculateTotalPriceWithTaxSum(tempBuildingProducts);
                documentCustomer.TotalOutput = totalPriceWithTax;
            }

            var docEntity = App.FullMapper.Map<Document>(documentCustomer);
            _unitOfWork.Documents.Add(docEntity);
            await _unitOfWork.SaveChangesAsync();

            if (customer.Subscription.HasValue && customer.Subscription != 0 && customer.Subscription >= documentCustomer.TotalOutput)
            {
                customer.Subscription = (int?)(customer.Subscription - documentCustomer.TotalOutput.Value);
                docEntity.PaymentStatus = PaymentStatus.Платено;
                docEntity.PaymentType = PaymentType.Subscription;
                docEntity.Description = customer.Building.Name;
                docEntity.PaymentDate = DateOnly.FromDateTime(DateTime.UtcNow);

                _unitOfWork.Customers.Update(customer);
            }
            else
            {
                docEntity.PaymentStatus = PaymentStatus.Неплатено;
            }

            _unitOfWork.Documents.Update(docEntity);

            return docEntity;
        }

        private bool AlreadyPayedInBookFinancials(List<BookFinancial> bookFinancials, int customerId, string toDocument)
        {
            var months = new Dictionary<string, int>
            {
                { "Јануари", 1 }, { "Февруари", 2 }, { "Март", 3 }, { "Април", 4 },
                { "Мај", 5 }, { "Јуни", 6 }, { "Јули", 7 }, { "Август", 8 },
                { "Септември", 9 }, { "Октомври", 10 }, { "Ноември", 11 }, { "Декември", 12 }
            };

            if (string.IsNullOrEmpty(toDocument)) return false;

            var parts = toDocument.Split(' ');
            if (parts.Length < 2) return false;

            if (!months.TryGetValue(parts[0], out int month)) return false;
            if (!int.TryParse(parts[1], out int year)) return false;

            string yearText = year.ToString();

            var entries = bookFinancials
                .Where(x => x.CustomerId == customerId && !string.IsNullOrWhiteSpace(x.Description) && x.Description.Contains(yearText))
                .ToList();

            foreach (var bf in entries)
            {
                var desc = bf.Description.Trim();

                var range = Regex.Match(desc, @"(\d{1,2})\s*-\s*(\d{1,2})\s*/\s*" + yearText);
                if (range.Success)
                {
                    if (int.TryParse(range.Groups[1].Value, out int from) && int.TryParse(range.Groups[2].Value, out int to))
                    {
                        if (month >= from && month <= to) return true;
                    }
                }

                var list = Regex.Match(desc, @"([\d,\s]+)\s*/\s*" + yearText);
                if (list.Success)
                {
                    var foundMonths = list.Groups[1].Value.Split(',')
                        .Select(x => int.TryParse(x.Trim(), out int m) ? m : -1)
                        .Where(x => x > 0)
                        .ToList();

                    if (foundMonths.Contains(month)) return true;
                }

                var single = Regex.Match(desc, @"(\d{1,2})\s*/\s*" + yearText);
                if (single.Success)
                {
                    if (int.TryParse(single.Groups[1].Value, out int singleMonth) && singleMonth == month)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public void SetStatusPayment(DocumentViewModel model)
        {
            var bookfinancialToUpdate = _unitOfWork.BookFinancials.GetAll(wh => wh.Where(x => x.DocumentId == model.Id));
            try
            {
                var documentToUpdate = _unitOfWork.Documents.GetByIdAsync(x => x.Id == model.Id, include: inc => inc.Include(bu => bu.Books).Include(cu => cu.Customer).ThenInclude(bu => bu.Building)).Result;

                documentToUpdate.PaymentStatus = PaymentStatus.Платено;
                documentToUpdate.PaymentDate = model.PaymentDate;
                documentToUpdate.PaymentDescription = model.PaymentDescription;
                documentToUpdate.PaymentType = model.PaymentType;
                documentToUpdate.PaymentNumber = model.PaymentNumber;

                if (bookfinancialToUpdate != null && bookfinancialToUpdate.Any())
                {
                    foreach (var item in bookfinancialToUpdate)
                    {
                        if (model.PaymentDate != null) item.PaymentDate = model.PaymentDate.Value;
                        item.PaymentType = model.PaymentType;
                        item.PaymentNumber = model.PaymentNumber;
                        item.Description = model.PaymentDescription;
                        item.Demands = item.Owes;
                        item.PaymentDate = model.PaymentDate;
                        item.DateTimeChanges = DateTime.UtcNow;
                        item.Owes = 0;
                        item.Status = PaymentStatus.Платено;
                    }
                    _unitOfWork.BookFinancials.UpdateRange(bookfinancialToUpdate);
                }
                else
                {
                    var listToAdd = new List<BookFinancial>
                    {
                        new()
                        {
                            InvoiceId = Constants.Recieve,
                            PaymentType = model.PaymentType,
                            PaymentNumber = model.PaymentNumber,
                            Description = model.PaymentDescription,
                            Demands = documentToUpdate.TotalOutput.GetValueOrDefault(),
                            Owes = 0,
                            DatumF = model.DateReceived,
                            PaymentDate = model.PaymentDate,
                            Time = DateTime.UtcNow,
                            DateTimeChanges = DateTime.UtcNow,
                            DocumentTypId = 4,
                            CustomerId = documentToUpdate.Customer.Id,
                            DocumentId = documentToUpdate.Id,
                            Status = PaymentStatus.Платено
                        }
                    };

                    if (documentToUpdate.Customer.ActivityId != 1 && documentToUpdate.Books
                             .Any(x => x.ArticleNotes != null && x.ArticleNotes.Contains("резервен фонд", StringComparison.OrdinalIgnoreCase)))
                    {
                        listToAdd.Add(new BookFinancial
                        {
                            InvoiceId = 1201, // Заменет Constants.Reserve
                            PaymentType = model.PaymentType,
                            Time = DateTime.UtcNow,
                            PaymentNumber = model.PaymentNumber,
                            DateTimeChanges = DateTime.UtcNow,
                            PaymentDate = model.PaymentDate,
                            Description = model.PaymentDescription,
                            DocumentId = documentToUpdate.Id,
                            Demands = documentToUpdate.Books
                                .FirstOrDefault(x => x.ArticleNotes != null && x.ArticleNotes.Contains("резервен фонд", StringComparison.OrdinalIgnoreCase))
                                ?.Total ?? 0,
                            Owes = 0,
                            CustomerId = documentToUpdate.Customer.Id,
                            DocumentTypId = 4,
                            DatumF = model.DateReceived,
                            Status = PaymentStatus.Платено
                        });
                    }

                    _unitOfWork.BookFinancials.AddRange(listToAdd);
                }
                _unitOfWork.Documents.Update(documentToUpdate);
            }
            catch (DbUpdateConcurrencyException)
            {
            }
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var documentEntity = await _unitOfWork.Documents.GetByIdAsync(x => x.Id == id.Value, d => d
                .Include(x => x.Books)
                .Include(d => d.Customer));

            if (documentEntity == null) return NotFound();

            var document = App.FullMapper.Map<DocumentViewModel>(documentEntity);
            document.Company = _config.Value;
            var debt = _unitOfWork.Documents.GetAll().Where(x => x.CustomerId == document.CustomerId && x.PaymentStatus != 0);

            if (debt != null && debt.Any())
            {
                var allParts = debt.OrderBy(x => x.Id)
                    .SelectMany(x => x.ToDocument.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    .ToList();

                var distinctExceptLast = allParts
                    .Reverse<string>()
                    .Skip(1)
                    .Reverse()
                    .Distinct()
                    .ToList();

                if (distinctExceptLast != null && distinctExceptLast.Any() && distinctExceptLast.Count() >= 8)
                {
                    document.Debt = " : " + debt.Sum(x => x.TotalOutput ?? 0);
                }
                else
                {
                    document.Debt = string.Join(",", distinctExceptLast) + " : " + debt.Sum(x => x.TotalOutput ?? 0);
                }
            }
            ViewData["CustomerId"] = new SelectList(_unitOfWork.Customers.GetAll().Where(x => !x.Hide), "Id", "Name", document.CustomerId);
            return PartialView("_DocumentDetailPartial", document);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, DocumentViewModel document)
        {
            if (id != document.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    var documentEntity = App.FullMapper.Map<Document>(document);
                    _unitOfWork.Documents.Update(documentEntity);
                    await _unitOfWork.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!InvoiceExists(document.Id)) return NotFound();
                    throw;
                }

                return RedirectToAction(nameof(Index));
            }
            ViewData["CustomerId"] = new SelectList(_unitOfWork.Customers.GetAll().Where(x => !x.Hide), "Id", "Name", document.CustomerId);
            return View(document);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var invoice = await _unitOfWork.Documents.GetByIdAsync(x => x.Id == id.Value, x => x.Include(i => i.Books));

            if (invoice == null) return NotFound();

            return View(invoice);
        }

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

        public async Task<IActionResult> CombineAndDownloadPdfs(int? buildingId, int? paymentStatusId, int year)
        {
            var customers = await _unitOfWork.Customers.GetCustomersByBuildingIdAsync(buildingId.Value);

            var startDate = new DateOnly();
            var endDate = new DateOnly();

            var results = GetFilteredBookFinancials(1201, buildingId.Value).ToList();
            var demands = results.Where(x => !x.DontSum).Sum(su => su.Demands);
            var owes = results.Where(x => !x.DontSum).Sum(su => su.Owes);

            PdfDocument endDoc = new PdfDocument();
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

                if (document == null) continue;

                document.TotalBuildingDemands = (int)demands;
                document.TotalBuildingOwes = (int)owes;
                var debt = _unitOfWork.Documents.GetAll().Where(x => x.CustomerId == document.CustomerId && x.PaymentStatus != 0);

                if (debt != null && debt.Any())
                {
                    var allParts = debt.OrderBy(x => x.Id)
                        .SelectMany(x => x.ToDocument.Split(',', StringSplitOptions.RemoveEmptyEntries))
                        .ToList();

                    var distinctExceptLast = allParts
                        .Reverse<string>()
                        .Skip(1)
                        .Reverse()
                        .Distinct()
                        .ToList();

                    if (distinctExceptLast != null && distinctExceptLast.Any() && distinctExceptLast.Count() >= 8)
                    {
                        document.Debt = " : " + debt.Sum(x => x.TotalOutput ?? 0);
                    }
                    else
                    {
                        document.Debt = string.Join(",", distinctExceptLast) + " : " + debt.Sum(x => x.TotalOutput ?? 0);
                    }
                }
                document.Company = _config.Value;
                document.IsForPdf = true;

                string htmlContent = await RenderPartialViewToStringAsync("~/Views/Shared/_DocumentDetailPartialPrint.cshtml", document);

                var request = _httpContextAccessor?.HttpContext?.Request;
                string baseUrl = $"{request?.Scheme}://{request?.Host.Value}/";

                HtmlToPdf converter = new HtmlToPdf();
                PdfDocument doc = converter.ConvertHtmlString(htmlContent, baseUrl);
                endDoc.Append(doc);
            }

            byte[] pdf = endDoc.Save();
            endDoc.Close();

            FileResult fileResult = new FileContentResult(pdf, "application/pdf")
            {
                FileDownloadName = $"{Buildings.FirstOrDefault(x => x.Id == buildingId.Value)?.Name}_{startDate.Month}_{startDate.Year}.pdf"
            };
            return fileResult;
        }

        public async Task<IActionResult> PrintDocuments(List<Document> documents, Building building, bool send)
        {
            if (documents != null && documents.Any())
            {
                double total = 0;
                double owes = 0;
                double demands = 0;

                if (building.CustomerRefId != null && building.Customers != null && building.Customers.Any() && building.Customers.Count() <= 1)
                {
                    total = (double)await _unitOfWork.Documents.Query().Where(x => x.CustomerId == building.CustomerRefId && x.PaymentStatus == PaymentStatus.Неплатено).SumAsync(x => x.TotalOutput);
                }
                else
                {
                    var results = GetFilteredBookFinancials(1201, building.Id).ToList();
                    demands = results.Where(x => !x.DontSum).Sum(su => su.Demands);
                    owes = results.Where(x => !x.DontSum).Sum(su => su.Owes);

                    total = owes - demands;
                }

                PdfDocument endDoc = new PdfDocument();
                MemoryStream pdfStream = new MemoryStream();

                foreach (var item in documents)
                {
                    var document = App.FullMapper.Map<DocumentViewModel>(item);
                    if (document == null) continue;

                    document.TotalBuildingDemands = (int)demands;
                    document.TotalBuildingOwes = (int)owes;
                    var debt = _unitOfWork.Documents.GetAll().Where(x => x.CustomerId == item.CustomerId && x.PaymentStatus != 0);

                    if (debt != null && debt.Any())
                    {
                        var allParts = debt
                             .OrderBy(x => x.Id)
                             .SelectMany(x => (x.ToDocument ?? (x.DateReceived?.ToString("yyyy-MM") ?? string.Empty)).Split(',', StringSplitOptions.RemoveEmptyEntries))
                             .ToList();

                        var distinctExceptLast = allParts
                            .Reverse<string>()
                            .Skip(1)
                            .Reverse()
                            .Distinct()
                            .ToList();

                        document.Debt = string.Join(",", distinctExceptLast) + " : " + debt.Sum(x => x.TotalOutput ?? 0);
                    }
                    document.Company = App.FullMapper.Map<CompanyConfig>(_config.Value);
                    document.IsForPdf = true;

                    if (send && !string.IsNullOrEmpty(document?.Customer?.Email))
                    {
                        await CreateAndSend(document);
                    }
                    else
                    {
                        string htmlContent = await RenderPartialViewToStringAsync("~/Views/Shared/_DocumentDetailPartialPrint.cshtml", document);

                        var request = _httpContextAccessor?.HttpContext?.Request;
                        string baseUrl = $"{request?.Scheme}://{request?.Host.Value}/";

                        HtmlToPdf converter = new HtmlToPdf();
                        PdfDocument doc = converter.ConvertHtmlString(htmlContent, baseUrl);

                        endDoc.Append(doc);
                    }
                }

                byte[] pdf = endDoc.Save();
                endDoc.Close();

                FileResult fileResult = new FileContentResult(pdf, "application/pdf");
                var dateOnly = documents.FirstOrDefault().Date;
                if (dateOnly != null)
                    fileResult.FileDownloadName = $"{building?.Name}_{dateOnly.Value.Month}_{dateOnly.Value.Year}.pdf";

                return fileResult;
            }
            return null;
        }

        private List<BookFinancialInfoViewModel> GetFilteredBookFinancials(int? invoiceId, int buildingId)
        {
            var query = _unitOfWork.BookFinancials.GetBuldingReserve(buildingId, invoiceId ?? 1201); // Заменет (int)InvoiceTyp.Reserve со 1201

            return query.Select(bf => new BookFinancialInfoViewModel
            {
                Id = bf.Id,
                Status = bf.Status,
                InvoiceId = bf.InvoiceId ?? 0,
                Description = bf.Description ?? "",
                DocumentTypId = bf.DocumentTypId ?? 0,
                DatumF = bf.DatumF ?? DateOnly.MinValue,
                Owes = bf.Owes,
                Demands = bf.Demands,
                DontSum = bf.DontSum
            }).ToList();
        }
    }
}