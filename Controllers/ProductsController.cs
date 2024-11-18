using CleanHub.CleanHub.Infrastructure.Data;
using CleanHub.Config;
using CleanHub.Entities;
using CleanHub.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace CleanHub.Controllers
{
    public class ProductsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ProductsController(ApplicationDbContext context, IOptions<SMTPConfig> config, IOptions<CompanyConfig> companyConfig)
        {
            _context = context;
        }
        // GET: ProductsController
        [Route("Продукти")]
        public async Task<IActionResult> Index()
        {
            string documentsJson = HttpContext.Session.GetString("Products");
            var settings = new JsonSerializerSettings
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            };
            List<ProductViewModel> products;
            if (!string.IsNullOrEmpty(documentsJson))
            {
                products = JsonConvert.DeserializeObject<List<ProductViewModel>>(documentsJson, settings);
            }
            else
            {
                var productEntity = await _context.Products.Select(c => new Product
                {
                    Id = c.Id,
                    ArticleNotes = c.ArticleNotes,
                    UnitOfMeasurement = c.UnitOfMeasurement,
                    Price = c.Price,
                    PriceWithTax = c.PriceWithTax,
                    Quantity = c.Quantity,
                    Tax = c.Tax
                }).ToListAsync();

                products = App.FullMapper.Map<List<ProductViewModel>>(productEntity);

                HttpContext.Session.SetString("Products", JsonConvert.SerializeObject(products, settings));
            }
            return View(products);
        }

        // GET: ProductsController/Create
        public ActionResult Create()
        {
            var productViewModel = new ProductViewModel();
            return View(productViewModel);
        }

        // POST: ProductsController/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProductViewModel product)
        {
            if (ModelState.IsValid)
            {
                var entity = App.FullMapper.Map<Product>(product);
                _context.Add(entity);
                await _context.SaveChangesAsync();
                HttpContext.Session.Remove("Products");

                return RedirectToAction(nameof(Index));
            }
            return View(product);
        }

        // GET: ProductsController/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }
            var productEntity = await _context.Products.FirstOrDefaultAsync(xd => xd.Id == id);
            var product = App.FullMapper.Map<ProductViewModel>(productEntity);
            if (product == null)
            {
                return NotFound();
            }

            return View(product);
        }

        // POST: ProductsController/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ProductViewModel product)
        {
            if (id != product.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var productEntity = App.FullMapper.Map<Product>(product);
                    _context.Update(productEntity);
                    await _context.SaveChangesAsync();
                    HttpContext.Session.Remove("Products");
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ProductExist(product.Id))
                    {
                        return NotFound();
                    }

                    throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(product);
        }

        // POST: ProductsController/Delete/5
      
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var product = await _context.Products
                .FirstOrDefaultAsync(m => m.Id == id);
            if (product == null)
            {
                return NotFound();
            }

            _context.Products.Remove(product);
            await _context.SaveChangesAsync();
            HttpContext.Session.Remove("Products");

            return RedirectToAction(nameof(Index));
        }

        private bool ProductExist(int id)
        {
            return _context.Products.Any(e => e.Id == id);
        }
    }
}
