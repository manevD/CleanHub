using CleanHub.Attribute;
using CleanHub.CleanHub.Infrastructure.Data;
using CleanHub.Config;
using CleanHub.Entities;
using CleanHub.Extensions;
using CleanHub.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using SelectPdf;
using System.Net;
using System.Net.Mail;
using System.Reflection.PortableExecutable;

namespace CleanHub.Controllers
{
    [RequireLogin]
    public class BuildingsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly SMTPConfig _smtpConfig;
        private readonly CompanyConfig _config;
        private readonly ICompositeViewEngine _viewEngine;
        private readonly IWebHostEnvironment _env;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private static int Month = DateTime.Now.Month;
        private static int Year = DateTime.Now.Year;

        public BuildingsController(ICompositeViewEngine viewEngine, IWebHostEnvironment env,
            IHttpContextAccessor httpContextAccessor, ApplicationDbContext context, IOptions<SMTPConfig> smtpConfig,
            IOptions<CompanyConfig> config)
        {
            _httpContextAccessor = httpContextAccessor;
            _env = env;
            _viewEngine = viewEngine;
            _context = context;
            _smtpConfig = smtpConfig.Value;
            _config = config.Value;
        }

        private List<Building> GetBuildings()
        {
            var allBuildings = new List<Building> { new Building { Name = "Сите", Id = 0 } };
            allBuildings.AddRange(_context.Buildings.ToList());
            return allBuildings;
        }

        // GET: Buildings
        [Route("Згради")]
        public async Task<IActionResult> Index()
        {
            var buildingsJson = HttpContext.Session.GetString("Buildings");
            var settings = new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore };

            List<BuildingViewModel> buildings = string.IsNullOrEmpty(buildingsJson)
                ? App.FullMapper.Map<List<BuildingViewModel>>(await _context.Buildings.AsNoTracking().ToListAsync())
                : JsonConvert.DeserializeObject<List<BuildingViewModel>>(buildingsJson, settings);

            if (string.IsNullOrEmpty(buildingsJson))
                HttpContext.Session.SetString("Buildings", JsonConvert.SerializeObject(buildings, settings));

