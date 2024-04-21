using CleanHub.Attribute;
using CleanHub.Data;
using CleanHub.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CleanHub.Controllers
{
    [RequireLogin]
    public class BuildingsController : Controller
    {
        private readonly ApplicationDbContext _context;

        private readonly IMemoryCache _cache;
        private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(30); // Adjust expiration time as needed

        public BuildingsController(ApplicationDbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        // GET: Buildings
        public async Task<IActionResult> Index()
        {
            if (!_cache.TryGetValue("BuildingsList", out List<Building> buildings))
            {
                // Data not in cache, retrieve from database
                buildings = await _context.Buildings.Include(x=>x.Residents).ToListAsync();
                // Cache the data
                _cache.Set("BuildingsList", buildings, _cacheExpiration);
            }
            return View(await _context.Buildings.ToListAsync());
        }

        // GET: Buildings/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }
            if (!_cache.TryGetValue("BuildingsList", out List<Building> buildings))
            {
                // Data not in cache, retrieve from database
                buildings = await _context.Buildings.Include(x=>x.Residents).ToListAsync();
                // Cache the data
                _cache.Set("BuildingsList", buildings, _cacheExpiration);
            }
            var building =  buildings.FirstOrDefault(m => m.Id == id);
            if (building == null)
            {
                return NotFound();
            }

            return View(building);
        }

        // GET: Buildings/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Buildings/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,NumberOfUnits")] Building building)
        {
            if (ModelState.IsValid)
            {
                _context.Add(building);
                await _context.SaveChangesAsync();
                _cache.Remove("BuildingsList");
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
            _cache.TryGetValue("BuildingsList", out List<Building> buildings);
            var building = buildings.FirstOrDefault(x=>x.Id==id);
            if (building == null)
            {
                return NotFound();
            }
            return View(building);
        }

        // POST: Buildings/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Building building)
        {
            if (id != building.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _cache.TryGetValue("BuildingsList", out List<Building> buildings);
                    building.NumberOfResidence = buildings.FirstOrDefault(x=>x.Id == id).Residents.Count();
                    _context.Update(building);

                    await _context.SaveChangesAsync();
                    _cache.Remove("BuildingsList");
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
            _cache.TryGetValue("BuildingsList", out List<Building> buildings);
            var building = buildings.FirstOrDefault(m => m.Id == id);
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
            _cache.TryGetValue("BuildingsList", out List<Building> buildings);

            var building = buildings.FirstOrDefault(x=>x.Id==id);
            if (building != null)
            {
                _context.Buildings.Remove(building);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool BuildingExists(int id)
        {
            _cache.TryGetValue("BuildingsList", out List<Building> buildings);

            return buildings.Any(e => e.Id == id);
        }
    }
}
