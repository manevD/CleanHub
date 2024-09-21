using CleanHub.Entities;
using CleanHub.Infrastructure.Data;
using CleanHub.Providers.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CleanHub.Providers
{
    public class StaticDataProvider : IStaticDataProvider
    {

        // Optional: Caching mechanism to avoid frequent DB hits
        private readonly IMemoryCache _cache;
        private readonly ApplicationDbContext _context;
        public StaticDataProvider(IMemoryCache cache, ApplicationDbContext context)
        {
            _cache = cache;
            _context = context;
        }

        
        public async Task<List<Building>> GetBuildings()
        {
            // Define a cache key
            var cacheKey = "buildings_cache";

            if (!_cache.TryGetValue(cacheKey, out List<Building> buildings))
            {
                buildings = await _context.Buildings.Include(x => x.Customers)
                   .Include(b => b.BuildingProducts).AsNoTrackingWithIdentityResolution()
                   .ToListAsync();

                var cacheEntryOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
                    SlidingExpiration = TimeSpan.FromSeconds(10)
                };
                _cache.Set(cacheKey, buildings, cacheEntryOptions);
            }

            return buildings;
        }

        public async Task<List<Product>> GetProducts()
        {
            // Define a cache key
            var cacheKey = "products_cache";

            if (!_cache.TryGetValue(cacheKey, out List<Product> products))
            {
                products = await _context.Products.ToListAsync();

                var cacheEntryOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
                    SlidingExpiration = TimeSpan.FromSeconds(10)
                };

                _cache.Set(cacheKey, products, cacheEntryOptions);
            }
            return products;
        }
    }
}
