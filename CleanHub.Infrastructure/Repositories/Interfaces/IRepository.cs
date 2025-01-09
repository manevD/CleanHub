using System.Linq.Expressions;

namespace CleanHub.CleanHub.Infrastructure.Repositories.Interfaces
{
    public interface IRepository<T> where T : class
    {
        Task<List<T>> GetAllWithIncludeAsync(
            Func<IQueryable<T>, IQueryable<T>>? include = null,
            Expression<Func<T, bool>>? predicate = null);
        Task<T?> GetByIdAsync(Expression<Func<T, bool>> predicate, Func<IQueryable<T>, IQueryable<T>>? include = null);
        Task<IEnumerable<T>> GetAllAsync(Func<IQueryable<T>, IQueryable<T>>? include = null);
        IEnumerable<T> GetAllNoTrakcing(Func<IQueryable<T>, IQueryable<T>>? include = null);

        IEnumerable<T> GetAll(Func<IQueryable<T>, IQueryable<T>>? include = null);

        void Add(T entity);
        void Update(T entity);
        void UpdateRange(IEnumerable<T> entities);
        void Delete(T entity);
        void DeleteRange(IEnumerable<T> entities);

        Task SaveChangesAsync();
         Task<int?> GetMaxAsync(Expression<Func<T, int?>> selector);

    }
}
