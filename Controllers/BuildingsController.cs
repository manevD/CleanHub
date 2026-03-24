using CleanHub.Attribute;
using CleanHub.Config;
using CleanHub.Entities;
using CleanHub.Extensions;
using CleanHub.Infrastructure.Data;
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

namespace CleanHub.Controllers
{
    [RequireLogin]
    public class BuildingsController(ICompositeViewEngine _viewEngine,
        IHttpContextAccessor _httpContextAccessor, IOptions<SMTPConfig> _smtpConfig,
        IOptions<CompanyConfig> _config, IUnitOfWork _unitOfWork) : Controller
    {
        private static int _month = DateTime.Now.Month;
        private static int _year = DateTime.Now.Year;

        //private async Task<List<Building>> GetBuildings()
        //{
        //    var allBuildings = new List<Building> { new Building { Name = "Сите", Id = 0 } };
        //    allBuildings.AddRange(await _unitOfWork.Buildings.GetAllAsync());
        //    return allBuildings;
        //}

        // GET: Buildings
        [Route("Згради")]
        [Route("")]
        public IActionResult Index()
        {
            var buildingsJson = HttpContext.Session.GetString("Buildings");
            var settings = new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore };

            List<BuildingViewModel>? buildings = string.IsNullOrEmpty(buildingsJson)
                ? App.FullMapper.Map<List<BuildingViewModel>>(_unitOfWork.Buildings.GetAll())
                : JsonConvert.DeserializeObject<List<BuildingViewModel>>(buildingsJson, settings);

            if (string.IsNullOrEmpty(buildingsJson))
                HttpContext.Session.SetString("Buildings", JsonConvert.SerializeObject(buildings, settings));

            return View(buildings);
        }

