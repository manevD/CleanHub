using CleanHub.Attribute;
using CleanHub.Config;
using CleanHub.Entities;
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
using System.Text;

namespace CleanHub.Controllers
{
    [RequireLogin]
    public class BuildingsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private SMTPConfig _smtpConfig;
        private readonly CompanyConfig _config;
        private readonly ICompositeViewEngine _viewEngine;
        private readonly IWebHostEnvironment _env;
        private readonly IHttpContextAccessor _httpContextAccessor;

        private static int Month = DateTime.Now.Month;
        private static int Year = DateTime.Now.Year;
        public BuildingsController(ICompositeViewEngine viewEngine, IWebHostEnvironment env, IHttpContextAccessor httpContextAccessor, ApplicationDbContext context, IOptions<SMTPConfig> smtpConfig, IOptions<CompanyConfig>  config)
        {
            _httpContextAccessor = httpContextAccessor;

            _env = env;
            _viewEngine = viewEngine;
            _context = context;
            _smtpConfig = smtpConfig.Value;
            _config = config.Value;   
        }

        // GET: Buildings
        public async Task<IActionResult> Index()
        {
            string buildingsJson = HttpContext.Session.GetString("Buildings");
            var settings = new JsonSerializerSettings
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            };
            List<BuildingViewModel> buildings;
            if (!string.IsNullOrEmpty(buildingsJson))
            {
                buildings = JsonConvert.DeserializeObject<List<BuildingViewModel>>(buildingsJson, settings);
            }
            else
            {
                var buildingssEntity = await _context.Buildings.AsNoTracking().ToListAsync();
                buildings = App.FullMapper.Map<List<BuildingViewModel>>(buildingssEntity);

                HttpContext.Session.SetString("Buildings", JsonConvert.SerializeObject(buildings, settings));
            }
            return View(buildings);
        }

        // GET: Buildings/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var building = await _context.Buildings.Include(x=>x.Customers).FirstOrDefaultAsync(c => c.Id == id);
           
            var buildingViewModel = App.FullMapper.Map<BuildingViewModel>(building);
            ViewBag.Month = Month;
            ViewBag.Year = Year;
            return View(buildingViewModel);
        }

        // GET: Buildings/Create
        public IActionResult Create()
        {
            BuildingViewModel buildingViewModel = new BuildingViewModel();
            return View(buildingViewModel);
        }

        // POST: Buildings/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(BuildingViewModel building)
        {
            if (ModelState.IsValid)
            {
                var entity = App.FullMapper.Map<Building>(building);
                _context.Add(entity);
                await _context.SaveChangesAsync();
                HttpContext.Session.Remove("Buildings");

                return RedirectToAction(nameof(Index));
            }
            return View(building);
        }

        // GET: Buildings/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }
            var buildingEntity = _context.Buildings.FirstOrDefault(c => c.Id == id);
            var building = App.FullMapper.Map<BuildingViewModel>(buildingEntity);
            HttpContext.Session.Remove("Buildings");

            return View(building);
        }

        // POST: Buildings/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, BuildingViewModel building)
        {
            if (id != building.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var buildingToUpdate = App.FullMapper.Map<Building>(building);
                    _context.Update(buildingToUpdate);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!BuildingExists(building.Id))
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
            return View(building);
        }

        // GET: Buildings/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }
            var building = _context.Buildings.FirstOrDefault(m => m.Id == id);
            if (building == null)
            {
                return NotFound();
            }

            return View(building);
        }

        // POST: Buildings/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {

            var building = _context.Buildings.FirstOrDefault(x => x.Id == id);
            if (building != null)
            {
                _context.Buildings.Remove(building);
            }
            await _context.SaveChangesAsync();
            HttpContext.Session.Remove("Buildings");

            return RedirectToAction(nameof(Index));
        }

        private bool BuildingExists(int id)
        {
            return _context.Buildings.Any(e => e.Id == id);
        }

        [HttpPost, ActionName("SendInvoiceEmail")]
        public async Task<IActionResult> SendInvoiceEmail(int id, int month, int year)
        {
            var customers = _context.Buildings.Where(x => x.Id == id).Include(x => x.Customers).SelectMany(d => d.Customers!).ToList();
        
            foreach (var item in customers.Where(x => x.Email != null))
            {
                using (SmtpClient smtpClient = new SmtpClient(_smtpConfig.Server))
                {
                    smtpClient.Credentials = new NetworkCredential(_smtpConfig.Email, _smtpConfig.Passwort);
                    smtpClient.EnableSsl = true;

                    var document = App.FullMapper.Map<DocumentViewModel>(_context.Documents.Include(x=>x.Customer).Include(x=>x.Books).FirstOrDefault(x => x.CustomerId == item.Id && x.Date!.Value.Year == year && x.Date!.Value.Month == month));
                    document.Company = _config;
                    document.IsForPdf = true;
                    
                    string htmlContent = await RenderPartialViewToStringAsync("~/Views/Shared/_DocumentDetailPartial.cshtml", document);
                    var request = _httpContextAccessor.HttpContext.Request;
                    string baseUrl = $"{request.Scheme}://{request.Host.Value}/";
                    HtmlToPdf converter = new HtmlToPdf();
                  
                    try
                    {
                        // create a new pdf document converting an url
                        PdfDocument doc = converter.ConvertHtmlString(htmlContent, baseUrl);

                        // create memory stream to save PDF
                        MemoryStream pdfStream = new MemoryStream();

                        // save pdf document into a MemoryStream
                        doc.Save(pdfStream);

                        // reset stream position
                        pdfStream.Position = 0;

                        // create email message
                        MailMessage message = new MailMessage();
                        message.From = new MailAddress(_smtpConfig.Email); ;
                        message.To.Add(_smtpConfig.Recipient);
                        message.Subject = "SelectPdf Sample - Convert and Email as Attachment";

                        message.Attachments.Add(new Attachment(pdfStream, "Document.pdf"));

                        // send email
                        smtpClient.Send(message);

                        // close pdf document
                        doc.Close();
                    }
                    catch (Exception ex) { }
                }
            }
            return RedirectToAction("Details",id);
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
