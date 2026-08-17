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
                // create email message
                MailMessage message = new MailMessage();
                message.From = new MailAddress(_smtpConfig.Value.Email);
                message.To.Add(customer.Email);
                message.Subject = "Известување Марти Хигиена";
                message.Body = emailBody;
                smtpClient.UseDefaultCredentials = false;
                // send email
                await smtpClient.SendMailAsync(message);
            }
        }
        [HttpPost]
        public async Task<IActionResult> Pay(int invoiceId, PaymentType PaymentType, DateTime PaymentDate, string PaymentNumber, string PaymentDescription)
        {
            // 1. Најди го документот во базата
            var document = await _unitOfWork.Documents.Query()
                .FirstOrDefaultAsync(x => x.Id == invoiceId);

            if (document == null)
            {
                return BadRequest("Сметката не е пронајдена.");
            }

            // 2. Постави ги вредностите на документот исто како во твојата за групно плаќање
            document.PaymentStatus = PaymentStatus.Платено;
            document.PaymentDate = DateOnly.FromDateTime(PaymentDate);
            document.PaymentDescription = PaymentDescription;
            document.PaymentType = PaymentType;
            document.PaymentNumber = PaymentNumber;

            // 3. Повикај ја твојата оригинална логика преку маперот 1:1
            var viewModel = App.FullMapper.Map<DocumentViewModel>(document);
            SetStatusPayment(viewModel);

            // 4. Зачувај ги промените
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
                message.To.Add(document?.Customer?.Email);
                message.Subject = string.Concat("Сметка Марти Хигиена ", document.Customer?.CustomerInfo, " за ", document.Date.Value.Month, "/",
                    document.Date.Value.Year);
                message.Body = emailBody;
                message.Attachments.Add(new Attachment(pdfStream,
                    string.Concat("МартиХигиена", document.Date.Value.Month, "/", document.Date.Value.Year, ".pdf")));
                smtpClient.UseDefaultCredentials = false;
                // send email
                await smtpClient.SendMailAsync(message);
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
        public IActionResult Index(bool? fromPaymentStatus)
        {
            if (!fromPaymentStatus.HasValue || !fromPaymentStatus.Value)
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
            }
            else
            {
                var selectedId = TempData["SelectedBuildingId"] as int?;
                var selectedName = TempData["SelectedBuildingName"] as string;

                ViewBag.Buildings = new SelectList(Buildings, "Id", "Name", selectedId);

                {
                    ViewBag.Buildings = new SelectList(Buildings, "Id", "Name", selectedId);
                    ViewBag.SelectedBuildingName = selectedName;
                    ViewBag.BuildingId = selectedId;
                }

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

            // ✅ Payment Status
            ViewBag.PaymentStatusList = Enum.GetValues(typeof(PaymentStatus))
                .Cast<PaymentStatus>()
                .Select(e => new SelectListItem
                {
                    Text = e.GetEnumDescription(),
                    Value = ((int)e).ToString(),
                    Selected = (int)e == paymentStatusId
                })
                .ToList();

            // ✅ Building
            var building = await _unitOfWork.Buildings.GetByIdAsync(x => x.Id == buildingId.Value, inc => inc.Include(c => c.Customers));
            if (building != null)
            {
                ViewBag.Buildings = new SelectList(Buildings, "Id", "Name", building.Id);
                ViewBag.SelectedBuildingName = building.Name;
                ViewBag.BuildingId = building.Id;
                ViewBag.Customers = building.Customers.ToList();
                var bookFinancials = _unitOfWork.BookFinancials
      .GetAllNoTrakcing(
          query => query
              .Include(bf => bf.Customer)
              .Where(bf =>
                  bf.Customer.BuildingId == buildingId.Value
                  &&
                  (
                      bf.InvoiceId == (int)InvoiceTyp.Recieve
                      ||
                      bf.DocumentTypId == 11
                  )
                  &&
                  bf.InvoiceId != (int)InvoiceTyp.Reserve
              )
      )
      .ToList();


                var dataDocument = _unitOfWork.Documents.GetAllNoTrakcing(query => query
                            .Include(bf => bf.Customer)        // Include Customer
                            .Where(bf => bf.Customer.BuildingId == buildingId.Value)).ToList();
                ViewBag.Documents = dataDocument;

                ViewBag.BookFinancials = bookFinancials;

                var customerBalances = building.Customers.Select(c =>
                {
                    // ======================
                    // Documents vom Customer
                    // ======================
                    var customerDocs = dataDocument
                        .Where(x => x.CustomerId == c.Id)
                        .ToList();


                    // ======================
                    // BookFinancial vom Customer
                    // gleiche Filter sind schon oben geladen
                    // ======================
                    var dataBookFinancial = bookFinancials
                        .Where(x => x.CustomerId == c.Id)
                        .ToList();


                    // ======================
                    // ПОБАРУВА
                    // IDENTISCH wie Detail
                    // ======================
                    var pobaruva = dataBookFinancial
                        .Sum(x => x.Demands);


                    // ======================
                    // ДОЛЖИ Dokumente
                    // IDENTISCH:
                    // dataDocument.Where(Date > 2021)
                    // ======================
                    double dolzi = customerDocs
                        .Where(x =>
                            x.Date.HasValue &&
                            x.Date.Value > new DateOnly(2021, 1, 1))
                        .Sum(x => (double)(x.TotalOutput ?? 0));


                    // ======================
                    // ДОЛЖИ BookFinancial
                    // IDENTISCH
                    // ======================
                    if (dataBookFinancial.Any(x =>
                            x.Owes != 0 &&
                            x.DatumF.HasValue &&
                            x.DatumF.Value >= new DateOnly(2021, 1, 1)))
                    {
                        dolzi += dataBookFinancial
                            .Where(x =>
                                x.Owes != 0 &&
                                x.DatumF.HasValue &&
                                x.DatumF.Value >= new DateOnly(2021, 1, 1))
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

                // An View übergeben
                ViewBag.CustomerBalances = customerBalances;
            }

            // ✅ Default Year = Current Year
            int selectedYear = year ?? DateTime.Now.Year;
            ViewBag.Year = selectedYear;

            // ❗ WICHTIG: Neue Methode mit Year verwenden
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


        //private int CalculateOverdueDays(DateOnly? dateReceived)
        //{
        //    var today = DateOnly.FromDateTime(DateTime.Now);
        //    if (dateReceived != null)
        //    {
        //        if (dateReceived >= today)
        //        {
        //            return 0;
        //        }
        //        return today.DayNumber - dateReceived.Value.DayNumber;
        //    }

        //    return 0;
        //}

        //private int CalculateNewTotal(DocumentViewModel doc)
        //{
        //    doc.ChargesInPercent = GetOverdueFeePercentage(doc.Delay.Value);
        //    if (doc.TotalOutput != null)
        //        return (int)Math.Round(doc.TotalOutput.Value * (doc.ChargesInPercent.Value / 100f), MidpointRounding.AwayFromZero);
        //    return 0;
        //}
        private async Task<List<Document>> GetDocumentsByYear(int? buildingId, int? paymentStatusId, int? year)
        {
            var query = await _unitOfWork.Documents.GetAllWithIncludeAsync(
                q => q.Include(d => d.Customer)
                      .ThenInclude(c => c.Building),

                d =>
                    (buildingId.GetValueOrDefault() == 0 || d.Customer.BuildingId == buildingId.Value) &&
                    (paymentStatusId == (int)PaymentStatus.Сите || (int)d.PaymentStatus == paymentStatusId)
            );

            // ✅ YEAR FILTER
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
        // GET: Invoices/Details/5
        public async Task<IActionResult> JustSetDocumentPayedStatus(
      int id,
      int? year,
      int? buildingId,
      int? paymentStatusId,
      string buildingName)
        {
            {
                if (id == null)
                {
                    return NotFound();
                }
                var documentEntity = await _unitOfWork.Documents.GetByIdAsync(xd => xd.Id == id);
                if (documentEntity == null)
                {
                    return NotFound();
                }
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
        }

        public async Task<IActionResult> JustSetDocumentNotPayedStatus(int id, int? year, int? buildingId, int? paymentStatusId, string buildingName)
        {
            {
                if (id == null)
                {
                    return NotFound();
                }
                var documentEntity = await _unitOfWork.Documents.GetByIdAsync(xd => xd.Id == id);
                if (documentEntity == null)
                {
                    return NotFound();
                }
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
            //documentViewModel.Delay = CalculateOverdueDays(documentViewModel.DateReceived);
            //if (documentViewModel.PaymentStatus == PaymentStatus.Платено)
            //{
            //    documentViewModel.NewTotal = (int?)documentViewModel.TotalOutput;
            //}
            //else
            //{
            //    documentViewModel.NewTotal = (int?)(documentViewModel.TotalOutput + CalculateNewTotal(documentViewModel));
            //}
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
                    .Reverse<string>()                    // Umkehren
                    .Skip(1)                              // Letztes Element ignorieren
                    .Reverse()                            // Wieder umkehren
                    .Distinct()
                    .ToList();
                if (distinctExceptLast != null && distinctExceptLast.Any() && distinctExceptLast.Count() >= 8)
                {
                    documentViewModel.Debt = " : " + debt.Sum(x => x.TotalOutput ?? 0);
                }
                else
                {
                    documentViewModel.Debt = string.Join(",", distinctExceptLast)
                                       + " : " + debt.Sum(x => x.TotalOutput ?? 0);
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

            // =========================
            // Kunden laden
            // =========================
            var customers = _unitOfWork.Customers.GetAll()
                .Select(x => new Customer
                {
                    Id = x.Id,
                    CustomerInfo = x.CustomerInfo
                })
                .ToList();

            // Fallback für Auswahl
            var selectedCustomer = customers
                .FirstOrDefault(x => x.Id == customerId)
                ?? customers.FirstOrDefault();

            ViewBag.SelectedCustomerName = selectedCustomer?.CustomerInfo;

            // Firma setzen
            documentViewModel.Company = _config.Value;

            // =========================
            // Dropdown setzen (immer!)
            // =========================
            ViewBag.Customers = new SelectList(
                customers,
                "Id",
                "CustomerInfo",
                selectedCustomer?.Id
            );

            // 👉 Wenn kein Kunde gewählt → nur leeres ViewModel zurückgeben
            if (!customerId.HasValue)
            {
                return View(documentViewModel);
            }

            // =========================
            // Customer inkl. Building laden
            // =========================
            var customer = _unitOfWork.Customers
                .GetAll(include: inc => inc
                    .Include(x => x.Building)
                    .ThenInclude(x => x.BuildingProducts))
                .FirstOrDefault(x => x.Id == customerId);

            if (customer == null)
            {
                // optional: Fehler anzeigen oder einfach leeres View zurück
                return View(documentViewModel);
            }

            documentViewModel.CustomerId = customerId;

            // =========================
            // Building mappen
            // =========================
            if (customer.Building != null)
            {
                documentViewModel.Building =
                    App.FullMapper.Map<BuildingViewModel>(customer.Building);
            }

            // 👉 ABSICHERUNG (sehr wichtig!)
            documentViewModel.Building ??= new BuildingViewModel();

            // =========================
            // Products laden wenn leer
            // =========================
            if (!(documentViewModel.Building.BuildingProducts?.Any() ?? false))
            {
                var products = await _unitOfWork.Products.GetAllAsync();

                documentViewModel.Building.BuildingProducts =
                    App.FullMapper.Map<List<BuildingProductViewModel>>(products);
            }

            // 👉 ReserveFund anwenden
            var reserveFund = documentViewModel.Building.ReserveFund;

            if (reserveFund != null)
            {
                foreach (var product in documentViewModel.Building.BuildingProducts
                             .Where(p => p.ArticleNotes?.Contains("Резервен") == true))
                {
                    product.Price = reserveFund.Value;
                }
            }

            // =========================
            // BuildingId setzen
            // =========================
            documentViewModel.BuildingId = documentViewModel.Building.Id;

            return View(documentViewModel);
        }

        [HttpPost]
        [Route("креирајФактураЗаСтанар")]
        public async Task<IActionResult> CreateForCustomer(DocumentViewModel documentViewModel, string actionType)
        {
            var customer = await _unitOfWork.Customers.GetByIdAsync(x => x.Id == documentViewModel.CustomerId);
            var toDocument =
             DocumentService.GetMonthAsString(documentViewModel.Date.Value.Month) +
             " " +
             documentViewModel.Date.Value.Year;

            var exists = _unitOfWork.Documents
                .GetAll()
                .Any(d => d.CustomerId == customer.Id && d.ToDocument == toDocument);

            if (exists)
            {
                TempData["InvoiceExists"] =
                    "За овој месец веќе постои фактура за овој станар.";

                return RedirectToAction(nameof(CreateForCustomer),
                    new { customerId = customer.Id });
            }
            documentViewModel.BuildingId = customer.BuildingId;
            var buildingProductsFromBuilding = _unitOfWork.Buildings.GetAllBuildingProducts(documentViewModel.BuildingId.Value).ToList();
            var buildingProdutsToRemove = documentViewModel.Building?.BuildingProducts
                .Where(x => string.IsNullOrWhiteSpace(x.ArticleNotes)).ToList();
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
                foreach (var buildingProduct in documentViewModel.Building.BuildingProducts.Where(x => x.PriceWithTax != 0))
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

            return RedirectToAction("Create", new
            {
                id = id,
                buildingId = buildingId,
                date = formattedDate
            });
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

            // 1. Повлечи ги сите објекти само со Id и Name (за потребите на datalist-от)
            var buildings = (List<Building>)await _unitOfWork.Buildings.GetAllAsync(
                query => query.Select(b => new Building()
                {
                    Id = b.Id,
                    Name = b.Name,
                    CustomerRefId = b.CustomerRefId
                }));

            documentViewModel.Buildings = App.FullMapper.Map<List<BuildingViewModel>>(buildings);

            // 2. Одреди го точното BuildingId (ако нема, земи го првиот објект од листата)
            int targetBuildingId = buildingId ?? buildings.FirstOrDefault()?.Id ?? 0;
            documentViewModel.BuildingId = targetBuildingId;

            documentViewModel.Building = documentViewModel.Buildings.FirstOrDefault(x => x.Id == targetBuildingId) ?? new BuildingViewModel();

            if (documentViewModel.Building.BuildingProducts == null)
            {
                documentViewModel.Building.BuildingProducts = new List<BuildingProductViewModel>();
            }

            // Поставување на почетни дефолт вредности за продуктите ако се null
            foreach (var product in documentViewModel.Building.BuildingProducts)
            {
                if (product.PriceWithTax == null) product.PriceWithTax = 0;
                if (product.Total == null) product.Total = 0;
                if (product.Tax == null) product.Tax = 0;
            }

            // Подготовка на ViewBag за погледот
            ViewBag.Buildings = new SelectList(buildings, "Id", "Name", documentViewModel.BuildingId);
            ViewBag.SelectedBuildingName = documentViewModel.Building?.Name;
            ViewBag.BuildingId = documentViewModel.BuildingId;

            // Финансиски пресметки
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

            bool send = actionType == "send";
            ModelState.Remove(nameof(actionType));
            if (ModelState.IsValid)
            {
                var building = await _unitOfWork.Buildings.GetByIdAsync(x => x.Id == document.BuildingId,
                      inc => inc.Include(x => x.Customers));

                var toDocument = DocumentService.GetMonthAsString(document.Date.Value.Month) + " " + document.Date.Value.Year;

                if (building.Customers.Any())
                {
                    var customer = building.Customers.Where(x => x.ActivityId == 3).FirstOrDefault();
                    if (customer != null)
                    {
                        var exists = await _unitOfWork.Documents.AnyAsync(
                            x => x.CustomerId == customer.Id &&
                                 x.ToDocument == toDocument
                        );

                        if (exists)
                        {
                            TempData["InvoiceExists"] =
                                "За овој месец веќе постои фактура за оваа зграда.";

                            return RedirectToAction(nameof(Create),
                                new { id = 0, buildingId = building.Id });
                        }
                    }
                }

                if (document.BuildingId == null || document.BuildingId == 0)
                {
                    document.BuildingId = 1;
                }

                ViewBag.Buildings = new SelectList(Buildings, "Id", "Name", building.Id);

                var selectedBuildingName = Buildings?.FirstOrDefault(x => x.Id == building.Id)?.Name;
                if (selectedBuildingName != null)
                    ViewBag.SelectedBuildingName = selectedBuildingName;
                var buildingProductsFromBuilding = _unitOfWork.Buildings.GetAllBuildingProducts(document.BuildingId.Value).ToList();
                var buildingProdutsToRemove = document.Building?.BuildingProducts
                    .Where(x => string.IsNullOrWhiteSpace(x.ArticleNotes)).ToList();
                if (buildingProdutsToRemove != null && buildingProdutsToRemove.Any())
                {
                    foreach (var buildingProduct in buildingProdutsToRemove)
                    {
                        document.Building?.BuildingProducts.Remove(buildingProduct);
                    }
                }

                //if (document.Building?.BuildingProducts != null && document.Building?.BuildingProducts.Any() == true)
                //{
                //    var productsToAdd = document.Building?.BuildingProducts.ToList();
                //    if (!building.Customers.Any(x => x.SetCost) && productsToAdd != null && productsToAdd.Any())
                //    {
                //        foreach (var product in productsToAdd.Where(x => x.IsNew))
                //        {
                //            var bookFinancialViewModelReserve = new BookFinancialViewModel
                //            {
                //                InvoiceId = Constants.Reserve,
                //                Demands = 0,
                //                DocumentTypId = 5,
                //                Owes = PriceHelper.CalculatePriceWithTax(product.Price, product.Tax),
                //                DatumF = DateOnly.FromDateTime(document.Date.Value.ToDateTime(TimeOnly.MinValue).AddDays(10)),
                //                CustomerId = building.CustomerRefId,
                //                Status = PaymentStatus.Неплатено,
                //                Time = DateTime.Now,
                //                Description = product.ArticleNotes,
                //            };
                //            var bookFinancialReserve = App.FullMapper.Map<BookFinancial>(bookFinancialViewModelReserve);
                //            _unitOfWork.BookFinancials.Add(bookFinancialReserve);
                //        }
                //    }
                //}

                var documents = new List<Document>();

                if (building != null)
                {
                    var hasSetCost = building?.Customers?.Any(x => x.SetCost) == true;
                    foreach (var customer in building.Customers.Where(x => x.Inactive == false && !x.Hide).ToList())
                    {
                        var docEntity = await CreateCustomerDocument(customer, document, building);

                        if (document?.Building?.BuildingProducts != null)
                        {

                            var allProducts = document?.Building?.BuildingProducts?
                                .Where(x => x.PriceWithTax != 0).ToList()
                                ?? new List<BuildingProductViewModel>();

                            List<BuildingProductViewModel> productsForCustomer = new List<BuildingProductViewModel>();

                            if (!hasSetCost)
                            {
                                // ✔ никој нема SetCost → сите добиваат се
                                productsForCustomer = allProducts;
                            }
                            else
                            {
                                if (customer.SetCost)
                                {
                                    // ✔ тие со SetCost → СÈ
                                    productsForCustomer = allProducts;
                                }
                                else
                                {
                                    // ✔ другите → само стари
                                    productsForCustomer = allProducts.Where(x => !x.IsNew).ToList();
                                }
                            }


                            if (!customer.PresmetajAdministrativniTrosoci)
                            {
                                productsForCustomer?.RemoveAll(x =>
                                    x.ArticleNotes != null &&
                                    x.ArticleNotes.Contains("административни трошоци", StringComparison.OrdinalIgnoreCase));
                            }

                            if (!customer.PresmetajKomunalnaTaksaJavnoOsvetluvanje)
                            {
                                productsForCustomer?.RemoveAll(x =>
                                    x.ArticleNotes != null &&
                                    x.ArticleNotes.Contains("комунална такса за јавно осветлување", StringComparison.OrdinalIgnoreCase));
                            }

                            if (!customer.PresmetajOdrzuvanjeLift)
                            {
                                productsForCustomer?.RemoveAll(x =>
                                    x.ArticleNotes != null &&
                                    x.ArticleNotes.Contains("одржување на лифт", StringComparison.OrdinalIgnoreCase));
                            }

                            if (!customer.PresmetajOdrzuvanjeSmetki)
                            {
                                productsForCustomer?.RemoveAll(x =>
                                    x.ArticleNotes != null &&
                                    x.ArticleNotes.Contains("одржување на сметки", StringComparison.OrdinalIgnoreCase));
                            }

                            if (!customer.PresmetajPotrosenaElektricnaEnergija)
                            {
                                productsForCustomer?.RemoveAll(x =>
                                    x.ArticleNotes != null &&
                                    x.ArticleNotes.Contains("потрошена електрична енергија", StringComparison.OrdinalIgnoreCase));
                            }

                            if (!customer.PresmetajRezervenFond)
                            {
                                productsForCustomer?.RemoveAll(x =>
                                    x.ArticleNotes != null &&
                                    x.ArticleNotes.Contains("резервен фонд", StringComparison.OrdinalIgnoreCase));
                            }

                            if (!customer.PresmetajUpravitel)
                            {
                                productsForCustomer?.RemoveAll(x =>
                                    x.ArticleNotes != null &&
                                    x.ArticleNotes.Contains("управител", StringComparison.OrdinalIgnoreCase));
                            }

                            if (!customer.PresmetajCistenjeVlez)
                            {
                                productsForCustomer?.RemoveAll(x =>
                                    x.ArticleNotes != null &&
                                    x.ArticleNotes.Contains("чистење на влез", StringComparison.OrdinalIgnoreCase));
                            }
                            foreach (var buildingProduct in productsForCustomer)
                            {
                                try
                                {
                                    if (buildingProduct.ArticleNotes?.Contains("гаража") == true && !customer.Garage)
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
                        }
                        var docViewModel = App.FullMapper.Map<DocumentViewModel>(docEntity);
                        if (docViewModel.PaymentStatus != PaymentStatus.Платено && customer.Subscription.HasValue && customer.Subscription != 0 && customer.Subscription < docViewModel.TotalOutput && !string.IsNullOrEmpty(customer.Email))
                        {
                            SendNotificationMail(customer, docViewModel.ToDocument);
                        }
                        CreateBookFinancialAndReserve(docEntity, customer.Id, building.ReserveFund ?? 0, document.PaymentDate, document.PaymentType, document.PaymentNumber);

                        documents.Add(docEntity);
                    }
                }

                CreateSpecialInvoice(document);
                await _unitOfWork.SaveChangesAsync();
                if (documents != null && documents.Any())
                {
                    List<DokumentiTest> mappedDocuments = documents.Select(d => new DokumentiTest
                    {
                        Dokid = d.Id.ToString(),
                        Datum = d.Date.Value.ToString("yyyy-MM-dd"),
                        Broj = d.Number.ToString(),
                        PartnerID = d.CustomerId.ToString(),
                        Godina = d.Date.Value.Year.ToString(),
                        VkupnoIz = d.TotalOutput.ToString()
                    }).ToList();

                    _context.DokumentiTest.AddRange(mappedDocuments);
                    _context.SaveChanges();
                }

                HttpContext.Session.Remove("Documents");

                return await PrintDocuments(documents, building, send);
            }

            return View(document);
        }


        /// <summary>
        /// 0 Zbirna 1 Poedinecna
        /// </summary>
        /// <param name="id"></param>
        /// <param name="buildingId"></param>
        /// <returns></returns>
        [HttpGet]
        public async Task<IActionResult> Create(int? id, int? buildingId, DateTime? date)
        {
            var documentViewModel = new DocumentViewModel();

            ViewBag.RouteId = id;

            var now = DateTime.UtcNow;

            if (!date.HasValue)
            {
                var lastDayOfLastMonth =
                    new DateTime(now.Year, now.Month, 1).AddDays(-1);

                documentViewModel.Date =
                    DateOnly.FromDateTime(lastDayOfLastMonth);
            }
            else
            {
                documentViewModel.Date =
                    DateOnly.FromDateTime(date.Value);
            }

            documentViewModel.Company = _config.Value;

            var buildings = new List<Building>();

            // ====================================================
            // ROUTE 2
            // ====================================================

            if (id == 2)
            {
                buildings = (List<Building>)await _unitOfWork.Buildings.GetAllAsync(
                    query => query.Select(b => new Building()
                    {
                        Id = b.Id,
                        Name = b.Name
                    }));

                documentViewModel.Buildings =
                    App.FullMapper.Map<List<BuildingViewModel>>(buildings);

                documentViewModel.Building =
                    buildingId.HasValue
                        ? documentViewModel.Buildings
                            .FirstOrDefault(x => x.Id == buildingId.Value)
                        : documentViewModel.Buildings.FirstOrDefault();

                if (documentViewModel.Building == null)
                {
                    documentViewModel.Building =
                        documentViewModel.Buildings.FirstOrDefault();
                }

                documentViewModel.BuildingId =
                    documentViewModel.Building?.Id ?? 0;

                documentViewModel.Building.BuildingProducts =
                    new List<BuildingProductViewModel>();
            }

            // ====================================================
            // ROUTE 0 / 1
            // ====================================================

            else
            {
                buildings = (List<Building>)await _unitOfWork.Buildings.GetAllAsync(
                    query => query
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

                documentViewModel.Buildings =
                    App.FullMapper.Map<List<BuildingViewModel>>(buildings);

                documentViewModel.Building =
                    buildingId.HasValue
                        ? documentViewModel.Buildings
                            .FirstOrDefault(x => x.Id == buildingId.Value)
                        : documentViewModel.Buildings.FirstOrDefault();

                if (documentViewModel.Building == null)
                {
                    documentViewModel.Building =
                        documentViewModel.Buildings.FirstOrDefault();
                }

                documentViewModel.BuildingId =
                    documentViewModel.Building.Id;

                documentViewModel.Building.Customers =
                    documentViewModel.Building.Customers
                        .Where(x =>
                            !x.Hide &&
                            x.Inactive != true &&
                            x.ActiveDatum.HasValue &&
                            x.ActiveDatum <= documentViewModel.Date)
                        .ToList();

                // ====================================================
                // PRODUCTS
                // ====================================================

                var filteredProducts = (id == 0)
                    ? documentViewModel.Building.BuildingProducts
                    : documentViewModel.Building.BuildingProducts
                        .Where(x =>
                            x.ArticleNotes != null &&
                            x.ArticleNotes.Contains(
                                "влез",
                                StringComparison.OrdinalIgnoreCase))
                        .ToList();

                if (filteredProducts != null && filteredProducts.Any())
                {
                    documentViewModel.Building.BuildingProducts =
                        filteredProducts;
                }
                else
                {
                    var basicProducts = (id == 0)
                        ? await _unitOfWork.Products.GetAllAsync()
                        : await _unitOfWork.Products.GetAllAsync(
                            x => x.Where(p =>
                                p.ArticleNotes != null &&
                                p.ArticleNotes.Contains(
                                    "влез",
                                    StringComparison.OrdinalIgnoreCase)));

                    documentViewModel.Building.BuildingProducts =
                        App.FullMapper.Map<List<BuildingProductViewModel>>(basicProducts);

                    // ====================================================
                    // RESERVE FUND
                    // ====================================================

                    foreach (var product in documentViewModel.Building
                                 .BuildingProducts
                                 .Where(p =>
                                     p.ArticleNotes != null &&
                                     p.ArticleNotes.Contains(
                                         "Резервен",
                                         StringComparison.OrdinalIgnoreCase)))
                    {
                        if (documentViewModel.Building.ReserveFund != null)
                        {
                            product.Price =
                                documentViewModel.Building.ReserveFund.Value;
                        }
                    }
                }
            }

            // ====================================================
            // SAFETY CHECK
            // ====================================================

            if (documentViewModel.Building?.BuildingProducts == null)
            {
                documentViewModel.Building.BuildingProducts =
                    new List<BuildingProductViewModel>();
            }

            // ====================================================
            // INITIAL VALUES
            // ====================================================

            foreach (var product in documentViewModel.Building.BuildingProducts)
            {
                if (product.PriceWithTax == null)
                    product.PriceWithTax = 0;

                if (product.Total == null)
                    product.Total = 0;
            }

            // ====================================================
            // VIEWBAGS
            // ====================================================

            ViewBag.Buildings = new SelectList(
                buildings,
                "Id",
                "Name",
                documentViewModel.BuildingId);

            ViewBag.SelectedBuildingName =
                documentViewModel.Building?.Name;

            ViewBag.BuildingId =
                documentViewModel.BuildingId;

            // ====================================================
            // FINANCIALS
            // ====================================================

            var results = GetFilteredBookFinancials(
                1201,
                documentViewModel.BuildingId.Value)
                .ToList();

            documentViewModel.TotalBuildingDemands =
                results.Where(x => !x.DontSum)
                       .Sum(x => x.Demands);

            documentViewModel.TotalBuildingOwes =
                results.Where(x => !x.DontSum)
                       .Sum(x => x.Owes);

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
                        InvoiceId = (int)InvoiceTyp.Reserve,
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
        public async Task<IActionResult> CreateBookFinancial( PaymentType PaymentType, DateTime PaymentDate, string PaymentNumber, string PaymentDescription, int Total,int customerId)
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

            // 2. Враќаш OK статус бидејќи ова ќе биде повикано преку AJAX
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

            TempData["Success"] = $"{ids.Count} фактури се успешно избришани.";

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

                var documents = _context.DokumentiTest
                    .Where(x => idsDoc.Contains(x.Dokid));
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
                var existsInBookFinancials = AlreadyPayedInBookFinancials(
                    bookFinancials,
                    customerId,
                    docEntity.ToDocument);
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
                        PaymentDate = paymentDate.HasValue && paymentDate.Value != DateOnly.MinValue
                                  ? paymentDate.Value
                                  : DateOnly.FromDateTime(DateTime.UtcNow),
                        PaymentType = docEntity.PaymentType.Value,
                        Description = string.IsNullOrEmpty(docEntity.PaymentDescription)
                                  ? docEntity.PaymentType.GetEnumDescription()
                                  : docEntity.PaymentDescription,
                        PaymentNumber = paymentNumber
                    };
                    CreateReserve(docEntity, customerId, reserve, paymentDate, paymentType, paymentNumber);
                    var bookFinancial = App.FullMapper.Map<BookFinancial>(bookFinancialViewModel);
                    _unitOfWork.BookFinancials.Add(bookFinancial);
                }
            }
            //else
            //{
            //    bookFinancialViewModel = new BookFinancialViewModel
            //    {
            //        InvoiceId = Constants.Recieve,
            //        DocumentId = docEntity.Id,
            //        Demands = 0,
            //        Owes = docEntity!.TotalOutput!.Value!,
            //        DocumentTypId = 4,
            //        CustomerId = customerId,
            //        Time = DateTime.Now,
            //        Status = PaymentStatus.Неплатено,
            //        DatumF = docEntity.DateReceived,
            //        Description = string.Empty,
            //    };
            //}
        }

        public void CreateReserve(Document docEntity, int customerId, int reserve, DateOnly? paymentDate,
            PaymentType? paymentType, string? paymentNumber)
        {
            var bookFinancialViewModelReserve = new BookFinancialViewModel();

            if (docEntity.PaymentStatus == PaymentStatus.Платено)
            {
                bookFinancialViewModelReserve = new BookFinancialViewModel()
                {
                    InvoiceId = Constants.Reserve,
                    DocumentId = docEntity.Id,
                    Demands = docEntity.Books.FirstOrDefault(x => x.ArticleNotes.Contains("Резервен фонд"))?.Total ?? 0.0,
                    DocumentTypId = 4,
                    Owes = 0,
                    DatumF = docEntity.DateReceived,
                    CustomerId = customerId,
                    Status = docEntity.PaymentStatus,
                    Time = DateTime.Now,
                    Description = string.IsNullOrEmpty(docEntity.PaymentDescription)
                    ? docEntity.PaymentType.GetEnumDescription()
                    : docEntity.PaymentDescription,
                    PaymentDate = paymentDate.HasValue && paymentDate.Value != DateOnly.MinValue
                    ? paymentDate.Value
                    : DateOnly.FromDateTime(DateTime.UtcNow),
                    PaymentType = docEntity.PaymentType.Value,
                    PaymentNumber = paymentNumber
                };
            }
            else
            {
                bookFinancialViewModelReserve = new BookFinancialViewModel()
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
            }

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
                Quantity = book.Quantity,

                PriceWithTax = book.PriceWithTax,

                Price = book.Price,

                Tax =  book.Tax,

                Total = book.PriceWithTax,

                ArticleNotes = book.ArticleNotes,
                UnitOfMeasurement = book.UnitOfMeasurement,
            };

            var entityBook = App.FullMapper.Map<Book>(bookEntity);
            docEntity.Books.Add(entityBook);
        }

        private async Task<Document> CreateCustomerDocument(Customer customer, DocumentViewModel document, Building building)
        {
            var documentCustomer = new DocumentViewModel
            {
                CustomerId = customer.Id,
                Number =
                    (await _unitOfWork.Documents.GetMaxAsync(x => x.Number) ?? 0) + 1,
                Date = document.Date
            };
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
            var calculator = new PriceCalculator(
                building.Customers.ToList());
            var tempBuildingProducts = document.Building?.BuildingProducts.ToList();

            var hasSetCost = building.Customers?.Any(x => x.SetCost) == true;

            // ✔ Garage filter
            if (!customer.Garage)
            {
                tempBuildingProducts = tempBuildingProducts?
                    .Where(x => x.ArticleNotes == null || !x.ArticleNotes.Contains("гаража"))
                    .ToList();
            }
           
            // ✔ SetCost логика
            if (hasSetCost && !customer.SetCost)
            {
                tempBuildingProducts = tempBuildingProducts?
                    .Where(x => !x.IsNew)
                    .ToList();
            }
            if (!customer.PresmetajAdministrativniTrosoci)
            {
                tempBuildingProducts?.RemoveAll(x =>
                    x.ArticleNotes != null &&
                    x.ArticleNotes.Contains("административни трошоци", StringComparison.OrdinalIgnoreCase));
            }

            if (!customer.PresmetajKomunalnaTaksaJavnoOsvetluvanje)
            {
                tempBuildingProducts?.RemoveAll(x =>
                    x.ArticleNotes != null &&
                    x.ArticleNotes.Contains("комунална такса за јавно осветлување", StringComparison.OrdinalIgnoreCase));
            }

            if (!customer.PresmetajOdrzuvanjeLift)
            {
                tempBuildingProducts?.RemoveAll(x =>
                    x.ArticleNotes != null &&
                    x.ArticleNotes.Contains("одржување на лифт", StringComparison.OrdinalIgnoreCase));
            }

            if (!customer.PresmetajOdrzuvanjeSmetki)
            {
                tempBuildingProducts?.RemoveAll(x =>
                    x.ArticleNotes != null &&
                    x.ArticleNotes.Contains("одржување на сметки", StringComparison.OrdinalIgnoreCase));
            }

            if (!customer.PresmetajPotrosenaElektricnaEnergija)
            {
                tempBuildingProducts?.RemoveAll(x =>
                    x.ArticleNotes != null &&
                    x.ArticleNotes.Contains("потрошена електрична енергија", StringComparison.OrdinalIgnoreCase));
            }

            if (!customer.PresmetajRezervenFond)
            {
                tempBuildingProducts?.RemoveAll(x =>
                    x.ArticleNotes != null &&
                    x.ArticleNotes.Contains("резервен фонд", StringComparison.OrdinalIgnoreCase));
            }

            if (!customer.PresmetajUpravitel)
            {
                tempBuildingProducts?.RemoveAll(x =>
                    x.ArticleNotes != null &&
                    x.ArticleNotes.Contains("управител", StringComparison.OrdinalIgnoreCase));
            }

            if (!customer.PresmetajCistenjeVlez)
            {
                tempBuildingProducts?.RemoveAll(x =>
                    x.ArticleNotes != null &&
                    x.ArticleNotes.Contains("чистење на влез", StringComparison.OrdinalIgnoreCase));
            }
            calculator.CalculatePrices(tempBuildingProducts, customer);
            // Calculate the total PriceWithTax sum
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
        private bool AlreadyPayedInBookFinancials(
     List<BookFinancial> bookFinancials,
     int customerId,
     string toDocument)
        {
            var months = new Dictionary<string, int>
    {
        { "Јануари", 1 },
        { "Февруари", 2 },
        { "Март", 3 },
        { "Април", 4 },
        { "Мај", 5 },
        { "Јуни", 6 },
        { "Јули", 7 },
        { "Август", 8 },
        { "Септември", 9 },
        { "Октомври", 10 },
        { "Ноември", 11 },
        { "Декември", 12 }
    };


            if (string.IsNullOrEmpty(toDocument))
                return false;


            var parts = toDocument.Split(' ');

            if (parts.Length < 2)
                return false;


            if (!months.TryGetValue(parts[0], out int month))
                return false;


            if (!int.TryParse(parts[1], out int year))
                return false;


            string yearText = year.ToString();


            var entries = bookFinancials
                .Where(x =>
                    x.CustomerId == customerId &&
                    !string.IsNullOrWhiteSpace(x.Description) &&
                    x.Description.Contains(yearText))
                .ToList();


            foreach (var bf in entries)
            {
                var desc = bf.Description.Trim();


                // ====================================
                // 01-08/2026
                // 1-8/2026
                // за 01-08/2026
                // од 01-08/2026
                // ====================================

                var range = Regex.Match(
                    desc,
                    @"(\d{1,2})\s*-\s*(\d{1,2})\s*/\s*" + yearText);

                if (range.Success)
                {
                    if (
                        int.TryParse(range.Groups[1].Value, out int from)
                        &&
                        int.TryParse(range.Groups[2].Value, out int to)
                    )
                    {
                        if (month >= from && month <= to)
                            return true;
                    }
                }


                // ====================================
                // 01,02,03,04,05/2026
                // 1,2,3,4,5/2026
                // ====================================

                var list = Regex.Match(
                    desc,
                    @"([\d,\s]+)\s*/\s*" + yearText);

                if (list.Success)
                {
                    var foundMonths = list.Groups[1]
                        .Value
                        .Split(',')
                        .Select(x =>
                        {
                            return int.TryParse(x.Trim(), out int m)
                                ? m
                                : -1;
                        })
                        .Where(x => x > 0)
                        .ToList();


                    if (foundMonths.Contains(month))
                        return true;
                }


                // ====================================
                // 05/2026
                // за 05/2026
                // ====================================

                var single = Regex.Match(
                    desc,
                    @"(\d{1,2})\s*/\s*" + yearText);


                if (single.Success)
                {
                    if (
                        int.TryParse(single.Groups[1].Value, out int singleMonth)
                        &&
                        singleMonth == month
                    )
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

                //if (model.NewTotal.HasValue && model.NewTotal != 0)
                //{
                //    documentToUpdate.NewTotal = model.NewTotal.Value;
                //}
                documentToUpdate.PaymentStatus = PaymentStatus.Платено;
                documentToUpdate.PaymentDate = model.PaymentDate;
                documentToUpdate.PaymentDescription = model.PaymentDescription;
                documentToUpdate.PaymentType = model.PaymentType;
                documentToUpdate.PaymentNumber = model.PaymentNumber;
                // Update related BookFinancials
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
                             .Any(x => x.ArticleNotes != null &&
                                 x.ArticleNotes.Contains("резервен фонд", StringComparison.OrdinalIgnoreCase)))
                    {
                        listToAdd.Add(new BookFinancial
                        {
                            InvoiceId = Constants.Reserve,
                            PaymentType = model.PaymentType,
                            Time = DateTime.UtcNow,
                            PaymentNumber = model.PaymentNumber,
                            DateTimeChanges = DateTime.UtcNow,
                            PaymentDate = model.PaymentDate,
                            Description = model.PaymentDescription,
                            DocumentId = documentToUpdate.Id,
                            Demands = documentToUpdate.Books
                                .FirstOrDefault(x => x.ArticleNotes != null &&
                                    x.ArticleNotes.Contains("резервен фонд", StringComparison.OrdinalIgnoreCase))
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
            catch (DbUpdateConcurrencyException e)
            {

            }
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
            var debt = _unitOfWork.Documents.GetAll().Where(x => x.CustomerId == document.CustomerId && x.PaymentStatus != 0);
            if (debt != null && debt.Any())
            {
                var allParts = debt.OrderBy(x => x.Id)
                    .SelectMany(x => x.ToDocument.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    .ToList();

                var distinctExceptLast = allParts
                    .Reverse<string>()                    // Umkehren
                    .Skip(1)                              // Letztes Element ignorieren
                    .Reverse()                            // Wieder umkehren
                    .Distinct()
                    .ToList();
                if (distinctExceptLast != null && distinctExceptLast.Any() && distinctExceptLast.Count() >= 8)
                {
                    document.Debt = " : " + debt.Sum(x => x.TotalOutput ?? 0);
                }
                else
                {
                    document.Debt = string.Join(",", distinctExceptLast)
                                       + " : " + debt.Sum(x => x.TotalOutput ?? 0);
                }
            }
            ViewData["CustomerId"] = new SelectList(_unitOfWork.Customers.GetAll().Where(x => !x.Hide), "Id", "Name", document.CustomerId);
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
            ViewData["CustomerId"] = new SelectList(_unitOfWork.Customers.GetAll().Where(x => !x.Hide), "Id", "Name", document.CustomerId);
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

        public async Task<IActionResult> CombineAndDownloadPdfs(int? buildingId, int? paymentStatusId, int year)
        {
            // Hole die Kunden, die dem Gebäude zugeordnet sind
            var customers = await _unitOfWork.Customers.GetCustomersByBuildingIdAsync(buildingId.Value);

            var startDate = new DateOnly();
            var endDate = new DateOnly();


            var results = GetFilteredBookFinancials(1201, buildingId.Value).ToList();
            var demands = results.Where(x => !x.DontSum).Sum(su => su.Demands);
            var owes = results.Where(x => !x.DontSum).Sum(su => su.Owes);


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
                document.TotalBuildingDemands = (int)demands;
                document.TotalBuildingOwes = (int)owes;
                var debt = _unitOfWork.Documents.GetAll().Where(x => x.CustomerId == document.CustomerId && x.PaymentStatus != 0);
                if (debt != null && debt.Any())
                {
                    var allParts = debt.OrderBy(x => x.Id)
                        .SelectMany(x => x.ToDocument.Split(',', StringSplitOptions.RemoveEmptyEntries))
                        .ToList();

                    var distinctExceptLast = allParts
                        .Reverse<string>()                    // Umkehren
                        .Skip(1)                              // Letztes Element ignorieren
                        .Reverse()                            // Wieder umkehren
                        .Distinct()
                        .ToList();

                    if (distinctExceptLast != null && distinctExceptLast.Any() && distinctExceptLast.Count() >= 8)
                    {
                        document.Debt = " : " + debt.Sum(x => x.TotalOutput ?? 0);
                    }
                    else
                    {
                        document.Debt = string.Join(",", distinctExceptLast)
                                           + " : " + debt.Sum(x => x.TotalOutput ?? 0);
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
                    if (document == null)
                    {
                        continue;
                    }
                    document.TotalBuildingDemands = (int)demands;
                    document.TotalBuildingOwes = (int)owes;
                    var debt = _unitOfWork.Documents.GetAll().Where(x => x.CustomerId == item.CustomerId && x.PaymentStatus != 0);
                    if (debt != null && debt.Any())
                    {
                        var allParts = debt
                             .OrderBy(x => x.Id)
                             .SelectMany(x =>
                                 (x.ToDocument ??
                                  (x.DateReceived?.ToString("yyyy-MM") ?? string.Empty))
                                 .Split(',', StringSplitOptions.RemoveEmptyEntries))
                             .ToList();

                        var distinctExceptLast = allParts
                            .Reverse<string>()                    // Umkehren
                            .Skip(1)                              // Letztes Element ignorieren
                            .Reverse()                            // Wieder umkehren
                            .Distinct()
                            .ToList();

                        document.Debt = string.Join(",", distinctExceptLast)
                                                 + " : " + debt.Sum(x => x.TotalOutput ?? 0);
                    }
                    document.Company = App.FullMapper.Map<CompanyConfig>(_config.Value);
                    document.IsForPdf = true;

                    if (send && !string.IsNullOrEmpty(document?.Customer?.Email))
                    {
                        await CreateAndSend(document);
                    }
                    else
                    {
                        string htmlContent = await RenderPartialViewToStringAsync(
                            "~/Views/Shared/_DocumentDetailPartialPrint.cshtml", document);

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
                    fileResult.FileDownloadName =
                        $"{building?.Name}_{dateOnly.Value.Month}_{dateOnly.Value.Year}.pdf";
                return fileResult;
            }
            return null;
        }
        private List<BookFinancialInfoViewModel> GetFilteredBookFinancials(int? invoiceId, int buildingId)
        {
            var query = _unitOfWork.BookFinancials.GetBuldingReserve(buildingId, invoiceId ?? (int)InvoiceTyp.Reserve);

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