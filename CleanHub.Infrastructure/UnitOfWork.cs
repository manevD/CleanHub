using CleanHub.Core.Interfaces;
using CleanHub.Infrastructure.Data;
using CleanHub.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace CleanHub.Infrastructure
{
    public class UnitOfWork<TEntity, TRepository> : IUnitOfWork<TEntity> where TEntity : class where TRepository : IGenericRepository<TEntity>
    {
        private readonly ApplicationDbContext _context = new ApplicationDbContext();
        private readonly Factory _factory;

        public UnitOfWork()
        {
            _factory = new Factory();

            Repositories = new Dictionary<Type, object>();
        }

        private GenericRepository<TEntity> _getCustomOrDefaultRepository;

        public GenericRepository<TEntity> GetCustomOrDefaultRepository
        {
            get
            {
                if (_getCustomOrDefaultRepository == null)
                {
                    _getCustomOrDefaultRepository = new GenericRepository<TEntity>(_context);
                }

                return _getCustomOrDefaultRepository;
            }
        }

        protected virtual T MakeRepository<T>(
        Func<ApplicationDbContext, object> factory, ApplicationDbContext dbContext)
        {
            var f = factory ?? _factory.GetRepositoryFactory<T>();
            if (f == null) throw new NotSupportedException(typeof(T).FullName);
            var repo = (T)f(dbContext);
            Repositories[typeof(T)] = repo;
            return repo;
        }

        protected Dictionary<Type, object> Repositories { get; private set; }

        public IGenericRepository<T> GetGenericRepository<T>() where T : class
        {
            return GetCustomRepository<IGenericRepository<T>>(
                _factory.GetRepositoryFactoryForEntityType<T>());
        }

        public virtual T GetCustomRepository<T>(Func<ApplicationDbContext, object> factory = null)
            where T : class
        {
#pragma warning disable IDE0018 // Inline variable declaration
            object repoObj;
#pragma warning restore IDE0018 // Inline variable declaration
            Repositories.TryGetValue(typeof(T), out repoObj);
            if (repoObj != null) { return (T)repoObj; }
            return MakeRepository<T>(factory, _context);
        }

        public void SetRepository<T>(T repository)
        {
            Repositories[typeof(T)] = repository;
        }

        public void Save()
        {
            var saved = false;
            while (!saved)
            {
                try
                {
                    // Attempt to save changes to the database
                    _context.SaveChanges();
                    saved = true;
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    foreach (var entry in ex.Entries)
                    {
                        if (entry.Entity is TEntity)
                        {
                            var proposedValues = entry.CurrentValues;
                            var databaseValues = entry.GetDatabaseValues();

                            foreach (var property in proposedValues.Properties)
                            {
                                var proposedValue = proposedValues[property];
                                var databaseValue = databaseValues[property];

                                // TODO: decide which value should be written to database
                                // proposedValues[property] = <value to be saved>;
                            }

                            // Refresh original values to bypass next concurrency check
                            entry.OriginalValues.SetValues(databaseValues);
                        }
                        else
                        {
                            throw new NotSupportedException(
                                "Don't know how to handle concurrency conflicts for "
                                + entry.Metadata.Name);
                        }
                    }
                }
            }
        }

        private bool _disposed = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _context.Dispose();
                }
            }
            _disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }

    internal class Factory
    {
        private readonly IDictionary<Type, Func<ApplicationDbContext, object>> _factories;

        public Factory() { _factories = GetFactories(); }

        public Factory(IDictionary<Type, Func<ApplicationDbContext, object>> factories)
        {
            _factories = factories;
        }

        private IDictionary<Type, Func<ApplicationDbContext, object>> GetFactories()
        {
            return null;
            //return new Dictionary<Type, Func<ApplicationDbContext, object>>
            //{ { typeof(ICategoryRepository),
            //        context => new CategoryRepository(context) },
            //    { typeof(IFloorRepository),
            //        context => new FloorRepository(context) },
            //    { typeof(ITableRepository),
            //        context => new TableRepository(context) },
            //    { typeof(IProductRepository),
            //        context => new ProductRepository(context) },
            //     { typeof(IIngredientRepository),
            //        context => new IngredientRepository(context) },
            //     //{ typeof(IOrderRepository),
            //     //   context => new OrderRepository(context) },
            //     { typeof(IProductIngredientRepository),
            //        context => new ProductIngredientRepository(context) },
            //      { typeof(IEmployeeRepository),
            //        context => new EmployeeRepository(context) },
            //       { typeof(IUserRepository),
            //        context => new UserRepository(context) },
            //     { typeof(IEventRepository),
            //        context => new EventRepository(context) },
            //      { typeof(IImageRepository),
            //        context => new ImageRepository(context) },
            //  { typeof(IApplicationRoleRepository),
            //        context => new ApplicationRoleRepository(context) } };
        }

        protected virtual Func<ApplicationDbContext, object> DefaultEntityRepositoryFactory<T>()
            where T : class
        {
            return dbContext => new GenericRepository<T>(dbContext);
        }

        public Func<ApplicationDbContext, object> GetRepositoryFactory<T>()
        {
#pragma warning disable IDE0018 // Inline variable declaration
            Func<ApplicationDbContext, object> factory;
#pragma warning restore IDE0018 // Inline variable declaration
            _factories.TryGetValue(typeof(T), out factory);
            return factory;
        }

        public Func<ApplicationDbContext, object> GetRepositoryFactoryForEntityType<T>()
            where T : class
        {
            return GetRepositoryFactory<T>() ?? DefaultEntityRepositoryFactory<T>();
        }
    }
}