        // GET: Buildings/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var buildingEntity = await _unitOfWork.Buildings.GetByIdAsync(c => c.Id == id, x => x
                .Include(x => x.Customers.Where(c => !c.Hide)));

            var buildingViewModel = App.FullMapper.Map<BuildingViewModel>(buildingEntity);
            if (buildingViewModel == null) return RedirectToAction(nameof(Index));

            if (buildingViewModel.Customers?.Any() == true)
                buildingViewModel.Customers = buildingViewModel.Customers
                    .OrderBy(c =>
                    {
                        if (c.CustomerInfo != null) return c.CustomerInfo.ExtractNumberAfterSt();
                        return 0;
                    })
                    .ToList();

            if (buildingViewModel != null &&  buildingViewModel.Customers.Any())
            {
                var owes = _unitOfWork.BookFinancials.GetOwes(buildingViewModel.Id);
                var demands = _unitOfWork.BookFinancials.GetDemands(buildingViewModel.Id);
                buildingViewModel.ReserveTotal = (demands - owes);
            }

            ViewBag.Month = _month;
            ViewBag.Year = _year;
            return View(buildingViewModel);
        }

        // GET: Buildings/Create
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var buildingViewModel = new BuildingViewModel
            {
                BuildingProducts =
                    App.FullMapper.Map<List<BuildingProductViewModel>>(await _unitOfWork.Products.GetAllAsync())
            };
            buildingViewModel.BuildingProducts.AddRange(InitializeDefaultBuildingProducts());

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
                var buildingEntity = App.FullMapper.Map<Building>(building);

                var customer = new Customer
                {
                    CustomerInfo = building.Name,
                    ActivityId = 2,
                    Adress = building.Name,
                    Inactive = false,
                    Building = buildingEntity,
                    Hide = true
                };

                _unitOfWork.Customers.Add(customer);
                _unitOfWork.Buildings.Add(buildingEntity);
                await _unitOfWork.SaveChangesAsync();
                buildingEntity.CustomerRefId = customer.Id;
                 await _unitOfWork.SaveChangesAsync();
                HttpContext.Session.Remove("Buildings");

                return RedirectToAction(nameof(Index));
            }

            return View(building);
        }
        private List<BuildingProductViewModel> InitializeDefaultBuildingProducts()
        {
            return Enumerable.Repeat(new BuildingProductViewModel
            {
                Id = 0,
                Input = 0,
                Output = 0,
                Quantity = 1,
                PriceWithTax = 0,
                Tax = 18,
                Total = 0,
                Price = 0,
                ArticleNotes = string.Empty,
                UnitOfMeasurement = "бр."
            }, 4).ToList();
        }
        // GET: Buildings/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var buildingEntity = await _unitOfWork.Buildings.GetByIdAsync(c => c.Id == id, inc => inc
                .Include(x => x.BuildingProducts));

            if (buildingEntity == null) return NotFound();
            var building = App.FullMapper.Map<BuildingViewModel>(buildingEntity);
            if (!building.BuildingProducts.Any())
            {
                building.BuildingProducts =
                    App.FullMapper.Map<List<BuildingProductViewModel>>(await _unitOfWork.Products.GetAllAsync());
            }

            building.BuildingProducts.AddRange(InitializeDefaultBuildingProducts());

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
                    var existingBuildingProducts = await _unitOfWork.BuildingProducts.GetAllAsync(wh => wh
                        .Where(x => x.BuildingId == id));

                    // Neue oder aktualisierte Produkte synchronisieren
                    if (building.BuildingProducts != null)
                    {
                        var buildingProducts = existingBuildingProducts.ToList();
                        foreach (var product in building.BuildingProducts)
                        {
                            var existingProduct = buildingProducts.FirstOrDefault(bp => bp.Id == product.Id);
                            if (existingProduct != null)
                            {
                                existingProduct = App.FullMapper.Map<BuildingProduct>(product);
                                _unitOfWork.BuildingProducts.Update(existingProduct);
                            }
                            else
                            {
                                product.BuildingId = id; // Das BuildingId für das neue Produkt setzen
                                var productEntity = App.FullMapper.Map<BuildingProduct>(product); // Optional: Mapping
                                _unitOfWork.BuildingProducts.Add(productEntity);
                            }
                        }

                        // Nicht mehr zugehörige Produkte entfernen
                        var updatedIds = building.BuildingProducts.Select(bp => bp.Id).ToList();
                        var productsToRemove = buildingProducts
                            .Where(bp => !updatedIds.Contains(bp.Id))
                            .ToList();

                        if (productsToRemove.Any())
                        {
                            _unitOfWork.BuildingProducts.DeleteRange(productsToRemove);
                        }
                    }

                    // Gebäude-Daten aktualisieren
                    var buildingToUpdate = App.FullMapper.Map<Building>(building);
                    buildingToUpdate.BuildingProducts.Clear(); // Produkte wurden schon separat behandelt
                    _unitOfWork.Buildings.Update(buildingToUpdate);

                    // Änderungen speichern
                    await _unitOfWork.SaveChangesAsync();
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

            var building = await _unitOfWork.Buildings.GetByIdAsync(x => x.Id == id);
            if (building == null) return NotFound();

            _unitOfWork.Buildings.Delete(building);
            await _unitOfWork.SaveChangesAsync();
            HttpContext.Session.Remove("Buildings");

            return RedirectToAction(nameof(Index));
        }

        private bool BuildingExists(int id) => _unitOfWork.Buildings.GetAll().Any(e => e.Id == id);

        [HttpPost, ActionName("SendInvoiceEmail")]
        public async Task<IActionResult> SendInvoiceEmail(int id, int month, int year)
        {
            var customers = await _unitOfWork.Customers.GetCustomersByBuildingIdAsync(id);
            using (SmtpClient smtpClient = new SmtpClient(_smtpConfig.Value.Server))
            {
                foreach (var item in customers.Where(x => x.Email != null))
                {
                    smtpClient.Credentials = new NetworkCredential(_smtpConfig.Value.Email, _smtpConfig.Value.Passwort);
                    smtpClient.EnableSsl = true;

                    var document = App.FullMapper.Map<DocumentViewModel>(await _unitOfWork.Documents.GetByIdAsync(
                        x => x.CustomerId == item.Id && x.Date!.Value.Year == year && x.Date!.Value.Month == month,
                        inc => inc.Include(x => x.Customer)
                            .Include(x => x.Books.Where(x=>!x.Hide))));
                    if (document == null)
                    {
                        RedirectToAction(nameof(Index));
                    }
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

                            Во прилог ви ја праќаме сметката за {month}/{year}. Ве молиме, проверете ја прикачената сметка и извршете ги потребните активности за плаќање.

                            Во случај на било какви прашања или недоразбирања можете слободно да не контактирате. Ви благодариме за Вашето внимание и соработка.
                            Со почит,

                            {_config.Value.Name}
                            {_config.Value.PhoneNumber}
                            {_config.Value.Email}
                            {_config.Value.Address}";
                    // create email message
                    MailMessage message = new MailMessage();
                    message.From = new MailAddress(_smtpConfig.Value.Email);
                    message.To.Add(item.Email);
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