            return View(buildings);
        }

        // GET: Buildings/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var buildingEntity = await _context.Buildings
                .Include(x => x.Customers)
                .FirstOrDefaultAsync(c => c.Id == id);

            var buildingViewModel = App.FullMapper.Map<BuildingViewModel>(buildingEntity);
            if (buildingViewModel == null) return RedirectToAction(nameof(Index));

            if (buildingViewModel.Customers?.Any() == true)
                buildingViewModel.Customers = buildingViewModel.Customers
                    .OrderBy(c => c.CustomerInfo.ExtractNumberAfterSt())
                    .ToList();

            if (buildingViewModel != null)
            {
                List<BookFinancial> bookFinancial;

                if (buildingViewModel.CustomerRefId.HasValue)
                {
                    bookFinancial = await _context.BookFinancials
                        .Where(x => x.CustomerId == buildingViewModel.CustomerRefId.Value)
                        .ToListAsync();
                }
                else
                {
                    bookFinancial = await _context.BookFinancials
                        .Where(x => x.CustomerId == buildingViewModel.Id)
                        .ToListAsync();
                }

                buildingViewModel.ReserveTotal = (int)bookFinancial.Sum(x => x.Owes);
            }

            ViewBag.Month = Month;
            ViewBag.Year = Year;
            return View(buildingViewModel);
        }

        // GET: Buildings/Create
        public async Task<IActionResult> Create()
        {
            var buildingViewModel = new BuildingViewModel
            {
                BuildingProducts =
                    App.FullMapper.Map<List<BuildingProductViewModel>>(await _context.Products.ToListAsync())
            };
            return View(buildingViewModel);
        }

        // POST: Buildings/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(BuildingViewModel building)
        {
            building.BuildingProducts?.RemoveAll(x => string.IsNullOrEmpty(x.ArticleNotes));
            building.BuildingProducts?.ForEach(x => x.BuildingId = building.Id);

            if (ModelState.IsValid)
            {
                _context.Add(App.FullMapper.Map<Building>(building));
                await _context.SaveChangesAsync();
                HttpContext.Session.Remove("Buildings");

                return RedirectToAction(nameof(Index));
            }

            return View(building);
        }

        // GET: Buildings/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var buildingEntity = await _context.Buildings
                .Include(x => x.BuildingProducts)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (buildingEntity == null) return NotFound();
            var building = App.FullMapper.Map<BuildingViewModel>(buildingEntity);
            if (!building.BuildingProducts.Any())
            {
                building.BuildingProducts =
                    App.FullMapper.Map<List<BuildingProductViewModel>>(await _context.Products.ToListAsync());
            }

            return View(building);
        }

        // POST: Buildings/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, BuildingViewModel building)
        {
            if (id != building.Id) // Korrekte ID-Prüfung
            {
                return NotFound();
            }

            building.BuildingProducts?.ForEach(x => x.BuildingId = id);

            if (ModelState.IsValid)
            {
                try
                {
                    // Existierende BuildingProducts aus der Datenbank laden
                    var existingBuildingProducts = await _context.BuildingProducts
                        .Where(x => x.BuildingId == id)
                        .ToListAsync();

                    // Neue oder aktualisierte Produkte synchronisieren
                    foreach (var product in building.BuildingProducts)
                    {
                        var existingProduct = existingBuildingProducts.FirstOrDefault(bp => bp.Id == product.Id);
                        if (existingProduct != null)
                        {
                            // Produkt existiert -> Werte aktualisieren
                            _context.Entry(existingProduct).CurrentValues.SetValues(product);
                        }
                        else
                        {
                            // Neues Produkt hinzufügen
                            product.BuildingId = id;
                            var productEntity = App.FullMapper.Map<BuildingProduct>(product);
                            await _context.BuildingProducts.AddAsync(productEntity);
                        }
                    }

                    // Nicht mehr zugehörige Produkte entfernen
                    var updatedIds = building.BuildingProducts.Select(bp => bp.Id).ToList();
                    var productsToRemove = existingBuildingProducts
                        .Where(bp => !updatedIds.Contains(bp.Id))
                        .ToList();

                    if (productsToRemove.Any())
                    {
                        _context.BuildingProducts.RemoveRange(productsToRemove);
                    }

                    // Gebäude-Daten aktualisieren
                    var buildingToUpdate = App.FullMapper.Map<Building>(building);
                    buildingToUpdate.BuildingProducts.Clear(); // Produkte wurden schon separat behandelt
                    _context.Buildings.Update(buildingToUpdate);

                    // Änderungen speichern
                    await _context.SaveChangesAsync();
                    HttpContext.Session.Remove("Buildings");
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!BuildingExists(building.Id))
                    {
                        return NotFound();
                    }

                    throw;
                }

                return RedirectToAction(nameof(Index));
            }

            return View(building);
        }

        // GET: Buildings/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var building = await _context.Buildings.FindAsync(id);
            if (building == null) return NotFound();

            _context.Buildings.Remove(building);
            await _context.SaveChangesAsync();
            HttpContext.Session.Remove("Buildings");

            return RedirectToAction(nameof(Index));
        }

        private bool BuildingExists(int id) => _context.Buildings.Any(e => e.Id == id);

        [HttpPost, ActionName("SendInvoiceEmail")]
        public async Task<IActionResult> SendInvoiceEmail(int id, int month, int year)
        {
            var customers = _context.Buildings.Where(x => x.Id == id).Include(x => x.Customers)
                .SelectMany(d => d.Customers!).ToList();
            using (SmtpClient smtpClient = new SmtpClient(_smtpConfig.Server))
            {
                foreach (var item in customers.Where(x => x.Email != null))
                {
                    smtpClient.Credentials = new NetworkCredential(_smtpConfig.Email, _smtpConfig.Passwort);
                    smtpClient.EnableSsl = true;

                    var document = App.FullMapper.Map<DocumentViewModel>(_context.Documents.Include(x => x.Customer)
                        .Include(x => x.Books).FirstOrDefault(x =>
                            x.CustomerId == item.Id && x.Date!.Value.Year == year && x.Date!.Value.Month == month));
                    document.Company = _config;
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

                            Во прилог ви ја праќаме сметката за {month}/{year}. Ве молиме, проверете ја прикачената сметка и извршете ги потребните активности за плаќање.

                            Во случај на било какви прашања или недоразбирања можете слободно да не контактирате. Ви благодариме за Вашето внимание и соработка.
                            Со почит,

                            {_config.Name}
                            {_config.PhoneNumber}
                            {_config.Email}
                            {_config.Address}";
                    // create email message
                    MailMessage message = new MailMessage();
                    message.From = new MailAddress(_smtpConfig.Email);
                    message.To.Add(_smtpConfig.Recipient);
                    message.Subject = string.Concat("Сметка Марти Хигиена ", item.CustomerInfo, " за ", month, "/",
                        year);
                    message.Body = emailBody;
                    message.Attachments.Add(new Attachment(pdfStream,
                        string.Concat("МартиХигиена", month, "/", year, ".pdf")));
                    smtpClient.UseDefaultCredentials = false;
                    // send email
                    smtpClient.Send(message);

                    // close pdf document
                    doc.Close();
                }
            }
            return RedirectToAction("Details", new { id = id });
        }
       
        private MemoryStream CombinePdfs(List<Stream> pdfStreams)
        { // Erstellt ein neues PDF-Dokument
            PdfDocument finalPdf = new PdfDocument();

            foreach (var stream in pdfStreams)
            {
                stream.Position = 0; // Sicherstellen, dass der Stream am Anfang ist

                // Lade das PDF aus dem Stream
                PdfDocument partPdf = new PdfDocument(PdfStandard.PdfA);

                // Hänge das geladene PDF an das finale PDF an
                finalPdf.Append(partPdf);

                // Schließe das Teildokument, um Ressourcen freizugeben
                partPdf.Close();
            }

            // Speicher das kombinierte PDF in einen MemoryStream
            MemoryStream outputStream = new MemoryStream();
            finalPdf.Save(outputStream);
            finalPdf.Close();

            // Setze den Stream zurück, damit er lesbar ist
            outputStream.Position = 0;

            return outputStream;
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