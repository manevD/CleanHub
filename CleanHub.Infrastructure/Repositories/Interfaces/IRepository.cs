using System.Linq.Expressions;

namespace CleanHub.CleanHub.Infrastructure.Repositories.Interfaces
{
    public interface IRepository<T> where T : class
    {
        T GetById(int id);
        IEnumerable<T> GetAll(Func<IQueryable<T>, IQueryable<T>> include = null);
     //   IEnumerable<T> Find(Expression<Func<T, bool>> predicate);
        void Add(T entity);
        void Delete(T entity);
        void Save();
    }
}
